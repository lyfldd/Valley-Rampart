using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.315 · M1-C（建造／升级／修复／拆除 全走仓）同局冒烟（Editor-only）
//  口径真源：HH.315 任务书 §二（四件）＋ §四（判据 7 条）；09_资源与仓库.md
//           §16.1（建造流程 ①~④）／§16.2（拆除）／§16.3（空白：升级修复同构、拆除要时间与工人、
//           工地有 HP·打毁掉箱）／§12 差异 #48/#53/#54/#55/#56/#64
//
//  入口纪律（test-harness-first 铁律1）：**走正门** TestHarnessApi.EnterTestRun（禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → 封盘 → ExitTestRun → 退 Play。
//
//  ⚠️ 改前／改后对照的取证方法（裁决 §三 口径 #1「A/B 开关仅供验收取证 · ⛔ 不作长期双轨」）：
//     · **改后读数 ＝ 生产码实测**（本片落码后的真路径）。
//     · **改前读数 ＝ 探针内「旧口径复刻」**（公式逐行源自任务书 §二 件2/件3 的现状实读
//       `Building.cs:656/:667/:668/:672` 与 `:827`）—— ⛔ **不是调用生产码**
//       （旧码已按裁决 4「一并改／零调用即删」退役，同 build 内已无旧分支可跑）。
//     · 例外：判据 1 的「改前」用**遗留无参重载** `Building.StartConstructing()`
//       （直建/探针路径仍在役，语义 ＝ 旧「下单即扣费 ⇒ 立即开工」）。
//
//  段：
//    §A 起局（正门）＋ 玩家前置（仓/木/工人）
//    §B 判据1 建造走仓（真下单路径 BuildController.TryBuild）＋ 裁决1加严项「第 1 笔搬运命中工地仓」
//    §C 判据2 阈值拦截（直接 Deposit 定值 ＋ 跑局观测）
//    §D 判据3/5/6 拆除（全退／掉箱／耗时与工人门控）
//    §E 判据4 全资源摊（克隆 def 含 Metal/Ore ⇒ 退还 ＋ 修复费 双落点）
//    §F 判据7 存档（工地仓内容物／投料进度／拆除进度 往返）
//  落盘：Logs/hh315_m1c/hh315_m1c_smoke.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH315M1CSmoke
{
    public const string Tag = "HH315M1C";
    private const string Menu = "Valley/验证/HH315 M1-C 建造升级修复拆除全走仓 同局冒烟（正门进局）";
    private const int SEED = 31518;
    private const string SLOT = "hh315_m1c";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;
    private static float _origTaskTimeout = -1f;   // 取证用临时放宽的调度器超时（收尾恢复）

    [MenuItem(Menu, priority = 215)]
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
        Sb.AppendLine("# HH.315 · M1-C（建造／升级／修复／拆除 全走仓）同局冒烟（正门 EnterTestRun · seed=" + SEED + " 槽=" + SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Sb.AppendLine("# ⚠️ 改后＝生产码实测；改前＝探针内「旧口径复刻」（公式源自任务书 §二 现状实读 :656/:667/:668/:672/:827）");
        new GameObject("HH315M1CSmokeHost").AddComponent<Host>().Go(Run());
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string s) { Sb.AppendLine(s); Debug.Log("[" + Tag + "] " + s); }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = SLOT, kingdomName = "M1C冒烟"
        };
        Log("── §A 正门进局（seed=" + SEED + " Small/difficulty=2）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }
        Log("§A 起局：地图=" + map.width + "x" + map.height + " 日=" + Day()
            + " 建筑数=" + LiveBuildings() + " 倍率=" + Time.timeScale
            + "（＝WorldConfig.time.testSpeedMultiplier 测试段倍率）");

        var houseDef = DefOf("House");
        if (houseDef == null) { Log("❌ def 缺 House ⇒ 中止"); Finish(); yield break; }
        Log("§A 样本 def：House cost=" + CostText(houseDef.cost)
            + " ⇒ SiteNeedOf(去金)=" + CostText(Building.SiteNeedOf(houseDef.cost))
            + " GoldOnlyOf(取金)=" + CostText(Building.GoldOnlyOf(houseDef.cost))
            + " isDestructible=" + houseDef.isDestructible + " footprint=" + houseDef.footprint);
        var whDef = DefOf("Warehouse");
        if (whDef != null)
            Log("§A 金-A 口径直读（裁决 2）：Warehouse cost=" + CostText(whDef.cost)
                + " ⇒ SiteNeedOf(去金)=" + CostText(Building.SiteNeedOf(whDef.cost))
                + " ｜GoldOnlyOf=" + CostText(Building.GoldOnlyOf(whDef.cost))
                + " ｜⚠️ 纯金 3 栋（farm/quarry/market）SiteNeedOf 为空 ⇒ 即时开工（裁决已认可·⛔ 不作判据1样本）");

        // ---- 玩家前置：确保「可收木的仓 ＋ 木 ≥ 12」＋ 闲置工人 ----
        var anchorSub = GridSystem.Instance.WorldToSubCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        Vector2Int anchorCell;
        if (anchorSub.HasValue)
        {
            var ac = GridSystem.Instance.SubToCell(anchorSub.Value);
            anchorCell = new Vector2Int(ac.x, ac.y);
        }
        else anchorCell = MapGenRules.NearestWalkable(map, map.width / 2, map.height / 2);
        anchorCell = MapGenRules.NearestWalkable(map, anchorCell.x, anchorCell.y);

        var wh = FindPlayerWoodStore();
        if (wh == null)
        {
            var wb = MakeAt(whDef, FreeCell(whDef, anchorCell), 0, true, BuildingState.Active);
            wh = wb != null ? wb.GetComponent<StorageComponent>() : null;
            Log("§A 玩家无「可收木」仓 ⇒ 探针直建 Warehouse 补位：" + (wb != null));
        }
        int woodAdded = wh != null ? wh.Add(ResourceType.Wood, 60) : 0;
        Log("§A 玩家仓：" + (wh != null ? wh.gameObject.name + " 声明=[" + string.Join(",", wh.DeclaredPaths) + "]" : "**无**")
            + " ⇒ 注木 +" + woodAdded + " 现存量=" + (wh != null ? wh.GetAmount(ResourceType.Wood) : -1));

        int spawned = 0;
        for (int i = 0; i < 2; i++)
            if (TestFixtureApi.SpawnFixtureUnitForDiag(Occupation.Worker, anchorCell, 0, i)) spawned++;
        Log("§A 玩家工人补位：spawn=" + spawned + " ⇒ 玩家国工人数=" + CountUnits(0));

        // ⭐ 全部探针建筑就近「取料仓」落位（缩短「仓 → 工地」搬运距离 ⇒ 搬运可在观测窗内完成）
        int anchorX = anchorCell.x, anchorY = anchorCell.y;
        if (wh != null) anchorCell = CellOfWorld(wh.transform.position);
        Log("§A 工作中心格=" + anchorCell.x + "," + anchorCell.y + "（＝取料仓所在格；锚点格="
            + anchorX + "," + anchorY + "）" + WorkersDiag(0, anchorCell));

        // ⚠️ 取证用临时放宽 `TaskScheduler.taskTimeout`（⛔ 仅探针内存态，收尾恢复；⛔ 不改生产值）：
        //   测试段 15x 下「游戏时标」15× 于工人位移（PathFollower 按帧步进）⇒ 默认 30 游戏秒 ≈ 30 帧
        //   不足以让工人走到取料仓（实测 300 帧才推进 5.9 世界单位）⇒ 搬运必然超时放弃。
        _origTaskTimeout = TaskScheduler.HasInstance ? TaskScheduler.Instance.taskTimeout : -1f;
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.taskTimeout = 3600f;
        Log("§A 取证参数：TaskScheduler.taskTimeout " + _origTaskTimeout.ToString("F0") + " → 3600（探针临时 · 收尾恢复）");

        // ================= §B 判据1 建造走仓 =================
        Log("");
        Log("## §B 判据1「建造走仓」（真下单路径 BuildController.TryBuild · kingdomId=0 玩家）");
        Log("§B 鉴别力：若「下单即扣费」⇒ 工地仓恒空（SiteStore=null）＋ Constructing 立即开始（constructProgress>0）");

        GridCoord sub; GateOrientation orient; GridCoord origin;
        bool found = TryFindPlaceable(houseDef, anchorCell, out sub, out orient, out origin);
        Log("§B 可放置点解析=" + found + (found ? "（origin=" + origin.x + "," + origin.y + "）" : ""));
        if (!found) { Log("❌ 无合法放置点 ⇒ §B 中止（后续段继续）"); }
        else
        {
            int gold0 = Gold(); int wood0 = wh != null ? wh.GetAmount(ResourceType.Wood) : -1;
            bool ok = BuildController.Instance != null && BuildController.Instance.TryBuild(houseDef, sub, orient, 0);
            yield return null;
            var b = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(origin) : null;
            Log("§B TryBuild=" + ok + " 落点建筑=" + (b != null) + " 金 " + gold0 + "→" + Gold()
                + " 仓木 " + wood0 + "→" + (wh != null ? wh.GetAmount(ResourceType.Wood) : -1)
                + "（金-A：House 无金 ⇒ 金不变；非金**不下单即扣** ⇒ 仓木不变）");
            if (b != null)
            {
                var site = b.SiteStore;
                Log("§B ⭐ 改后实测（下单后第 1 帧）：state=" + b.state
                    + " SiteStore=" + (site != null) + " IsSiteAwaitingMaterials=" + b.IsSiteAwaitingMaterials
                    + " SiteNeed=" + CostText(b.SiteNeed)
                    + " 工地仓量=" + (site != null ? site.TotalCount : -1)
                    + " 缺口=" + (site != null ? site.TotalRemaining : -1)
                    + " totalInvested=" + b.totalInvested + " constructProgress=" + b.constructProgress.ToString("F4"));

                // ⭐ 裁决 1 加严项：下单后第 1 笔搬运命中工地仓（实测）
                Log("§B 搬运距离：工地 " + Vec(b.transform.position) + " ← 取料仓 "
                    + (wh != null ? Vec(wh.transform.position) : "无") + " ⇒ 直线 "
                    + (wh != null ? Vector2.Distance(b.transform.position, wh.transform.position).ToString("F1") : "—")
                    + " 世界单位");
                int f0 = Time.frameCount; float rt0 = Time.realtimeSinceStartup;
                int firstHaulFrame = -1; int hauledTotal = 0; int maxSiteSeen = 0;
                bool overThreshold = false; int needWood = site != null ? site.NeedOf(ResourceType.Wood) : 0;
                int diagTick = 0;
                while (b != null && site != null && b.IsSiteAwaitingMaterials
                       && Time.frameCount - f0 < 3600 && Time.realtimeSinceStartup - rt0 < 120f)
                {
                    yield return null;
                    if (b == null || site == null) break;
                    int cur = site.GetAmount(ResourceType.Wood);
                    if (cur > maxSiteSeen) maxSiteSeen = cur;
                    if (cur > needWood) overThreshold = true;
                    if (b.totalInvested > 0 && firstHaulFrame < 0)
                    {
                        firstHaulFrame = Time.frameCount - f0;
                        hauledTotal = b.totalInvested;
                    }
                    if (++diagTick % 300 == 0)
                        Log("§B …诊断 帧=" + (Time.frameCount - f0) + " totalInvested=" + b.totalInvested
                            + " 工地仓量=" + cur + "/" + needWood
                            + " 在册搬料工人=" + AssignedWorkers(site) + WorkersDiag(0, anchorCell));
                }
                Log("§B ⭐ 裁决1加严项 —— 第 1 笔搬运命中工地仓：帧=" + firstHaulFrame
                    + "（下单后第 " + (firstHaulFrame >= 0 ? firstHaulFrame.ToString() : "—") + " 帧）"
                    + " 该笔后 totalInvested=" + hauledTotal
                    + " ｜料齐=" + (b != null ? (!b.IsSiteAwaitingMaterials).ToString() : "—")
                    + " ｜观测窗内工地仓峰值=" + maxSiteSeen + "/" + needWood
                    + "（料齐瞬间即清空转本体 ⇒ 以 totalInvested 为不清空观测面）");
                Log("§B 判据1 改后读数：SiteStore 非空=" + (site != null)
                    + " 投料前 totalInvested=0 ⇒ 搬运后=" + hauledTotal
                    + " ｜判据2 旁证：工地仓量是否超配方=" + overThreshold);
                if (b != null && !b.IsSiteAwaitingMaterials)
                {
                    int cf = Time.frameCount; float crt = Time.realtimeSinceStartup;
                    while (b != null && b.state == BuildingState.Constructing
                           && Time.frameCount - cf < 2400 && Time.realtimeSinceStartup - crt < 60f) yield return null;
                    Log("§B 料齐后开工推进 ⇒ state=" + (b != null ? b.state.ToString() : "已销毁")
                        + " constructProgress=" + (b != null ? b.constructProgress.ToString("F4") : "—"));
                }
            }

            // 改前对照：遗留无参重载 ＝ 旧「立即开工」语义
            var legacy = MakeAt(houseDef, FreeCell(houseDef, anchorCell), 0, true, BuildingState.Active);
            if (legacy != null)
            {
                legacy.StartConstructing();   // ⛔ 无投料（旧行为路径 · 直建/探针仍在役）
                yield return null;
                Log("§B ⭐ 改前对照（遗留无参 StartConstructing()＝旧「下单即扣费⇒立即开工」）："
                    + "SiteStore=" + (legacy.SiteStore != null)
                    + " IsSiteAwaitingMaterials=" + legacy.IsSiteAwaitingMaterials
                    + " constructProgress=" + legacy.constructProgress.ToString("F4")
                    + "（旧行为：无工地仓 ⇒ 恒空、进度立即推进）");
                legacy.Die(DeathCause.Demolished);
            }
        }

        // ================= §C 判据2 阈值拦截 =================
        Log("");
        Log("## §C 判据2「阈值拦截（够阈值即停 · ⛔ 不多搬）」");
        Log("§C 鉴别力：无拦截 ⇒ 工地仓量 > 配方量");
        var c1 = MakeAt(houseDef, FreeCell(houseDef, anchorCell), 0, true, BuildingState.Active);
        if (c1 != null)
        {
            c1.StartConstructing(houseDef.cost);
            var s1 = c1.SiteStore;
            if (s1 == null) Log("❌ 工地仓未创建 ⇒ §C 中止");
            else
            {
                int need = s1.NeedOf(ResourceType.Wood);
                int a1 = s1.Deposit(ResourceType.Wood, 3);      // 未到阈值 ⇒ 期望 3
                int m1 = s1.GetAmount(ResourceType.Wood);       // 期望 3
                int a2 = s1.Deposit(ResourceType.Wood, 999);    // 只收缺口 ⇒ 期望 need-3
                int m2 = s1.GetAmount(ResourceType.Wood);       // 料齐瞬间已清空 ⇒ 0（材料转本体）
                int a3 = s1.Deposit(ResourceType.Wood, 999);    // 工地已关 ⇒ 期望 0
                Log("§C 直测（need(Wood)=" + need + "）：Deposit(3)=" + a1 + "（期望 3）⇒ 量=" + m1
                    + "；Deposit(999)=" + a2 + "（期望 " + (need - 3) + "＝只收缺口）⇒ 料齐清空后量=" + m2
                    + "；再 Deposit(999)=" + a3 + "（期望 0＝工地已关拒收）"
                    + " ⇒ 是否超配方=" + (Mathf.Max(m1, m2) > need));
            }
            c1.Die(DeathCause.Demolished);
        }
        var c2 = MakeAt(whDef, FreeCell(whDef, anchorCell), 0, true, BuildingState.Active);
        if (c2 != null)
        {
            c2.StartConstructing(whDef.cost);   // 金4 石4 ⇒ 去金后 need={Stone:4}
            var s2 = c2.SiteStore;
            Log("§C 多资源样本（Warehouse 金4/石4）：SiteNeed=" + CostText(c2.SiteNeed)
                + "（金不入工地仓 · 金-A）⇒ Deposit(Stone,999) 实收=" + (s2 != null ? s2.Deposit(ResourceType.Stone, 999) : -1)
                + "（期望 4）");
            c2.Die(DeathCause.Demolished);
        }

        // ================= §D 判据3/5/6 拆除 =================
        Log("");
        Log("## §D 判据3「拆除全退」＋ 判据5「退还掉箱」＋ 判据6「拆除耗时与工人」");
        Log("§D 鉴别力：旧口径 ⇒ 半血房退还 = 满血 × (hp/maxHp) < 满血；国库即时增加且无箱；拆除同帧完成");
        var d1 = MakeAt(houseDef, FreeCell(houseDef, anchorCell), 0, true, BuildingState.Active);
        if (d1 != null)
        {
            var d1coord = d1.coord;   // ⚠️ 先快照：拆除后 d1 已销毁，取 coord 会抛 MissingReference
            d1.hp = Mathf.Max(1, d1.maxHp / 2);
            Log("§D 样本：House @(" + d1coord.x + "," + d1coord.y + ") hp=" + d1.hp + "/" + d1.maxHp
                + " CanDemolish=" + d1.CanDemolish + " totalInvested=" + d1.totalInvested
                + " DemolishDuration()=" + d1.DemolishDuration().ToString("F2") + "s");

            int goldBefore = Gold();
            string vaultBefore = VaultText(houseDef.cost);
            int chestBefore = ChestCountAt(d1coord);
            int investedD1 = d1.totalInvested > 0 ? d1.totalInvested : houseDef.cost.TotalCount;
            var legacyPack = LegacyRefundPack(houseDef, investedD1, (float)d1.hp / Mathf.Max(1, d1.maxHp));

            d1.Demolish();
            Log("§D 拆除入口（改后·同帧）：IsDemolishing=" + d1.IsDemolishing + " DemolishProgress="
                + d1.DemolishProgress.ToString("F4") + " state=" + d1.state
                + " ｜在册工人=" + AssignedWorkers(d1));

            // 判据6：Demolish() → 真拆 的帧数/游戏秒数（⛔ 非「同帧完成」）
            int df0 = Time.frameCount; float drt0 = Time.realtimeSinceStartup;
            float dtime0 = Time.time;
            while (d1 != null && d1.IsDemolishing && Time.frameCount - df0 < 6000
                   && Time.realtimeSinceStartup - drt0 < 120f) yield return null;
            int frames = Time.frameCount - df0; float realSec = Time.realtimeSinceStartup - drt0;
            float gameSec = Time.time - dtime0;
            Log("§D 判据6 改后读数：Demolish() → 真拆 耗 " + frames + " 帧 / " + gameSec.ToString("F2")
                + " 游戏秒 / " + realSec.ToString("F2") + " 真实秒（配置 DemolishDuration=6.00s @BuildConfig.demolishBaseSeconds，"
                + "测试段 deltaTime=" + Time.deltaTime.ToString("F2") + "s/帧）；"
                + "改前（旧口径 · 已退役 · 任务书 §二 件4 现状实读「无耗时字段 · 瞬时」）= 同帧完成（0 帧）");

            // 判据6 门控（确定性）：kingdomId=99（无工人的国）⇒ ⛔ 无工人到场 ⇒ 进度恒 0
            var gate = MakeAt(houseDef, FreeCell(houseDef, anchorCell), 0, true, BuildingState.Active);
            if (gate != null)
            {
                gate.kingdomId = 99;
                gate.Demolish();
                float gp0 = gate.DemolishProgress;
                for (int i = 0; i < 60; i++) yield return null;
                Log("§D 判据6 门控读数（kingdomId=99 无工人）：60 帧内 进度 " + gp0.ToString("F4")
                    + "→" + gate.DemolishProgress.ToString("F4") + " ｜在册工人 " + AssignedWorkers(gate)
                    + "（期望：进度不动 ⇒ 「需工人到场」门控成立）");
                gate.Die(DeathCause.Demolished);
            }

            int chestAfter = ChestCountAt(d1coord);
            string pack = ChestTextAt(d1coord);
            Log("§D 判据5 改后读数：国库金 " + goldBefore + "→" + Gold()
                + " ｜国库非金 " + vaultBefore + "→" + VaultText(houseDef.cost)
                + "（期望**不变**＝⛔ 国库不即时增加）");
            Log("§D 判据5 掉箱：原地箱子数 " + chestBefore + "→" + chestAfter
                + " 箱内容=" + pack + "（期望 ≥1 箱）");
            Log("§D 判据3 改后读数（全退）：掉落退还 = " + pack
                + " ｜投入基数 invested=" + investedD1 + "（＝def.cost.TotalCount 兜底）");
            Log("§D 判据3 改前复刻（旧口径 · 四资源摊 ＋ hp/maxHp 比例 ＋ RulerController.Refund 直入国库）= "
                + CostText(legacyPack) + "（" + legacyPack.TotalCount + " 件）"
                + " ⇒ 对比改后＝全量退还（⛔ 无 hp/maxHp 折扣）");
        }

        // ================= §E 判据4 全资源摊 =================
        Log("");
        Log("## §E 判据4「全资源摊（含 Metal/Ore）」—— 现有 40 栋 cost 全无 Metal ⇒ 零鉴别力 ⇒ 探针克隆 def");
        Log("§E 鉴别力：旧口径 :667 只摊 金/石/木/粮 ⇒ Metal/Ore 永不出现在**退还**列表；:827 旧口径含 Metal 但**不含 Ore**");
        var probeDef = UnityEngine.Object.Instantiate(houseDef);
        probeDef.name = "House_M1C_Probe";
        probeDef.cost = ResourceList.Of(
            new ResourceAmount(ResourceType.Wood, 2),
            new ResourceAmount(ResourceType.Metal, 2),
            new ResourceAmount(ResourceType.Ore, 2));
        Log("§E 探针 def.cost=" + CostText(probeDef.cost) + "（木2＋铁2＋矿2）");
        var e1 = MakeAt(probeDef, FreeCell(probeDef, anchorCell), 0, true, BuildingState.Active);
        if (e1 != null)
        {
            var e1coord = e1.coord;   // ⚠️ 先快照（拆除后 e1 已销毁）
            int investedE1 = e1.totalInvested > 0 ? e1.totalInvested : probeDef.cost.TotalCount;
            var newRepair = e1.GetRepairCost();
            var oldRepair = LegacyRepairCost(probeDef, investedE1);
            Log("§E 修复费（:827 同族第三落点）：改后=" + CostText(newRepair) + " ｜改前复刻=" + CostText(oldRepair)
                + " ⇒ Ore 是否出现：改后=" + (newRepair.Get(ResourceType.Ore) > 0)
                + " 改前=" + (oldRepair.Get(ResourceType.Ore) > 0));

            var newPackRef = LegacyRefundPack(probeDef, investedE1, 1f);   // 与改后同输入 ⇒ 供对照（旧公式）
            int cb = ChestCountAt(e1coord);
            e1.Demolish();
            int ef0 = Time.frameCount; float ert0 = Time.realtimeSinceStartup;
            while (e1 != null && e1.IsDemolishing && Time.frameCount - ef0 < 6000
                   && Time.realtimeSinceStartup - ert0 < 120f) yield return null;
            Log("§E 退还（:667 第二落点）改后掉落箱=" + ChestTextAt(e1coord)
                + "（原地箱数 " + cb + "→" + ChestCountAt(e1coord) + "）");
            Log("§E 退还 改前复刻（旧 :667 过滤 金/石/木/粮）= " + CostText(newPackRef)
                + " ⇒ Metal/Ore 是否出现：改前=" + (newPackRef.Get(ResourceType.Metal) > 0)
                + "/" + (newPackRef.Get(ResourceType.Ore) > 0) + "（期望 false/false ＝ 旧口径鉴别力）");
        }
        else Log("❌ 克隆 def 直建失败 ⇒ §E 部分读数缺失");

        // ================= §F 判据7 存档 =================
        Log("");
        Log("## §F 判据7「存档：新档能存能读（工地仓内容物／投料进度一并）」");
        var f1 = MakeAt(houseDef, FreeCell(houseDef, anchorCell), 0, true, BuildingState.Active);
        if (f1 != null)
        {
            var f1coord = f1.coord;
            f1.StartConstructing(houseDef.cost);
            int dep = f1.SiteStore != null ? f1.SiteStore.Deposit(ResourceType.Wood, 1) : -1;   // 部分到料 ⇒ 仍待料
            Log("§F 存档前（投料态）：awaiting=" + f1.IsSiteAwaitingMaterials + " siteNeed=" + CostText(f1.SiteNeed)
                + " siteContents=" + CostText(f1.SiteStore != null ? f1.SiteStore.Contents : ResourceList.Empty)
                + "（本笔入仓 " + dep + "）totalInvested=" + f1.totalInvested);

            var f2 = MakeAt(houseDef, FreeCell(houseDef, anchorCell), 0, true, BuildingState.Active);
            float progBefore = -1f;
            GridCoord f2coord = default(GridCoord);
            if (f2 != null)
            {
                f2coord = f2.coord;
                f2.Demolish();
                // 先让真工人推一点进度（>0）⇒ 再切到「无人工国 99」冻结 ⇒ 读档可精确比对（⛔ 不受读档耗时影响）
                int sf0 = Time.frameCount; float srt0 = Time.realtimeSinceStartup;
                while (f2 != null && f2.IsDemolishing && f2.DemolishProgress < 0.2f
                       && Time.frameCount - sf0 < 1800 && Time.realtimeSinceStartup - srt0 < 60f) yield return null;
                if (f2 != null) f2.kingdomId = 99;
                yield return null; yield return null;
                progBefore = f2 != null ? f2.DemolishProgress : -1f;
                Log("§F 拆除态存档前：IsDemolishing=" + (f2 != null && f2.IsDemolishing)
                    + " DemolishProgress=" + progBefore.ToString("F4")
                    + "（已切 kingdomId=99 冻结 ⇒ 读档后可精确比对）");
            }

            bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
            bool loaded = SaveManager.Instance != null && SaveManager.Instance.Load(SLOT);
            yield return null; yield return null;
            Log("§F Save=" + saved + " Load=" + loaded);

            var f1b = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(f1coord) : null;
            Log("§F 读档后（投料态）：建筑=" + (f1b != null)
                + " awaiting=" + (f1b != null && f1b.IsSiteAwaitingMaterials)
                + " siteNeed=" + (f1b != null ? CostText(f1b.SiteNeed) : "—")
                + " siteContents=" + (f1b != null && f1b.SiteStore != null ? CostText(f1b.SiteStore.Contents) : "—")
                + " totalInvested=" + (f1b != null ? f1b.totalInvested : -1)
                + " ⇒ 往返一致=" + (f1b != null && f1b.IsSiteAwaitingMaterials
                    && f1b.SiteStore != null && f1b.SiteStore.GetAmount(ResourceType.Wood) == 1
                    && f1b.SiteNeed.Get(ResourceType.Wood) == houseDef.cost.Get(ResourceType.Wood)));
            var f2b = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(f2coord) : null;
            Log("§F 读档后（拆除态）：建筑=" + (f2b != null)
                + " IsDemolishing=" + (f2b != null && f2b.IsDemolishing)
                + " DemolishProgress=" + (f2b != null ? f2b.DemolishProgress.ToString("F4") : "—")
                + "（存档前 " + progBefore.ToString("F4") + "）");
        }

        Finish();
    }

    // ======================= 旧口径复刻（⛔ 非调用生产码：旧码已按裁决退役）=======================

    private static bool LegacyIsRefundResource(ResourceType t)
        => t == ResourceType.Gold || t == ResourceType.Stone || t == ResourceType.Wood || t == ResourceType.Food;

    private static int LegacySumCostOf(ResourceList cost, bool includeMetal)
    {
        if (cost.items == null) return 0;
        int sum = 0;
        for (int i = 0; i < cost.items.Length; i++)
        {
            var e = cost.items[i];
            if (LegacyIsRefundResource(e.type) || (includeMetal && e.type == ResourceType.Metal)) sum += e.amount;
        }
        return sum;
    }

    /// <summary>旧 `Demolish` 退还（任务书 §二 件2 现状实读 `:656` ratio × `:667` 四资源过滤 × `:668` 分摊 × `:672 Refund`）。</summary>
    private static ResourceList LegacyRefundPack(BuildingDef def, int invested, float hpRatio)
    {
        if (def == null || def.cost.items == null || def.cost.items.Length == 0) return ResourceList.Empty;
        int costSum = LegacySumCostOf(def.cost, includeMetal: false);
        int baseSum = Mathf.Max(1, costSum);
        int inv = invested > 0 ? invested : costSum;
        var pack = ResourceList.Empty;
        for (int i = 0; i < def.cost.items.Length; i++)
        {
            var e = def.cost.items[i];
            if (!LegacyIsRefundResource(e.type)) continue;
            int amount = Mathf.FloorToInt((float)inv * e.amount / baseSum);
            amount = Mathf.RoundToInt(amount * Mathf.Clamp01(hpRatio));   // RulerController.Refund(cost, ratio)
            if (amount > 0) pack = pack.Set(e.type, amount);
        }
        return pack;
    }

    /// <summary>旧 `GetRepairCost`（任务书 §二 件2 现状实读 `:827`：五资源过滤 ＝ 四资源 ＋ Metal）。</summary>
    private static ResourceList LegacyRepairCost(BuildingDef def, int invested)
    {
        if (def == null || def.cost.items == null || def.cost.items.Length == 0) return ResourceList.Empty;
        int costSum = LegacySumCostOf(def.cost, includeMetal: true);
        int inv = invested > 0 ? invested : costSum;
        if (inv <= 0) return ResourceList.Empty;
        float ratio = RepairConfig.Instance != null ? Mathf.Clamp01(RepairConfig.Instance.repairCostRatio) : 0.5f;
        int total = Mathf.Max(1, Mathf.RoundToInt(inv * ratio));
        int baseSum = Mathf.Max(1, costSum);
        var cost = ResourceList.Empty;
        for (int i = 0; i < def.cost.items.Length; i++)
        {
            var e = def.cost.items[i];
            if (!LegacyIsRefundResource(e.type) && e.type != ResourceType.Metal) continue;
            int amount = Mathf.RoundToInt((float)total * e.amount / baseSum);
            if (amount > 0) cost = cost.Set(e.type, amount);
        }
        return cost;
    }

    // ======================= 工具 =======================

    private static int Day() => TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;

    private static int Gold() => RulerController.Instance != null ? RulerController.Instance.Gold : -1;

    private static int LiveBuildings()
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null) return -1;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++) if (reg.All[i] != null) n++;
        return n;
    }

    private static int CountUnits(int kingdomId)
    {
        if (UnitRegistry.Instance == null) return -1;
        int n = 0;
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        for (int i = 0; i < us.Count; i++) if (us[i] != null && us[i].IsAlive && us[i].kingdomId == kingdomId) n++;
        return n;
    }

    private static int AssignedWorkers(ITaskSource src)
    {
        var comp = src as Component;                 // ⚠️ 走 Unity 的 ==（销毁即 fake-null）⇒ 返回 -1
        if (comp == null || !TaskScheduler.HasInstance) return -1;
        return TaskScheduler.Instance.CountAssignedWorkers(src);
    }

    /// <summary>工人面诊断（空闲／携带／距工作中心）：用于判定搬料未发生的**根因**（⛔ 非形容词）。</summary>
    private static string WorkersDiag(int kingdomId, Vector2Int center)
    {
        if (UnitRegistry.Instance == null) return "";
        int total = 0, idle = 0, carrying = 0, carryingOther = 0;
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
            if (inv != null && !inv.IsEmpty)
            {
                carrying++;
                if (inv.carriedType != ResourceType.Wood) carryingOther++;
            }
            float d = Vector2.Distance(u.transform.position, c);
            if (d < minDist) minDist = d;
        }
        return " ｜工人诊断：k" + kingdomId + " 总数=" + total + " 空闲=" + idle
            + " 携带中=" + carrying + "（非木=" + carryingOther + "）"
            + " 最近距工作中心=" + (minDist == float.MaxValue ? "—" : minDist.ToString("F1"));
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

    private static string VaultText(ResourceList pack)
    {
        if (pack.items == null) return "{}";
        var sb = new StringBuilder("{");
        for (int i = 0; i < pack.items.Length; i++)
        {
            if (i > 0) sb.Append(",");
            var t = pack.items[i].type;
            int v = t == ResourceType.Gold ? Gold()
                : (TreasureVault.Instance != null ? TreasureVault.Instance.GetAmount(t) : -1);
            sb.Append(t).Append(":").Append(v);
        }
        return sb.Append("}").ToString();
    }

    private static StorageComponent FindPlayerWoodStore()
    {
        var list = WarehouseRegistry.GatherActive(0);
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i] as StorageComponent;
            if (s == null) continue;
            if (s.Accepts(ResourceType.Wood)) return s;
        }
        return null;
    }

    private static int ChestCountAt(GridCoord c)
        => ChestManager.HasInstance ? ChestManager.Instance.CountAt(c) : -1;

    private static string ChestTextAt(GridCoord c)
    {
        if (!ChestManager.HasInstance) return "无 ChestManager";
        var buf = new List<ChestEntity>();
        ChestManager.Instance.FillChestsInCellRect(new RectInt(c.x, c.y, 1, 1), buf);
        if (buf.Count == 0) return "无箱";
        var sb = new StringBuilder();
        for (int i = 0; i < buf.Count; i++) sb.Append(CostText(buf[i].contents)).Append(" ");
        return sb.ToString().Trim();
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

    /// <summary>确定性找一块空的可放置格（环形外扩 · ⛔ 不用 Random；⛔ 跳过已有箱子的格 ⇒ 掉箱读数不被污染）。</summary>
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
                    if (ChestCountAt(gc) > 0) continue;                 // ⛔ 避开已有箱子（掉箱读数洁净）
                    if (PlacementValidator.ValidateFootprintClear(gc, w, h)) return c;
                }
            }
        }
        return center;
    }

    /// <summary>找「玩家可下单」的合法放置点（同 BuildController.TryBuild 的校验口）。</summary>
    private static bool TryFindPlaceable(BuildingDef def, Vector2Int center, out GridCoord sub,
                                         out GateOrientation orient, out GridCoord origin)
    {
        sub = default(GridCoord); orient = GateOrientation.Horizontal; origin = default(GridCoord);
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (grid == null || map == null) return false;
        for (int r = 1; r <= 14; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = MapGenRules.NearestWalkable(map, center.x + dx, center.y + dy);
                    var s = grid.CellToSub(new GridCoord(c.x, c.y), 0, 0);
                    var chk = PlacementValidator.ValidatePlacement(def, s, GateOrientation.Horizontal, 0);
                    if (!chk.ok) continue;
                    sub = s; orient = GateOrientation.Horizontal; origin = chk.snappedOrigin;
                    return true;
                }
            }
        }
        return false;
    }

    // ======================= 收尾（L-32）＋ 落盘 =======================

    private static void Finish()
    {
        if (_origTaskTimeout > 0f && TaskScheduler.HasInstance) TaskScheduler.Instance.taskTimeout = _origTaskTimeout;   // 恢复取证参数
        Time.timeScale = 0f;                                     // L-32 条文3：真暂停
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
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh315_m1c");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh315_m1c_smoke.txt");
            File.WriteAllText(stable, Sb.ToString());
            File.WriteAllText(Path.Combine(dir, "hh315_m1c_smoke_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}
