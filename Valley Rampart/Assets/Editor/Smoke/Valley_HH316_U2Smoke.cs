using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.316 · U-2「箱 → 仓搬运链」（`D798` 三歧义已裁）同局冒烟（Editor-only）
//  口径真源：HH.316 任务书 §件1~件6 ＋ §判据 1~7；`09_资源与仓库.md` §9.8（谁去捡/谁能搬）/ §九（掉落箱）/ §五（仓库=标签）。
//
//  入口纪律（test-harness-first 铁律1）：**走正门** `TestHarnessApi.EnterTestRun`（⛔ 禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → 封盘 → ExitTestRun → 退 Play。
//
//  段（与判据一一对应）：
//    §A 起局（正门）＋ 玩家前置（仓/木/金/工人）
//    §B 判据1+4 链 A 成形（箱 ⇒ 工人取货 ⇒ 到账 · 分资源明细）＋ 多资源箱逐轮搬完
//    §C 判据2 靶例（投料中态拆除 ⇒ 同 coord 两箱 ⇒ 搬回 ⇒ 逐条目到账：金→Gold 字段／材料→国库仓）
//    §D 判据5 金路径（⛔ 不进「声明收金」的仓 —— 探针注入复刻 `Well` 误配面）
//    §E 判据6 箱＝仓存档往返（contents 逐值一致 · 单一真源）
//    §F 判据7 过期仍消亡 ＋ ⛔ 无「箱再掉箱」递归
//    §G 件6 链 B（点箱 ＝ 立案搬运任务 · ⛔ 不消费箱体、⛔ 无独立入账口）
//    §H 判据3 无主先到先得（AI 工人可搬 · 鉴别力：未改则应恒 None）
//  落盘：Logs/hh316_u2/hh316_u2_smoke.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH316U2Smoke
{
    public const string Tag = "HH316U2";
    private const string Menu = "Valley/验证/HH316 U2 箱仓搬运链 同局冒烟（正门进局）";
    private const int SEED = 31620;
    private const string SLOT = "hh316_u2";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;
    private static float _origTaskTimeout = -1f;
    private static GameObject _goldProbeGo;          // §D 探针注入的「声明收金」仓（收尾清除）
    private static GameObject _probeStoreGo;         // §A 探针注入的「通用收货仓」（收尾清除）
    private static StorageComponent _probeStore;     // 玩家国(0) 通用仓(res · 容量 500) ⇒ 材料到账面

    [MenuItem(Menu, priority = 216)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 冒烟已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0;
        Sb.AppendLine("# HH.316 · U-2「箱 → 仓搬运链」同局冒烟（正门 EnterTestRun · seed=" + SEED + " 槽=" + SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Sb.AppendLine("# 口径：HH.316 件1~件6 ＋ 判据 1~7；D798 三歧义裁决（链B并回链A／装载段A-1／无主-1先到先得）");
        new GameObject("HH316U2SmokeHost").AddComponent<Host>().Go(Run());
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string s) { Sb.AppendLine(s); Debug.Log("[" + Tag + "] " + s); }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = SLOT, kingdomName = "U2冒烟"
        };
        Log("── §A 正门进局（seed=" + SEED + " Small/difficulty=2）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }
        Log("§A 起局：地图=" + map.width + "x" + map.height + " 日=" + Day()
            + " 金=" + Gold() + " 箱子数=" + ChestTotal()
            + " 玩家国工人=" + CountUnits(0) + " 倍率=" + Time.timeScale);

        // 玩家前置：可收木的仓 ＋ 木 ＋ 工人（照 HH315_M1C_Smoke §A 先例）
        var anchorSub = GridSystem.Instance.WorldToSubCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        Vector2Int anchorCell;
        if (anchorSub.HasValue)
        {
            var ac = GridSystem.Instance.SubToCell(anchorSub.Value);
            anchorCell = new Vector2Int(ac.x, ac.y);
        }
        else anchorCell = MapGenRules.NearestWalkable(map, map.width / 2, map.height / 2);
        anchorCell = MapGenRules.NearestWalkable(map, anchorCell.x, anchorCell.y);

        var wh = FindPlayerStore(ResourceType.Wood);
        if (wh == null)
        {
            var whDef = DefOf("Warehouse");
            var wb = MakeAt(whDef, FreeCell(whDef, anchorCell), 0, true, BuildingState.Active);
            wh = wb != null ? wb.GetComponent<StorageComponent>() : null;
            Log("§A 玩家无「可收木」仓 ⇒ 探针直建 Warehouse 补位：" + (wb != null));
        }
        int woodAdded = wh != null ? wh.Add(ResourceType.Wood, 60) : 0;
        Log("§A 玩家仓：" + (wh != null ? wh.gameObject.name + " 声明=[" + string.Join(",", wh.DeclaredPaths) + "]"
            + " 木 +" + woodAdded + " 存量=" + wh.GetAmount(ResourceType.Wood) : "**无**")
            + " ｜玩家国各仓合计：" + PlayerStoresText());

        // ⚠️ 起局国库已满（难度2 初始 石100/木100/粮50 = 容量250 ⇒ 开局即「Food 溢出装箱」）
        //   ⇒ 材料无处可卸会走「国库满 ⇒ 溢出再装箱」回路（读数被污染、工人被占死）
        //   ⇒ 探针注入一个**玩家国(0) 通用收货仓（res · 容量500）**承接到账（⛔ 不作任务源；收尾清除）。
        InjectProbeStore(anchorCell);
        if (Gold() < 60) RulerController.Instance?.ModifyResource(ResourceType.Gold, true, 100);
        Log("§A 金基线（保证 §C 下单可付）=" + Gold() + " ｜工人数（玩家国）=" + CountUnits(0));

        int spawned = 0;
        for (int i = 0; i < 2; i++)
            if (TestFixtureApi.SpawnFixtureUnitForDiag(Occupation.Worker, anchorCell, 0, i)) spawned++;
        Log("§A 玩家工人补位：spawn=" + spawned + " ⇒ 玩家国工人数=" + CountUnits(0));

        // ⚠️ 取证用临时放宽 `TaskScheduler.taskTimeout`（⛔ 仅探针内存态 · 收尾恢复；照 HH315_M1C_Smoke 先例：
        //   测试段时标 15× 于工人位移 ⇒ 默认 30 游戏秒不足以走完两段位移 ⇒ 必然超时放弃）。
        _origTaskTimeout = TaskScheduler.HasInstance ? TaskScheduler.Instance.taskTimeout : -1f;
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.taskTimeout = 3600f;
        Log("§A 取证参数：TaskScheduler.taskTimeout " + _origTaskTimeout.ToString("F0") + " → 3600（探针临时 · 收尾恢复）");

        // ================= §B 判据1 + 判据4：链 A 成形 ＋ 多资源箱逐轮搬完 =================
        Log("");
        Log("## §B 判据1「箱 ⇒ 工人取货 ⇒ 到账」＋ 判据4「多资源箱逐轮搬完」");
        Log("§B 鉴别力：改前（箱非 ITaskSource）⇒ 无工人被派（CountAssignedWorkers 恒 0）、箱永不空；"
            + "若装载段未命中箱容器 ⇒ 装载恒 false（箱不空）");
        var bCell = NearCell(anchorCell, 3);
        var bPack = ResourceList.Of(
            new ResourceAmount(ResourceType.Wood, 6),
            new ResourceAmount(ResourceType.Stone, 4),
            new ResourceAmount(ResourceType.Gold, 3));
        string bBefore = PlayerStoresText(); int bGoldBefore = Gold();
        var bChest = SpawnChestAt(bCell, bPack, Faction.None);
        Log("§B 落箱前：金=" + bGoldBefore + " ｜玩家国各仓=" + bBefore + " ｜格(" + bCell.x + "," + bCell.y + ")箱数=" + ChestCountAt(bCell));
        if (bChest == null)
        {
            Log("❌ SpawnChest 返回 null ⇒ §B 中止");
        }
        else
        {
            Log("§B 落箱后：箱在场=" + (bChest != null) + " 声明=[" + string.Join(",", bChest.Store != null ? bChest.Store.DeclaredPaths : new string[0])
                + "] 容量=" + (bChest.Store != null ? bChest.Store.capacity : -1)
                + "（期望＝Σ量×体积=" + SpaceOf(bPack) + "）内容=" + CostText(bChest.contents)
                + " 是否容器真源=" + SameList(bChest.contents, bChest.Store != null ? bChest.Store.Contents : ResourceList.Empty));
            int f0 = Time.frameCount; float rt0 = Time.realtimeSinceStartup;
            int assignedSeen = 0; int firstAssignFrame = -1;
            while (bChest != null && !bChest.IsEmpty && Time.frameCount - f0 < 4000 && Time.realtimeSinceStartup - rt0 < 150f)
            {
                yield return null;
                if (bChest == null) break;
                KeepAlive(bChest);   // ⛔ 观测窗保活（隔离过期机制 · 见 KeepAlive 注）
                int n = AssignedWorkers(bChest);
                if (n > assignedSeen) assignedSeen = n;
                if (n > 0 && firstAssignFrame < 0) firstAssignFrame = Time.frameCount - f0;
                if ((Time.frameCount - f0) % 400 == 0)
                    Log("§B …诊断 帧=" + (Time.frameCount - f0) + " 箱余=" + CostText(bChest.contents)
                        + " 在册搬运工人=" + n + WorkersDiag(0, bCell));
            }
            Log("§B ⭐ 链成形：箱在场=" + (bChest != null) + " 箱余=" + (bChest != null ? CostText(bChest.contents) : "已销毁")
                + " ｜曾派工人数=" + assignedSeen + "（首派帧=" + (firstAssignFrame >= 0 ? firstAssignFrame.ToString() : "—") + "）");
            yield return WaitDelivered(0);   // 等最后一批搬完（箱空瞬间最后一批仍在背包 ⇒ 照读会误判未到账）
            Log("§B ⭐ 到账后：金=" + bGoldBefore + "→" + Gold() + "（该箱金 3 · 期望 +3 ⇒ 金直通 Gold 字段）"
                + " ｜玩家国各仓=" + bBefore + "→" + PlayerStoresText() + "（该箱 木6/石4 · 期望逐条目到账）"
                + "\n§B 全账=" + LedgerText());
            Log("§B 判据4：箱是否**逐轮搬完**=" + (bChest == null || bChest.IsEmpty)
                + "（多资源＝表序逐轮广告多轮搬运；⛔ 不是「只搬一种就失效」）");
        }

        // ================= §C 判据2：靶例（投料中态拆除 ⇒ 同 coord 两箱）=================
        Log("");
        Log("## §C 判据2 靶例「投料中态拆除 ⇒ 同 coord 两箱 ⇒ 两箱都被搬回 ⇒ 逐条目到账」");
        Log("§C 鉴别力：改前（无搬运链）⇒ 两箱内容物**必丢**（M1-C 件3「退还随本体掉箱」功能上作废）");
        var probeDef = Object.Instantiate(DefOf("House"));
        probeDef.name = "House_U2_Probe";
        probeDef.cost = ResourceList.Of(new ResourceAmount(ResourceType.Gold, 20), new ResourceAmount(ResourceType.Wood, 400));
        Log("§C 探针 def.cost=" + CostText(probeDef.cost) + "（金20 ＋ 木400 ⇒ 木**不可能料齐** ⇒ 工地仓必留内容物 ⇒ 双箱可复现）");
        var c1 = MakeAt(probeDef, FreeCell(probeDef, anchorCell), 0, true, BuildingState.Active);
        if (c1 == null) Log("❌ 探针建筑直建失败 ⇒ §C 中止");
        else
        {
            var cCoord = c1.coord;
            // 手工复刻「下单即扣金」（真下单路径 `BuildController.TryBuild` 需放置校验/种族门禁；本处直建 + 手工扣金，读数等价）
            var goldOnly = Building.GoldOnlyOf(probeDef.cost);
            RulerController.Instance?.ModifyResource(ResourceType.Gold, false, goldOnly.Get(ResourceType.Gold));
            c1.StartConstructing(probeDef.cost);     // 投料态（工地仓注册 · 广告搬料任务）
            yield return null;
            // ⭐ 探针直投工地仓（确定性 ⇒ 工地仓内容物非空 ⇒ 拆除后**第二箱**必可复现；照 HH315_M1C_Smoke §C 先例）
            int siteDep = c1.SiteStore != null ? c1.SiteStore.Deposit(ResourceType.Wood, 3) : -1;
            Log("§C 投料态：awaiting=" + c1.IsSiteAwaitingMaterials + " SiteNeed=" + CostText(c1.SiteNeed)
                + " 已扣金=" + goldOnly.Get(ResourceType.Gold) + "（金=" + Gold() + "）"
                + " ｜探针直投工地仓 Wood=" + siteDep + " ⇒ 内容物=" + (c1.SiteStore != null ? CostText(c1.SiteStore.Contents) : "—"));

            int goldBeforeC = Gold(); string matsBeforeC = PlayerStoresText(); int chestsBeforeC = ChestCountAt(cCoord);
            Log("§C 落箱前：金=" + goldBeforeC + " ｜玩家国各仓=" + matsBeforeC + " ｜格(" + cCoord.x + "," + cCoord.y + ")箱数=" + chestsBeforeC
                + "\n§C 落箱前全账=" + LedgerText());

            // ⚠️ 靶例就地补 3 名工人（**拆除前**补 ⇒ 首次广告时最近空闲工人在场；隔离「可达性/世界态/他国先到先得」
            //   对「搬回」这一被测对象的外因影响 · ⛔ 非生产改动）
            int cSpawn = 0;
            for (int i = 0; i < 3; i++)
                if (TestFixtureApi.SpawnFixtureUnitForDiag(Occupation.Worker, new Vector2Int(cCoord.x, cCoord.y), 0, i)) cSpawn++;
            Log("§C 靶例就地补工人（拆除前）：spawn=" + cSpawn);

            c1.Demolish();
            int df0 = Time.frameCount; float drt0 = Time.realtimeSinceStartup;
            while (c1 != null && c1.IsDemolishing && Time.frameCount - df0 < 8000 && Time.realtimeSinceStartup - drt0 < 240f)
            {
                yield return null;
                if ((Time.frameCount - df0) % 500 == 0)
                    Log("§C …拆除诊断 帧=" + (Time.frameCount - df0)
                        + " 进度=" + (c1 != null ? c1.DemolishProgress.ToString("F3") : "—")
                        + " 在册拆除工人=" + AssignedWorkers(c1) + WorkersDiag(0, new Vector2Int(cCoord.x, cCoord.y)));
            }
            yield return null;
            Log("§C ⭐ 落箱后：建筑在场=" + (c1 != null) + "（真拆帧=" + (Time.frameCount - df0) + "）"
                + " ｜同 coord 箱数=" + chestsBeforeC + "→" + ChestCountAt(cCoord)
                + " 箱内容=" + ChestsTextAt(cCoord) + "（期望 2 箱 · U-4 双箱面）");

            // 等两箱搬完（逐箱判空）
            Log("§C 观测窗保活：两箱 bornDay 逐帧顶到当天（⛔ 隔离「15× 时标 ⇒ 3 游戏日过期回收」· 过期机制另由 §F 判定）");
            int hf0 = Time.frameCount; float hrt0 = Time.realtimeSinceStartup;
            while (ChestsNonEmptyAt(cCoord) > 0 && Time.frameCount - hf0 < 6000 && Time.realtimeSinceStartup - hrt0 < 180f)
            {
                yield return null;
                KeepAliveAt(cCoord);   // ⛔ 观测窗保活（两箱面 · 隔离过期机制）
                if ((Time.frameCount - hf0) % 500 == 0)
                    Log("§C …诊断 帧=" + (Time.frameCount - hf0) + " 未空箱数=" + ChestsNonEmptyAt(cCoord)
                        + " 内容=" + ChestsTextAt(cCoord) + " 在册搬运工人=" + AssignedAt(cCoord)
                        + " 在册[国/npc:态]=" + AssignedUnitsText()
                        + WorkersDiag(0, new Vector2Int(cCoord.x, cCoord.y)));
            }
            yield return WaitDelivered(0);   // 等最后一批搬完（箱空瞬间最后一批仍在背包）
            Log("§C ⭐ 到账后：金=" + goldBeforeC + "→" + Gold() + "（箱内金合计应到账 ⇒ Gold 字段）"
                + " ｜玩家国各仓=" + matsBeforeC + "→" + PlayerStoresText() + "（箱内材料应到账 ⇒ 国库仓）"
                + " ｜同 coord 未空箱数=" + ChestsNonEmptyAt(cCoord)
                + "\n§C 到账后全账=" + LedgerText() + "（箱为**无主源** ⇒ 任一王国先到先得 · §9.8：可由他国账体现）"
                + "\n§C 在册[国/npc:态]=" + AssignedUnitsText());
        }

        // ================= §D 判据5：金路径（⭐ M1-E 口径演进：金**走仓**）=================
        Log("");
        Log("## §D 判据5「金路径（⭐ M1-E 起：金走 FindNearestAvailable ⇒ 进最近同国收金仓 —— 全库仅国库收金 · U-7 已修）」");
        Log("§D 鉴别力（⭐ M1-E 口径）：金走仓 ⇒ 应进「最近的同国 ＋ 收金仓」——本探针注入仓与国库（Vault）**同在主城** ⇒ 二者竞争；");
        Log("§D     读「注入仓金」与「Vault 金」**双侧**即可判去向（⚠️ 旧 M1-C 口径「金直通 Gold 字段 · ⛔ 不进仓」已被 D805 件4 推翻）");
        var host = FindPlayerCastle();
        if (host == null) Log("❌ 玩家主城不在场 ⇒ §D 中止（探针收金仓无法挂王国）");
        else
        {
            _goldProbeGo = new GameObject("HH316_GoldStore");
            _goldProbeGo.transform.SetParent(host.transform, false);
            var gs = _goldProbeGo.AddComponent<StorageComponent>();
            gs.SetDeclaredPaths(new[] { "res_currency.gold" });   // 复刻 Well.asset 误配声明
            gs.capacity = 100;
            WarehouseRegistry.Register(gs);
            var dCell = NearCell(CellOfWorld(host.transform.position), 4);
            int dGold0 = Gold();
            var dChest = SpawnChestAt(dCell, ResourceList.Of(new ResourceAmount(ResourceType.Gold, 7)), Faction.None);
            Log("§D 注入「声明收金」仓 @" + Vec(_goldProbeGo.transform.position) + "（res_currency.gold · 容量100）"
                + " ｜落箱前 金=" + dGold0 + " 该仓金=" + gs.GetAmount(ResourceType.Gold) + " 箱=" + (dChest != null));
            int gf0 = Time.frameCount; float grt0 = Time.realtimeSinceStartup;
            while (dChest != null && !dChest.IsEmpty && Time.frameCount - gf0 < 4000 && Time.realtimeSinceStartup - grt0 < 150f)
            { yield return null; KeepAlive(dChest); }   // ⛔ 观测窗保活
            yield return WaitDelivered(0);   // 等最后一批搬完（箱空瞬间最后一批仍在背包）
            int vaultGoldD = TreasureVault.Instance != null ? TreasureVault.Instance.GetAmount(ResourceType.Gold) : -1;
            Log("§D ⭐ 读数（⭐ M1-E 口径）：金(Vault)=" + dGold0 + "→" + Gold()
                + " ｜注入仓金=" + gs.GetAmount(ResourceType.Gold)
                + " ｜Vault 金=" + vaultGoldD
                + " ⇒ 判读：注入仓金=0 且 Vault 已增 ⇒ 金**走仓**（进 Vault · 距离胜）；"
                + "⚠️ 世界背景亦有入账 ⇒ 以「双侧读数」判去向，⛔ 不用形容词"
                + " ｜箱=" + (dChest != null ? CostText(dChest.contents) : "空/已熔"));
            // 清理探针仓
            WarehouseRegistry.Unregister(gs);
            Object.Destroy(_goldProbeGo);
            _goldProbeGo = null;
        }

        // ================= §E 判据6：存档往返（箱＝仓 · 唯一真源）=================
        Log("");
        Log("## §E 判据6「箱＝仓不破坏存档：contents 往返逐值一致（⛔ 禁双写 ⇒ 单一真源）」");
        var farCell = FarCell(anchorCell, 26);
        var ePack = ResourceList.Of(
            new ResourceAmount(ResourceType.Wood, 3),
            new ResourceAmount(ResourceType.Stone, 2),
            new ResourceAmount(ResourceType.Ore, 1),
            new ResourceAmount(ResourceType.Gold, 5));
        var eChest = SpawnChestAt(farCell, ePack, Faction.None);
        if (eChest == null) Log("❌ §E 落箱失败 ⇒ 中止");
        else
        {
            Log("§E 存档前：箱 @" + eChest.cell.x + "," + eChest.cell.y
                + " 内容=" + CostText(eChest.contents)
                + " 容器=" + CostText(eChest.Store.Contents)
                + " 容量=" + eChest.Store.capacity + "（期望 " + SpaceOf(ePack) + "）"
                + " 单一真源=" + SameList(eChest.contents, eChest.Store.Contents));
            bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
            bool loaded = SaveManager.Instance != null && SaveManager.Instance.Load(SLOT);
            yield return null; yield return null;
            var eChest2 = FindChestAt(farCell);
            Log("§E Save=" + saved + " Load=" + loaded
                + " ｜读档后：箱在场=" + (eChest2 != null)
                + " 内容=" + (eChest2 != null ? CostText(eChest2.contents) : "—")
                + " 容器=" + (eChest2 != null ? CostText(eChest2.Store.Contents) : "—")
                + " 容量=" + (eChest2 != null ? eChest2.Store.capacity : -1)
                + " ⇒ 逐值往返一致=" + (eChest2 != null && SameList(eChest2.contents, ePack)
                    && SameList(eChest2.Store.Contents, ePack) && eChest2.Store.capacity == SpaceOf(ePack)));
            // ⚠️ 读档重建销毁探针收货仓（非存档对象）⇒ 复注入（§F/§G 的到账读数续用）
            InjectProbeStore(anchorCell);
        }

        // ================= §F 判据7：过期仍消亡 ＋ ⛔ 无递归 =================
        Log("");
        Log("## §F 判据7「过期仍消亡（§九 设计内）＋ 未被『箱再掉箱』递归」");
        Log("§F 鉴别力：若「过期即洒落」被误实现 ⇒ 过期后原地会出现新箱（bornDay 重置 ⇒ 无限续命 ⇒ 违 §九 :358 护栏）");
        var fCell = FarCell(anchorCell, 30);
        float expire = ChestConfig.Instance != null ? ChestConfig.Instance.expireDays : 3f;
        var fChest = SpawnChestAt(fCell, ResourceList.Of(new ResourceAmount(ResourceType.Stone, 2)), Faction.None);
        if (fChest == null) Log("❌ §F 落箱失败 ⇒ 中止");
        else
        {
            int total0 = ChestTotal();
            fChest.bornDay = Day() - expire - 1f;   // 伪造成已过期（⛔ 不改生产字段语义）
            Log("§F 样本：箱 @" + fCell.x + "," + fCell.y + " bornDay=" + fChest.bornDay + "（当前日 " + Day()
                + " · expireDays=" + expire + "）全局箱数=" + total0);
            for (int i = 0; i < 5; i++) yield return null;
            Log("§F ⭐ 读数：过期后该格箱数=" + ChestCountAt(fCell) + "（期望 0 ⇒ 消亡）"
                + " ｜全局箱数=" + total0 + "→" + ChestTotal()
                + "（期望**不增** ⇒ ⛔ 无「箱再掉箱」递归）"
                + " ｜该格内容物=" + ChestsTextAt(fCell) + "（期望 无箱 ⇒ 连同内容消亡 · §七「消亡＝连同内容消失」）");
        }

        // ================= §G 件6 链 B：点箱 ＝ 立案搬运任务 =================
        Log("");
        Log("## §G 件6 链 B「玩家手点 ＝ 调用搬运任务的一种形式」（09 §9.8 :390）");
        Log("§G 鉴别力：旧径（`Interact` → `Pickup` 消费）⇒ 箱**当场消失**且内容物不入账；"
            + "被否决的直入账径 ⇒ 箱当场消失 ＋ 国库/金立即增加 ⇒ 本列应读「箱仍在场 ＋ 内容物仍在容器」");
        var gCell = NearCell(anchorCell, 5);
        var gPack = ResourceList.Of(new ResourceAmount(ResourceType.Stone, 2));
        var gChest = SpawnChestAt(gCell, gPack, Faction.None);
        if (gChest == null) Log("❌ §G 落箱失败 ⇒ 中止");
        else
        {
            int gGold0 = Gold(); string gStores0 = PlayerStoresText();
            var res = gChest.Interact(new Interactor(Faction.PlayerCamp, gChest.transform.position));
            Log("§G 手点：返回 kind=" + res.kind + "（期望 None ⇒ ⛔ 不返回资源包/面板）"
                + " ｜箱仍在场=" + (gChest != null) + " 内容物仍在容器=" + (gChest != null && !gChest.IsEmpty)
                + "（期望 true/true ⇒ ⛔ 未被消费）"
                + " ｜金=" + gGold0 + "→" + Gold() + " 仓=" + gStores0 + "→" + PlayerStoresText()
                + "（期望**不变** ⇒ ⛔ 无独立入账口）");
            int assignedAfter = AssignedWorkers(gChest);
            for (int i = 0; i < 90; i++) yield return null;
            assignedAfter = Mathf.Max(assignedAfter, AssignedWorkers(gChest));
            Log("§G ⭐ 立案读数：点后 90 帧内在册搬运工人=" + assignedAfter + "（期望 ≥1 ⇒ 立案成立 · 工人来搬）"
                + " ｜箱余=" + (gChest != null ? CostText(gChest.contents) : "已空/销毁"));
            int kf0 = Time.frameCount; float krt0 = Time.realtimeSinceStartup;
            while (gChest != null && !gChest.IsEmpty && Time.frameCount - kf0 < 4000 && Time.realtimeSinceStartup - krt0 < 150f)
            { yield return null; KeepAlive(gChest); }   // ⛔ 观测窗保活
            yield return WaitDelivered(0);   // 等最后一批搬完
            Log("§G 到账后：金=" + gGold0 + "→" + Gold() + " 仓=" + gStores0 + "→" + PlayerStoresText()
                + " ｜箱余=" + (gChest != null ? CostText(gChest.contents) : "已空）")
                + "\n§G 全账=" + LedgerText());
        }

        // ================= §H 判据3：无主先到先得（AI 工人可搬）=================
        Log("");
        Log("## §H 判据3「无主先到先得：AI 工人可搬（09 §9.8 :391）」");
        Log("§H 鉴别力：若 `SourceKingdom` 未改（非 Building 源仍 return 0=玩家池）⇒ 派工要求 idleKingdom==0 "
            + "⇒ **AI 工人永不被匹配** ⇒ 本列应读「AI 工人 state=None、在册工人=0」");
        int aiKingdom = FirstAiKingdomId();
        var hCell = FarCell(anchorCell, 24);
        bool aiSpawned = TestFixtureApi.SpawnFixtureUnitForDiag(Occupation.Worker, hCell, aiKingdom, 0);
        var aiUnit = FindUnitAt(hCell, aiKingdom);
        Log("§H AI 前置：kingdomId=" + aiKingdom + " spawn=" + aiSpawned + " npcId=" + (aiUnit != null ? aiUnit.npcId : -1)
            + " @" + hCell.x + "," + hCell.y + "（远离玩家工作中心）");
        var hChest = SpawnChestAt(NearCell(hCell, 1), ResourceList.Of(new ResourceAmount(ResourceType.Gold, 1)), Faction.None);
        if (hChest == null) Log("❌ §H 落箱失败 ⇒ 中止");
        else
        {
            int aiId = aiUnit != null ? aiUnit.npcId : -1;
            TaskState st0 = aiId > 0 && TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(aiId) : TaskState.None;
            for (int i = 0; i < 200; i++) yield return null;
            TaskState st1 = aiId > 0 && TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(aiId) : TaskState.None;
            int aiGoldMid = AiGold(aiKingdom);
            for (int i = 0; i < 1800; i++)   // 等 AI 交付落地（携带清空/回空闲）
            {
                TaskState cur = aiId > 0 && TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(aiId) : TaskState.None;
                if (i > 60 && (cur == TaskState.None || CarryingCount(aiKingdom) == 0)) break;
                yield return null;
                KeepAlive(hChest);   // ⛔ 观测窗保活
            }
            Log("§H ⭐ 读数：AI 工人 state " + st0 + "→" + st1 + "（期望 ≠ None ⇒ AI 工人被派）"
                + " 终态=" + (aiId > 0 && TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(aiId).ToString() : "—")
                + " AI 台账金 " + aiGoldMid + "→" + AiGold(aiKingdom)
                + " ｜该箱在册搬运工人=" + AssignedWorkers(hChest)
                + " ｜箱余=" + (hChest != null ? CostText(hChest.contents) : "已空/销毁")
                + " ｜AI 国台账：金=" + AiGold(aiKingdom)
                + "（若 AI 把战利品搬回 ⇒ 入 AI 台账 · §9.8「AI 会来抢你的战利品」）");
        }

        Finish();
    }

    // ======================= 工具 =======================

    private static int Day() => TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
    private static int Gold() => RulerController.Instance != null ? RulerController.Instance.Gold : -1;
    private static int ChestTotal() => ChestManager.HasInstance ? ChestManager.Instance.Count : -1;

    private static int CountUnits(int kingdomId)
    {
        if (UnitRegistry.Instance == null) return -1;
        int n = 0;
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        for (int i = 0; i < us.Count; i++) if (us[i] != null && us[i].IsAlive && us[i].kingdomId == kingdomId) n++;
        return n;
    }

    private static UnitController FindUnitAt(Vector2Int cell, int kingdomId)
    {
        if (UnitRegistry.Instance == null) return null;
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        Vector2 c = CellCenter(cell);
        float best = float.MaxValue; UnitController found = null;
        for (int i = 0; i < us.Count; i++)
        {
            var u = us[i];
            if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
            float d = Vector2.Distance(u.transform.position, c);
            if (d < best) { best = d; found = u; }
        }
        return found;
    }

    private static int FirstAiKingdomId()
    {
        var reg = KingdomRegistry.Instance;
        if (reg != null)
        {
            var all = reg.GetAll();
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].id > 0) return all[i].id;
        }
        return 1;   // 无 AI 王国在册 ⇒ 仍用 id=1 测路由（台账兜底＝丢弃路径，另行说明）
    }

    private static int AiGold(int kingdomId)
    {
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        return k != null ? k.GetResourceValue(ResourceType.Gold) : -1;
    }

    private static int AssignedWorkers(ITaskSource src)
    {
        var comp = src as Component;                 // ⚠️ 走 Unity 的 ==（销毁即 fake-null）⇒ 返回 -1
        if (comp == null || !TaskScheduler.HasInstance) return -1;
        return TaskScheduler.Instance.CountAssignedWorkers(src);
    }

    /// <summary>工人面诊断（空闲／携带／距工作中心）：判定搬运未发生的**根因**（⛔ 非形容词）。</summary>
    private static string WorkersDiag(int kingdomId, Vector2Int center)
    {
        if (UnitRegistry.Instance == null) return "";
        int total = 0, idle = 0, carrying = 0;
        float minDist = float.MaxValue;
        Vector2 c = CellCenter(center);
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        for (int i = 0; i < us.Count; i++)
        {
            var u = us[i];
            if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
            total++;
            var st = TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(u.npcId) : TaskState.None;
            if (st == TaskState.None) idle++;
            var inv = u.GetComponent<WorkerInventory>();
            if (inv != null && !inv.IsEmpty) carrying++;
            float d = Vector2.Distance(u.transform.position, c);
            if (d < minDist) minDist = d;
        }
        return " ｜工人诊断：k" + kingdomId + " 总数=" + total + " 空闲=" + idle + " 携带中=" + carrying
            + " 最近距=" + (minDist == float.MaxValue ? "—" : minDist.ToString("F1"));
    }

    private static Vector2 CellCenter(Vector2Int cell)
    {
        var g = GridSystem.Instance;
        if (g != null) return g.CoordToWorld(new GridCoord(cell.x, cell.y));
        return new Vector2(cell.x, cell.y);
    }

    private static Vector2Int CellOfWorld(Vector3 world)
    {
        var g = GridSystem.Instance;
        if (g != null)
        {
            var sub = g.WorldToSubCoord(world);
            if (sub.HasValue)
            {
                var c = g.SubToCell(sub.Value);
                return new Vector2Int(c.x, c.y);
            }
        }
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
    }

    private static string Vec(Vector3 v) => "(" + v.x.ToString("F1") + "," + v.y.ToString("F1") + ")";

    private static BuildingDef DefOf(string id) => BuildingFactory.FindDefById(id);

    private static string CostText(ResourceList l)
    {
        if (l.items == null || l.items.Length == 0) return "{}";
        var sb = new StringBuilder("{");
        for (int i = 0; i < l.items.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append(l.items[i].type).Append(":").Append(l.items[i].amount);
        }
        return sb.Append("}").ToString();
    }

    private static bool SameList(ResourceList a, ResourceList b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.items.Length; i++)
            if (a.items[i].amount != b.Get(a.items[i].type)) return false;
        return true;
    }

    private static int SpaceOf(ResourceList pack)
    {
        int sum = 0;
        if (pack.items != null)
            for (int i = 0; i < pack.items.Length; i++)
            {
                int amt = pack.items[i].amount;
                if (amt > 0) sum += amt * ResourceCatalog.VolumeOf(pack.items[i].type);
            }
        return sum;
    }

    /// <summary>玩家国（0）**各仓**某资源合计（含国库容器 · ⛔ 排除箱容器）。</summary>
    private static int PlayerStoreAmount(ResourceType t)
    {
        int sum = 0;
        var all = Object.FindObjectsOfType<StorageComponent>();
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || s.GetComponent<ChestEntity>() != null) continue;   // ⛔ 箱容器不计
            var pb = s.GetComponentInParent<Building>();
            if (pb == null || pb.kingdomId != 0) continue;
            sum += s.GetAmount(t);
        }
        return sum;
    }

    /// <summary>玩家国到账面读数（木/石/金 ＋ Gold 字段）。</summary>
    private static string PlayerStoresText()
    {
        return "{Wood:" + PlayerStoreAmount(ResourceType.Wood)
            + ",Stone:" + PlayerStoreAmount(ResourceType.Stone)
            + ",Gold(字段):" + Gold()
            + ",Vault(Wood):" + (TreasureVault.Instance != null ? TreasureVault.Instance.GetAmount(ResourceType.Wood) : -1)
            + ",探针仓(Wood:" + (_probeStore != null ? _probeStore.GetAmount(ResourceType.Wood) : -1)
            + "/Stone:" + (_probeStore != null ? _probeStore.GetAmount(ResourceType.Stone) : -1) + ")"
            + "}";
    }

    private static StorageComponent FindPlayerStore(ResourceType t)
    {
        var all = Object.FindObjectsOfType<StorageComponent>();
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || s.GetComponent<ChestEntity>() != null) continue;
            var pb = s.GetComponentInParent<Building>();
            if (pb == null || pb.kingdomId != 0) continue;
            if (s.Accepts(t)) return s;
        }
        return null;
    }

    private static Building FindPlayerCastle()
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null) return null;
        var all = reg.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b != null && b.kingdomId == 0 && b.def != null && b.def.sourceType == BuildingType.CastleCore) return b;
        }
        return null;
    }

    /// <summary>
    /// **全账读数**（判据2「逐条目到账」的完整账面 · 箱为**无主源** ⇒ 可能被任一王国先到先得）：
    /// ① 各国仓（按 `KingdomOf` 分国 · ⛔ 排除箱容器）；② AI 台账（`KingdomState`）；③ 箱内合计；④ 在途携带；⑤ 玩家金字段。
    /// ⇒ 「未落玩家账」时可据此判读去向（入他国账 ／ 仍在箱 ／ 仍在途），⛔ 不用形容词。
    /// </summary>
    private static string LedgerText()
    {
        var sb = new StringBuilder();
        var byK = new SortedDictionary<int, int[]>();
        var all = Object.FindObjectsOfType<StorageComponent>();
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || s.GetComponent<ChestEntity>() != null) continue;   // ⛔ 箱容器不计
            var pb = s.GetComponentInParent<Building>();
            int k = pb != null ? pb.kingdomId : -1;
            if (!byK.TryGetValue(k, out var arr)) { arr = new int[3]; byK[k] = arr; }
            arr[0] += s.GetAmount(ResourceType.Wood);
            arr[1] += s.GetAmount(ResourceType.Stone);
            arr[2] += s.GetAmount(ResourceType.Gold);
        }
        foreach (var kv in byK)
            sb.Append("k").Append(kv.Key).Append(":W").Append(kv.Value[0]).Append("/S").Append(kv.Value[1])
              .Append("/G").Append(kv.Value[2]).Append(' ');
        var reg = KingdomRegistry.Instance;
        if (reg != null)
        {
            var ks = reg.GetAll();
            for (int i = 0; i < ks.Count; i++)
            {
                var k = ks[i];
                if (k == null || k.id <= 0) continue;
                sb.Append("台账k").Append(k.id).Append(":W").Append(k.GetResourceValue(ResourceType.Wood))
                  .Append("/S").Append(k.GetResourceValue(ResourceType.Stone))
                  .Append("/G").Append(k.GetResourceValue(ResourceType.Gold)).Append(' ');
            }
        }
        int cw = 0, cs = 0, cg = 0;
        if (ChestManager.HasInstance)
        {
            var chests = Object.FindObjectsOfType<ChestEntity>();
            for (int i = 0; i < chests.Length; i++)
            {
                var c = chests[i];
                if (c == null) continue;
                var p = c.contents;
                cw += p.Get(ResourceType.Wood); cs += p.Get(ResourceType.Stone); cg += p.Get(ResourceType.Gold);
            }
        }
        int uw = 0, us = 0, ug = 0;
        if (UnitRegistry.Instance != null)
        {
            var units = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || !u.IsAlive) continue;
                var inv = u.GetComponent<WorkerInventory>();
                if (inv == null || inv.IsEmpty) continue;
                if (inv.carriedType == ResourceType.Wood) uw += inv.carriedAmount;
                else if (inv.carriedType == ResourceType.Stone) us += inv.carriedAmount;
                else if (inv.carriedType == ResourceType.Gold) ug += inv.carriedAmount;
            }
        }
        sb.Append("｜箱内:W").Append(cw).Append("/S").Append(cs).Append("/G").Append(cg)
          .Append(" 在途:W").Append(uw).Append("/S").Append(us).Append("/G").Append(ug)
          .Append(" 玩家金字段:").Append(Gold());
        return sb.ToString();
    }

    /// <summary>注入「玩家国(0) 通用收货仓」（res · 容量500 · 挂主城 ⇒ `KingdomOf`=0）—— 承接到账读数用（⛔ 非任务源）。</summary>
    private static void InjectProbeStore(Vector2Int anchorCell)
    {
        var castle = FindPlayerCastle();
        if (castle == null)
        {
            Log("⚠️ 玩家主城不在场 ⇒ 探针收货仓未注入（材料到账读数将受「国库满⇒溢出装箱」回路影响）");
            return;
        }
        var pCell = NearCell(anchorCell, 2);
        _probeStoreGo = new GameObject("HH316_ProbeStore");
        _probeStoreGo.transform.SetParent(castle.transform, false);
        _probeStoreGo.transform.position = CellCenter(pCell);
        _probeStore = _probeStoreGo.AddComponent<StorageComponent>();
        _probeStore.SetDeclaredPaths(new[] { WarehousePaths.All });   // 通用仓（收 res ⇒ 材料可落）
        _probeStore.capacity = 500;
        WarehouseRegistry.Register(_probeStore);
        Log("§ 探针收货仓注入 @" + pCell.x + "," + pCell.y + "（res · 容量500 · 挂玩家主城 ⇒ KingdomOf=0 · 收尾清除）"
            + " ｜入库前 木=" + _probeStore.GetAmount(ResourceType.Wood) + " 石=" + _probeStore.GetAmount(ResourceType.Stone));
    }

    /// <summary>
    /// ⛔ **观测窗保活**（探针专用 · ⛔ 非生产改动）：测试段 15× 时标下「3 个游戏日（`expireDays`）」
    /// 仅约 1 分钟现实时间 ⇒ 搬运动作**尚未走完**箱就被**过期消亡**（§九 触发①）⇒ 判据读数会误判为「未到账」。
    /// 过期机制已由 §F 单独判定 ⇒ 本处把观测中的箱 `bornDay` 顶到当天，把该正交机制隔离出观测窗。
    /// </summary>
    private static void KeepAlive(ChestEntity chest)
    {
        if (chest != null) chest.bornDay = Day();
    }

    /// <summary>同格全部箱保活（§C 两箱面 · 逐帧刷新）。</summary>
    private static void KeepAliveAt(GridCoord c)
    {
        if (!ChestManager.HasInstance) return;
        var buf = new List<ChestEntity>();
        ChestManager.Instance.FillChestsInCellRect(new RectInt(c.x, c.y, 1, 1), buf);
        int day = Day();
        for (int i = 0; i < buf.Count; i++) if (buf[i] != null) buf[i].bornDay = day;
    }

    /// <summary>工人国携带中数量（判「最后一批是否已到账」——箱空瞬间最后一批仍在背包，照读会误判为未到账）。</summary>
    private static int CarryingCount(int kingdomId)
    {
        if (UnitRegistry.Instance == null) return 0;
        int n = 0;
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        for (int i = 0; i < us.Count; i++)
        {
            var u = us[i];
            if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
            var inv = u.GetComponent<WorkerInventory>();
            if (inv != null && !inv.IsEmpty) n++;
        }
        return n;
    }

    /// <summary>等「最后一批到账」：该国无携带中工人（≥30 帧起判，防取货前误判）；上限 frames 帧。</summary>
    private static IEnumerator WaitDelivered(int kingdomId, int frames = 1200)
    {
        for (int i = 0; i < frames; i++)
        {
            if (i > 30 && CarryingCount(kingdomId) == 0) yield break;
            yield return null;
        }
    }

    private static ChestEntity SpawnChestAt(Vector2Int c, ResourceList pack, Faction f)
        => ChestManager.HasInstance ? ChestManager.Instance.SpawnChest(new GridCoord(c.x, c.y), pack) : null;   // ⭐ M1-D/#57：去 faction 实参（f 形参保留兼容调用点）

    private static int ChestCountAt(GridCoord c)
        => ChestManager.HasInstance ? ChestManager.Instance.CountAt(c) : -1;

    private static int ChestCountAt(Vector2Int c) => ChestCountAt(new GridCoord(c.x, c.y));
    private static int ChestsNonEmptyAt(Vector2Int c) => ChestsNonEmptyAt(new GridCoord(c.x, c.y));
    private static string ChestsTextAt(Vector2Int c) => ChestsTextAt(new GridCoord(c.x, c.y));
    private static ChestEntity FindChestAt(Vector2Int c) => FindChestAt(new GridCoord(c.x, c.y));

    private static int ChestsNonEmptyAt(GridCoord c)
    {
        if (!ChestManager.HasInstance) return -1;
        var buf = new List<ChestEntity>();
        ChestManager.Instance.FillChestsInCellRect(new RectInt(c.x, c.y, 1, 1), buf);
        int n = 0;
        for (int i = 0; i < buf.Count; i++) if (buf[i] != null && !buf[i].IsEmpty) n++;
        return n;
    }

    private static string ChestsTextAt(GridCoord c)
    {
        if (!ChestManager.HasInstance) return "无 ChestManager";
        var buf = new List<ChestEntity>();
        ChestManager.Instance.FillChestsInCellRect(new RectInt(c.x, c.y, 1, 1), buf);
        if (buf.Count == 0) return "无箱";
        var sb = new StringBuilder();
        for (int i = 0; i < buf.Count; i++) sb.Append(CostText(buf[i].contents)).Append(" ");
        return sb.ToString().Trim();
    }

    /// <summary>在册工人一览（国/npc/态）—— ⭐ 判「谁抢到了无主箱」（§9.8 先到先得 ⇒ 可为**他国**工人 · ⛔ 不用形容词）。</summary>
    private static string AssignedUnitsText()
    {
        if (UnitRegistry.Instance == null || !TaskScheduler.HasInstance) return "—";
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        var sb = new StringBuilder();
        for (int i = 0; i < us.Count; i++)
        {
            var u = us[i];
            if (u == null || !u.IsAlive) continue;
            var st = TaskScheduler.Instance.GetWorkerState(u.npcId);
            if (st == TaskState.None) continue;
            sb.Append("k").Append(u.kingdomId).Append("/npc").Append(u.npcId).Append(':').Append(st).Append(' ');
        }
        return sb.Length == 0 ? "（无）" : sb.ToString().Trim();
    }

    /// <summary>同格各箱的在册搬运工人合计（判「有无派工」· ⛔ 非读数形容词）。</summary>
    private static int AssignedAt(GridCoord c)
    {
        if (!ChestManager.HasInstance || !TaskScheduler.HasInstance) return -1;
        var buf = new List<ChestEntity>();
        ChestManager.Instance.FillChestsInCellRect(new RectInt(c.x, c.y, 1, 1), buf);
        int n = 0;
        for (int i = 0; i < buf.Count; i++)
            if (buf[i] != null) n += TaskScheduler.Instance.CountAssignedWorkers(buf[i]);
        return n;
    }

    private static ChestEntity FindChestAt(GridCoord c)
    {
        if (!ChestManager.HasInstance) return null;
        var buf = new List<ChestEntity>();
        ChestManager.Instance.FillChestsInCellRect(new RectInt(c.x, c.y, 1, 1), buf);
        for (int i = 0; i < buf.Count; i++) if (buf[i] != null && !buf[i].IsEmpty) return buf[i];
        return null;
    }

    private static Building MakeAt(BuildingDef def, Vector2Int cell, int kingdomId, bool playerBuilt, BuildingState st)
    {
        if (def == null || BuildingFactory.Instance == null) return null;
        var coord = new GridCoord(cell.x, cell.y);
        var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1,
                                def.footprint.y > 0 ? def.footprint.y : 1);
        var world = GridSystem.FootprintCenterWorld(coord, fp, Vector3.zero);
        bool ok = BuildingFactory.Instance.CreateBuildingInstance(
            def, def.sourceType, coord, fp, world,
            isPlayerBuilt: playerBuilt, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: st, kingdomId: kingdomId);
        if (!ok) { Log("❌ 直建失败：" + def.id + " @" + cell.x + "," + cell.y); return null; }
        return BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(coord) : null;
    }

    /// <summary>确定性找一块空的可放置格（环形外扩 · ⛔ 不用 Random；⛔ 跳过已有箱子的格）。</summary>
    private static Vector2Int FreeCell(BuildingDef def, Vector2Int center)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) return center;
        int w = def != null && def.footprint.x > 0 ? def.footprint.x : 1;
        int h = def != null && def.footprint.y > 0 ? def.footprint.y : 1;
        for (int r = 2; r <= 24; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = MapGenRules.NearestWalkable(map, center.x + dx, center.y + dy);
                    var gc = new GridCoord(c.x, c.y);
                    if (ChestCountAt(gc) > 0) continue;                 // ⛔ 避开已有箱子（读数洁净）
                    if (PlacementValidator.ValidateFootprintClear(gc, w, h)) return c;
                }
            }
        }
        return center;
    }

    /// <summary>距 center 最近的可走格（确定性环扫；用于落箱点）。</summary>
    private static Vector2Int NearCell(Vector2Int center, int radius)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) return center;
        for (int r = radius; r <= radius + 4; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = MapGenRules.NearestWalkable(map, center.x + dx, center.y + dy);
                    if (ChestCountAt(new GridCoord(c.x, c.y)) == 0) return c;
                }
            }
        }
        return center;
    }

    /// <summary>远离工作中心的可走格（存档/过期样本用 ⇒ 工人走不到，读数不被搬运污染）。</summary>
    private static Vector2Int FarCell(Vector2Int center, int radius)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) return center;
        for (int r = radius; r <= radius + 6; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = MapGenRules.NearestWalkable(map, center.x + dx, center.y + dy);
                    if (ChestCountAt(new GridCoord(c.x, c.y)) == 0) return c;
                }
            }
        }
        return center;
    }

    // ======================= 收尾（L-32）＋ 落盘 =======================

    private static void Finish()
    {
        if (_origTaskTimeout > 0f && TaskScheduler.HasInstance) TaskScheduler.Instance.taskTimeout = _origTaskTimeout;   // 恢复取证参数
        if (_goldProbeGo != null) { Object.Destroy(_goldProbeGo); _goldProbeGo = null; }   // §D 探针仓清场
        if (_probeStoreGo != null)                                                          // §A 探针收货仓清场
        {
            if (_probeStore != null) WarehouseRegistry.Unregister(_probeStore);
            Object.Destroy(_probeStoreGo);
            _probeStoreGo = null; _probeStore = null;
        }
        Time.timeScale = 0f;                                     // L-32 条文3：真暂停
        Log("★ 收尾：Time.timeScale=0 ｜全局箱子数=" + ChestTotal() + "（⛔ 残留按 §E/§F 读数判）");
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();
        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + SLOT + "）→ 退 Play（L-32 条文1：禁留 1x 余留世界）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh316_u2");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh316_u2_smoke.txt");
            File.WriteAllText(stable, Sb.ToString());
            File.WriteAllText(Path.Combine(dir, "hh316_u2_smoke_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}