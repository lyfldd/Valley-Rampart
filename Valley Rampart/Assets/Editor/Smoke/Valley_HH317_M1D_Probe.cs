using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using static BuildingFactory;

// ============================================================================
//  HH.317 · M1-D「掉落与战利品」正门冒烟（Editor-only）
//  口径真源：HH.317 施工任务书 件1~件10 ＋ 判据 1~12（`D802`/`D803` 裁）。
//  入口纪律（test-harness-first 铁律1）：走正门 `TestHarnessApi.EnterTestRun`（⛔ 禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → 封盘 → ExitTestRun → 退 Play。
//
//  段（与判据一一对应）：
//    §B 判据1 Killer 链（生产事件直证）／判据2 D490／判据3 战利品来源（正＋空包负）／判据4 ×1.5 不落地／判据10 日志
//    §C 判据5 标签（不可掉仓 ⛔／可掉仓 ✅；盲区② 工地仓并入 §D②）
//    §D 判据6 #49 三分类（①工事被打毁掉箱 ②拆除分箱 ③EnterRuined 仓留存无箱-负向）
//    §E 判据7 EnforceCellLimit 洒落邻格（总量守恒）
//    §F 判据8 ownerFaction 恒 None／判据11 存档往返逐值
//    §G 判据9 无仓单位（怪物同型）不掉箱
//  判据12（回归：M1-C 冒烟 + 2_20B_M7）＝另跑，不进本探针。
//  落盘：Logs/hh317_m1d/hh317_m1d_smoke.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH317M1DProbe
{
    public const string Tag = "HH317M1D";
    private const string Menu = "Valley/验证/HH317 M1-D 掉落与战利品 正门冒烟（正门进局）";
    private const int SEED = 31721;
    private const string SLOT = "hh317_m1d";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;
    private static int _pass, _fail;
    private static readonly List<GameObject> _cleanup = new List<GameObject>();

    private static readonly List<UnitDiedEvent> _died = new List<UnitDiedEvent>();   // 判据1：生产事件直证
    private static readonly List<string> _orcLootLogs = new List<string>();          // 判据10：日志捕获

    [MenuItem(Menu, priority = 218)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 冒烟已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0; _pass = 0; _fail = 0;
        Sb.AppendLine("# HH.317 · M1-D「掉落与战利品」正门冒烟（seed=" + SEED + " 槽=" + SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Sb.AppendLine("# 口径：施工任务书 件1~件10 ＋ 判据 1~12（D802/D803 裁）");
        new GameObject("HH317M1DProbeHost").AddComponent<Host>().Go(Run());
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string s) { Sb.AppendLine(s); Debug.Log("[" + Tag + "] " + s); }

    private static void Check(bool ok, string name, string detail)
    {
        if (ok) _pass++; else _fail++;
        Log((ok ? "PASS " : "FAIL ") + name + " :: " + detail);
        if (!ok) Debug.LogError("[" + Tag + "][FAIL] " + name + " :: " + detail);
    }

    private static void OnDied(UnitDiedEvent e) => _died.Add(e);
    private static void OnLog(string msg, string stack, LogType type)
    {
        if (msg != null && msg.Contains("[OrcLoot]")) _orcLootLogs.Add(msg);
    }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = SLOT, kingdomName = "M1D探针"
        };
        Log("── §A 正门进局（seed=" + SEED + " Small/difficulty=2）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }
        EventBus.Subscribe<UnitDiedEvent>(OnDied);
        Application.logMessageReceived += OnLog;
        Log("§A 起局：地图=" + map.width + "x" + map.height + " 日=" + Day() + " 箱数=" + ChestTotal() + " 倍率=" + Time.timeScale);

        var ds = DamageSystem.Instance;
        if (ds == null) { Log("❌ DamageSystem 不在场 ⇒ 中止"); Finish(); yield break; }

        var anchorSub = GridSystem.Instance.WorldToSubCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        Vector2Int anchorCell;
        if (anchorSub.HasValue)
        {
            var ac = GridSystem.Instance.SubToCell(anchorSub.Value);
            anchorCell = new Vector2Int(ac.x, ac.y);
        }
        else anchorCell = MapGenRules.NearestWalkable(map, map.width / 2, map.height / 2);
        anchorCell = MapGenRules.NearestWalkable(map, anchorCell.x, anchorCell.y);

        // ================= §B 判据1/2/3/4/10 =================
        Log("");
        Log("## §B 判据1 Killer 链 ＋ 判据2 D490 ＋ 判据3 来源＝死者背包 ＋ 判据4 ×1.5 不落地 ＋ 判据10 日志");
        Log("§B 鉴别力声明：样本走生产入口 `DamageSystem.ApplyDamage(source,target,9999)`（⛔ 非直构事件 · L-51）——");
        Log("§B         改前 Killer 生产恒 null（D802 实证）⇒ 判据1 必红且战利品不落箱；改前内容＝凭空随机金 ⇒ 判据3/4 必红。");
        var k1 = SpawnUnitDirect(Occupation.Berserker, CellCenter(NearCell(anchorCell, 3)));
        var v1 = SpawnUnitDirect(Occupation.Warrior, CellCenter(NearCell(anchorCell, 4)));
        if (k1 == null || v1 == null) { Check(false, "§B 直构", "k1/v1 直构失败"); }
        else
        {
            _cleanup.Add(k1.gameObject);
            k1.raceId = RaceIds.Orc;                 // 材料强制兽人（玩家可能非兽人）
            v1.kingdomId = -1;                       // 材料隔离：死亡不进玩家幸福桶/王国口径
            var inv = v1.GetOrAddInventory();
            int stored = inv.TryStore(ResourceType.Wood, 7);
            var before1 = AllChests();
            int died0 = _died.Count, logs0 = _orcLootLogs.Count;
            ds.ApplyDamage(k1, v1, 9999);            // ⭐ 生产路径致死（→ TakeDamage(source) → Die → UnitDiedEvent）
            int chests1 = ChestTotal();

            // 判据1：生产事件 Killer 直证
            UnitDiedEvent? evt = null;
            for (int i = died0; i < _died.Count; i++)
                if (ReferenceEquals(_died[i].Unit, v1)) { evt = _died[i]; break; }
            bool killerOk = evt.HasValue && ReferenceEquals(evt.Value.Killer, k1);
            Check(killerOk, "判据1 Killer 链（生产事件直证）",
                "UnitDiedEvent.Killer " + (killerOk ? "== 致死者 k1(npc" + k1.npcId + ")" : "!= 致死者（" + (evt.HasValue ? (evt.Value.Killer == null ? "null" : "他人") : "无事件") + "）")
                + "｜本段事件数=" + (_died.Count - died0) + "｜鉴别力：改前生产恒 null");

            // 判据2：D490 狂战叠层
            int st = k1.Frenzy != null ? k1.Frenzy.Stacks : -1;
            Check(k1.Frenzy != null && st >= 1, "判据2 D490 狂战叠层（生产路径）", "Frenzy.Stacks=" + st + "（期望 ≥1 · 改前恒 0）");

            // 判据3（正例）：落箱内容＝死者背包逐值
            var nc = FindNewChest(before1);
            int got = nc != null ? nc.contents.Get(ResourceType.Wood) : -1;
            Check(nc != null && got == stored && stored == 7, "判据3 来源＝死者背包（正例）",
                "新箱内容 Wood=" + got + "（期望 " + stored + "）｜鉴别力：改前＝凭空随机金（与背包无关）");

            // 判据10：#60 日志按 killer 身份
            bool logNew = _orcLootLogs.Count > logs0;
            string lastLog = logNew ? _orcLootLogs[_orcLootLogs.Count - 1] : "（无）";
            bool idOk = logNew && lastLog.Contains("r" + k1.raceId) && lastLog.Contains("npc" + k1.npcId);
            Check(idOk, "判据10 #60 日志含 killer 身份", "最后一条=" + lastLog + "｜[OrcLoot] 关键字保留（七考观察锚）");

            // 判据3（负例）：空背包 ⇒ 不落箱
            var v2 = SpawnUnitDirect(Occupation.Warrior, CellCenter(NearCell(anchorCell, 5)));
            if (v2 != null)
            {
                v2.kingdomId = -1;
                v2.GetOrAddInventory();              // 有背包但空
                var before2 = AllChests();
                ds.ApplyDamage(k1, v2, 9999);
                Check(AllChests().Count == before2.Count && FindNewChest(before2) == null,
                    "判据3 负例：背包空 ⇒ 不落箱",
                    "箱数 " + before2.Count + "→" + AllChests().Count + "（期望不变 · 09 §九「仓非空才生成」）");
            }
            else Check(false, "§B 负例直构", "v2 失败");

            // 判据4：×1.5 本批不落地（死者含金 ⇒ 逐值原样、⛔ 无 ×1.5）
            var v3 = SpawnUnitDirect(Occupation.Warrior, CellCenter(NearCell(anchorCell, 6)));
            if (v3 != null)
            {
                v3.kingdomId = -1;
                var inv3 = v3.GetOrAddInventory();
                int g = inv3.TryStore(ResourceType.Gold, 10);
                var before3 = AllChests();
                ds.ApplyDamage(k1, v3, 9999);
                var nc3 = FindNewChest(before3);
                int gotG = nc3 != null ? nc3.contents.Get(ResourceType.Gold) : -1;
                Check(nc3 != null && gotG == g && g == 10, "判据4 ×1.5 本批不落地（预期：无 ×1.5 痕迹）",
                    "死者背包 Gold=" + g + " ⇒ 箱内容 Gold=" + gotG + "（⛔ 未乘 · 待 M1-E 金进仓后落地）");
            }
            else Check(false, "§B 判据4直构", "v3 失败");
        }

        // ================= §C 判据5 标签 =================
        Log("");
        Log("## §C 判据5 可掉落标签（形状1：`StorageComponent.droppable` · 默认可掉 · D803 裁）");
        var whDef = DefOf("Warehouse");
        var whA = MakeAt(whDef, FreeCell(whDef, anchorCell), 0, true, BuildingState.Active);
        if (whA != null)
        {
            var stA = GetOrAddProbeStore(whA);
            int a = stA.Add(ResourceType.Wood, 3);
            var beforeA = AllChests();
            whA.Die(DeathCause.Demolished);   // 拆除终态（构造法＝生产唯一终点 FinishDemolish；声明见报告）
            var ncA = FindNewChest(beforeA);
            int wA = ncA != null ? ncA.contents.Get(ResourceType.Wood) : -1;
            Check(ncA != null && wA == a && a == 3, "判据5 ✅ 可掉仓（默认 true）⇒ 掉箱",
                "箱内容 Wood=" + wA + "（期望 " + a + " · 默认可掉 = D803 裁）");
        }
        else Check(false, "§C 可掉仓建筑", "直建失败");
        var whB = MakeAt(whDef, FreeCell(whDef, anchorCell), 0, true, BuildingState.Active);
        if (whB != null)
        {
            var stB = GetOrAddProbeStore(whB);
            stB.droppable = false;             // ⭐ 不可掉落标签（形状1 第二维）
            int b = stB.Add(ResourceType.Wood, 5);
            var beforeB = AllChests();
            whB.Die(DeathCause.Demolished);
            Check(AllChests().Count == beforeB.Count, "判据5 ⛔ 不可掉仓（droppable=false）⇒ 不掉箱",
                "箱数 " + beforeB.Count + "→" + AllChests().Count + "（期望不变；内容 " + b + " 随销毁 · 人口仓/水仓未来落地走此口径）");
        }
        else Check(false, "§C 不可掉仓建筑", "直建失败");

        // ================= §D 判据6 #49 三分类 =================
        Log("");
        Log("## §D 判据6 #49 三分类（①工事被打毁 ②拆除分箱 ③EnterRuined 仓留存-负向）");
        // ① 工事被打毁 ⇒ 产出仓掉箱（wall 无本体仓 ⇒ 手动挂 · 标签默认可掉）
        var wallDef = DefOf("wall");
        var wall = MakeAt(wallDef, FreeCell(wallDef, anchorCell), 0, true, BuildingState.Active);
        if (wall != null)
        {
            Check(wall.IsFortification, "§D 前提：wall 为工事", "IsFortification=" + wall.IsFortification);
            var stW = GetOrAddProbeStore(wall);
            int w = stW.Add(ResourceType.Wood, 4);
            var beforeW = AllChests();
            wall.TakeDamage(9999, null);       // ⭐ 甲案签名（source=null）· 工事 ⇒ Die(Killed)
            var ncW = FindNewChest(beforeW);
            int wW = ncW != null ? ncW.contents.Get(ResourceType.Wood) : -1;
            Check(ncW != null && wW == w && w == 4, "判据6① 工事被打毁 ⇒ 产出仓掉箱",
                "箱内容 Wood=" + wW + "（期望 " + w + "）｜Q3：Die 路径掉箱");
        }
        else Check(false, "§D 工事建筑", "直建失败");

        // ② 拆除终态 ⇒ 分箱（工地仓箱 {3} ＋ 产出仓箱 {4} ＝ 2 箱 · 盲区② 工地仓纳管）
        var probeDef = Object.Instantiate(DefOf("House"));
        probeDef.name = "House_HH317_Probe";
        probeDef.cost = ResourceList.Of(new ResourceAmount(ResourceType.Wood, 400));   // 木 400 ⇒ 不可能料齐 ⇒ 工地仓必留内容
        var site = MakeAt(probeDef, FreeCell(probeDef, anchorCell), 0, true, BuildingState.Active);
        if (site != null)
        {
            site.StartConstructing(probeDef.cost);              // 投料态（工地仓注册）
            var siteStore = site.SiteStore;
            int dep = siteStore != null ? siteStore.Deposit(ResourceType.Wood, 3) : -1;
            var stS = GetOrAddProbeStore(site);                 // 本体仓（手动挂/已有）
            int sAdd = stS.Add(ResourceType.Wood, 4);
            var beforeS = AllChests();
            site.Die(DeathCause.Demolished);
            var news = NewChests(beforeS);
            int n3 = 0, n4 = 0, nSum = 0;
            for (int i = 0; i < news.Count; i++)
            {
                int wv = news[i] != null ? news[i].contents.Get(ResourceType.Wood) : 0;
                nSum += wv; if (wv == 3) n3++; if (wv == 4) n4++;
            }
            Check(news.Count == 2 && n3 == 1 && n4 == 1 && nSum == 7,
                "判据6② 拆除 ⇒ 分箱（工地仓 {3} ＋ 产出仓 {4}）",
                "新箱数=" + news.Count + " 明细 3×" + n3 + " ＋ 4×" + n4 + "（Σ=7）｜dep=" + dep + " sAdd=" + sAdd
                + "｜Q6 分箱＝一容器一箱；盲区② 工地仓显式纳管");
            Object.DestroyImmediate(probeDef);
        }
        else Check(false, "§D 投料态建筑", "直建失败");

        // ③ EnterRuined（非工事被打爆）⇒ 仓留存 ⛔ 无箱（负向）
        var hDef = DefOf("House");
        var hb = MakeAt(hDef, FreeCell(hDef, anchorCell), 0, true, BuildingState.Active);
        if (hb != null)
        {
            var stH = GetOrAddProbeStore(hb);
            int hAdd = stH.Add(ResourceType.Wood, 5);
            var beforeH = AllChests();
            hb.TakeDamage(9999, null);         // 非工事 ⇒ EnterRuined（Q3：生命周期未结束）
            bool ruined = hb.state == BuildingState.Ruined;
            int keep = stH.GetAmount(ResourceType.Wood);
            Check(ruined && AllChests().Count == beforeH.Count && keep == hAdd && hAdd == 5,
                "判据6③ EnterRuined ⇒ 仓留存 ⛔ 无箱（负向）",
                "state=" + hb.state + " 箱数 " + beforeH.Count + "→" + AllChests().Count + " 仓 Wood=" + keep + "（期望 5 留存 · Q3）");
            if (BuildingRegistry.Instance != null) BuildingRegistry.Instance.Unregister(hb);
            if (TaskScheduler.HasInstance) TaskScheduler.Instance.Unregister(hb);
            Object.DestroyImmediate(hb.gameObject);
        }
        else Check(false, "§D 废墟建筑", "直建失败");

        // ================= §E 判据7 EnforceCellLimit 洒落 =================
        Log("");
        Log("## §E 判据7 EnforceCellLimit ⇒ 洒落邻格（⛔ 不丢 · D802 Q6）");
        var far = FarCell(anchorCell, 25);
        for (int i = 0; i < 4; i++) SpawnChestAt(far, ResourceList.Of(new ResourceAmount(ResourceType.Wood, 1)));
        int at4 = ChestCountAt(far);
        int total0 = ChestTotal();
        int wood0 = WoodTotalInChests();
        var ring0 = RingChests(far, 1, 4);
        SpawnChestAt(far, ResourceList.Of(new ResourceAmount(ResourceType.Wood, 9)));   // 第 5 箱 ⇒ 触发驱逐 ⇒ 洒落
        int at5 = ChestCountAt(far);
        int total1 = ChestTotal();
        int wood1 = WoodTotalInChests();
        int ringAdd = RingChests(far, 1, 4).Count - ring0.Count;
        Check(at4 == 4 && at5 == 4 && total1 == total0 + 1 && wood1 == wood0 + 9 && ringAdd == 1,
            "判据7 单格上限 ⇒ 洒落邻格（总量守恒）",
            "落前 at=" + at4 + " ΣWood=" + wood0 + " 总箱=" + total0 + "｜落后 at=" + at5 + " ΣWood=" + wood1 + " 总箱=" + total1
            + " 环内新增=" + ringAdd + "（期望 at 恒4；Σ+9；环内 +1＝洒落箱 · ⛔ 不静默/不销毁）");

        // ================= §F 判据8 ownerFaction ＋ 判据11 存档往返 =================
        Log("");
        Log("## §F 判据8 ownerFaction 恒 None ＋ 判据11 存档往返逐值");
        var all = AllChests();
        int nonNone = 0;
        for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].ownerFaction != Faction.None) nonNone++;
        Check(all.Count > 0 && nonNone == 0, "判据8 SpawnChest 去参 ⇒ ownerFaction 恒 None",
            "箱数=" + all.Count + " 非 None=" + nonNone + "（5 生产调用点全改道 · 编译面已证）");
        var snapBefore = ChestSnapshot(out int empty0);
        var payload = ChestManager.Instance.SaveState();
        ChestManager.Instance.LoadState(payload);
        var snapAfter = ChestSnapshot(out int empty1);
        string diff = DiffSnap(snapBefore, snapAfter);
        Check(diff == "", "判据11 存档往返逐值一致",
            "非空箱 " + snapBefore.Count + "→" + snapAfter.Count + " 差异=" + (diff == "" ? "0（cell/bornDay/ownerFaction/contents 逐值）" : diff)
            + "｜空箱剔除 before=" + empty0 + " after=" + empty1 + "（LoadState 空箱不重建＝既定语义 SpawnChest 同源 · HH.109）");

        // ================= §G 判据9 无仓单位（怪物同型） =================
        Log("");
        Log("## §G 判据9 无仓 ⇒ 不落箱（怪物同型 · D803 行为变化）");
        var k2 = SpawnUnitDirect(Occupation.Berserker, CellCenter(NearCell(anchorCell, 7)));
        var v9 = SpawnUnitDirect(Occupation.Warrior, CellCenter(NearCell(anchorCell, 8)));
        if (k2 != null && v9 != null)
        {
            _cleanup.Add(k2.gameObject);
            k2.raceId = RaceIds.Orc;
            v9.kingdomId = -1;
            bool hasInv = v9.GetComponent<WorkerInventory>() != null;
            var before9 = AllChests();
            ds.ApplyDamage(k2, v9, 9999);
            Check(!hasInv && FindNewChest(before9) == null, "判据9 无仓单位不掉箱（怪物同型）",
                "背包在场=" + hasInv + " 箱数 " + before9.Count + "→" + AllChests().Count
                + "（⛔ 无仓 ⇒ 无箱 · 等价样本：MonsterController 同为无包 UnitController 子类且 DropLoot 已退役）");
        }
        else Check(false, "§G 直构", "k2/v9 失败");

        Finish();
    }

    // ======================= 辅助 =======================

    private static int Day() => TimeManager.Instance != null ? Mathf.RoundToInt(TimeManager.Instance.CurrentDay) : -1;
    private static int ChestTotal() => ChestManager.HasInstance ? ChestManager.Instance.Count : -1;
    private static int ChestCountAt(Vector2Int c) => ChestManager.HasInstance ? ChestManager.Instance.CountAt(new GridCoord(c.x, c.y)) : -1;

    /// <summary>权威箱集合：走 `ChestManager._chests`（经 `FillChestsInCellRect` 全图矩形）——
    /// ⛔ 不用 `FindObjectsOfType`（同帧 `Destroy` 的旧箱尚未真正销毁 ⇒ 幽灵对象污染读数；首跑判据7/11 即因此误红）。</summary>
    private static List<ChestEntity> AllChests()
    {
        var buf = new List<ChestEntity>();
        if (!ChestManager.HasInstance) return buf;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        var rect = map != null ? new RectInt(0, 0, map.width, map.height) : new RectInt(0, 0, 512, 512);
        ChestManager.Instance.FillChestsInCellRect(rect, buf);
        return buf;
    }

    private static ChestEntity FindNewChest(List<ChestEntity> before)
    {
        var now = AllChests();
        for (int i = 0; i < now.Count; i++) if (!before.Contains(now[i])) return now[i];
        return null;
    }

    private static List<ChestEntity> NewChests(List<ChestEntity> before)
    {
        var res = new List<ChestEntity>();
        var now = AllChests();
        for (int i = 0; i < now.Count; i++) if (!before.Contains(now[i])) res.Add(now[i]);
        return res;
    }

    private static ChestEntity SpawnChestAt(Vector2Int c, ResourceList pack)
        => ChestManager.HasInstance ? ChestManager.Instance.SpawnChest(new GridCoord(c.x, c.y), pack) : null;

    private static int WoodTotalInChests()
    {
        int s = 0; var all = AllChests();
        for (int i = 0; i < all.Count; i++) if (all[i] != null) s += all[i].contents.Get(ResourceType.Wood);
        return s;
    }

    private static List<ChestEntity> RingChests(Vector2Int c, int r0, int r1)
    {
        var res = new List<ChestEntity>();
        var all = AllChests();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] == null) continue;
            int d = Mathf.Max(Mathf.Abs(all[i].cell.x - c.x), Mathf.Abs(all[i].cell.y - c.y));
            if (d >= r0 && d <= r1) res.Add(all[i]);
        }
        return res;
    }

    private static List<string> ChestSnapshot(out int emptyCount)
    {
        var list = new List<string>();
        emptyCount = 0;
        var all = AllChests();
        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i]; if (c == null) continue;
            var p = c.contents;
            if (p.items == null || p.items.Length == 0) { emptyCount++; continue; }   // 空箱剔除（LoadState 既定语义：空箱不重建）
            var sb = new StringBuilder();
            sb.Append(c.cell.x).Append(',').Append(c.cell.y).Append('|').Append(c.bornDay.ToString("F2")).Append('|').Append((int)c.ownerFaction).Append('|');
            for (int k = 0; k < p.items.Length; k++)
                if (p.items[k].amount > 0) sb.Append((int)p.items[k].type).Append(':').Append(p.items[k].amount).Append(',');
            list.Add(sb.ToString());
        }
        list.Sort();
        return list;
    }

    private static string DiffSnap(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return "count " + a.Count + "→" + b.Count;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return "item" + i + " 「" + a[i] + "」→「" + b[i] + "」";
        return "";
    }

    private static BuildingDef DefOf(string id) => Resources.Load<BuildingDef>("Buildings/" + id);

    private static StorageComponent GetOrAddProbeStore(Building b)
    {
        var st = b.GetComponent<StorageComponent>();
        if (st == null)
        {
            st = b.gameObject.AddComponent<StorageComponent>();
            st.SetDeclaredPaths(new[] { WarehousePaths.All });
            st.capacity = 200;
        }
        return st;
    }

    private static Vector3 CellCenter(Vector2Int c) => GridSystem.Instance.CoordToWorld(new GridCoord(c.x, c.y));

    private static UnitController SpawnUnitDirect(Occupation occ, Vector2 pos)
    {
        var data = UnitDataManager.Instance != null ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ) : null;
        if (data == null) return null;
        var go = new GameObject("hh317_probe_" + occ);
        go.transform.position = pos;
        var uc = go.AddComponent<UnitController>();
        uc.Initialize(data);
        return uc;
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
                    if (ChestCountAt(c) > 0) continue;                  // ⛔ 避开已有箱子（读数洁净）
                    if (PlacementValidator.ValidateFootprintClear(gc, w, h)) return c;
                }
            }
        }
        return center;
    }

    private static Vector2Int NearCell(Vector2Int center, int radius)
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
                    if (ChestCountAt(c) == 0) return c;
                }
            }
        }
        return center;
    }

    private static Vector2Int FarCell(Vector2Int center, int radius)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) return center;
        for (int r = radius; r <= radius + 8; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = MapGenRules.NearestWalkable(map, center.x + dx, center.y + dy);
                    if (ChestCountAt(c) == 0) return c;
                }
            }
        }
        return center;
    }

    // ======================= 收尾（L-32）＋ 落盘 =======================

    private static void Finish()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnDied);
        Application.logMessageReceived -= OnLog;
        int cleaned = 0;
        for (int i = 0; i < _cleanup.Count; i++) if (_cleanup[i] != null) { Object.DestroyImmediate(_cleanup[i]); cleaned++; }
        _cleanup.Clear();
        Time.timeScale = 0f;                                     // L-32 条文3：真暂停
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Log("★ 收尾三态：探针残留=0（cleanup 清 " + cleaned + "）｜isDirty=" + scene.isDirty + "｜根对象数=" + scene.rootCount
            + "｜全局箱数=" + ChestTotal());
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();
        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + SLOT + "）→ 退 Play（L-32 条文1：禁留 1x 余留世界）");
        Log("★ 总判：PASS=" + _pass + " FAIL=" + _fail + "（本探针覆盖判据 1~11；判据12 回归＝M1-C 冒烟 ＋ 2_20B_M7 另跑）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh317_m1d");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh317_m1d_smoke.txt");
            File.WriteAllText(stable, Sb.ToString());
            File.WriteAllText(Path.Combine(dir, "hh317_m1d_smoke_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}
