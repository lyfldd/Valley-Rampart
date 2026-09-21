using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using static BuildingFactory;

// ============================================================================
//  HH.318 · M1-E「金币降普通资源」正门冒烟（Editor-only）
//  口径真源：HH.318 施工任务书 件1~件8 ＋ 判据 1~8（`D805` 裁）。
//  入口纪律（test-harness-first 铁律1）：走正门 `TestHarnessApi.EnterTestRun`（⛔ 禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → 封盘 → ExitTestRun → 退 Play。
//
//  段（与判据一一对应）：
//    §B 判据1 金进仓 ＋ 不占容量（体积 0：UsedSpace 不变）
//    §C 判据2 玩家扣费走仓（`WarehouseHelper.TrySettle` ＝ `BuildController.PayOrderCost` 玩家分支唯一终点）
//              ＋ 事件补发（金走仓后不再经 `ModifyResource` ⇒ 须由 `WarehouseHelper` 补发）
//    §D 判据3 AI 金面（入仓／扣费／读口一致 ＋ 僵尸台账=0 —— Q0「#52 金面并入」）
//    §E 判据4 `Gold` 只读门面（读数＝Vault 金 · UI 读点零改）
//    §F 判据5 存档（Q5 改裁：新档 ⛔ 不写 gold ／ 旧档 gold>0 ⇒ 告警在场 ＋ 金作废 ⛔ 无桥）
//    §G 判据6 `U-7`（Well 改声明 `res_fluid.water` ⇒ ⛔ 不收金）＋ 件4 金走仓落点可达
//    §H 判据7 `×1.5`（兽人 ⇒ `RoundToInt(原量×1.5)`；非兽人 ⇒ ⛔ 不落箱 —— `M1-D` 已定口径）
//  判据8（回归：HH317 M1-D ／ 2_20B_M7 ／ HH315 M1-C）＝另跑，不进本探针。
//  落盘：Logs/hh318_m1e/hh318_m1e_smoke.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH318M1EProbe
{
    public const string Tag = "HH318M1E";
    private const string Menu = "Valley/验证/HH318 M1-E 金币降普通资源 正门冒烟（正门进局）";
    private const int SEED = 31821;
    private const string SLOT = "hh318_m1e";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;
    private static int _pass, _fail;
    private static readonly List<GameObject> _cleanup = new List<GameObject>();

    private static readonly List<UnitDiedEvent> _died = new List<UnitDiedEvent>();          // 事件直证
    private static readonly List<string> _orcLootLogs = new List<string>();                // [OrcLoot] 日志
    private static readonly List<RulerResourceChangedEvent> _resEvents = new List<RulerResourceChangedEvent>();  // 判据2 事件补发
    private static readonly List<string> _warnLogs = new List<string>();                   // 判据5/6 告警在场

    [MenuItem(Menu, priority = 219)]
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
        Sb.AppendLine("# HH.318 · M1-E「金币降普通资源」正门冒烟（seed=" + SEED + " 槽=" + SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Sb.AppendLine("# 口径：施工任务书 件1~件8 ＋ 判据 1~8（D805 裁）");
        new GameObject("HH318M1EProbeHost").AddComponent<Host>().Go(Run());
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
    private static void OnRes(RulerResourceChangedEvent e) => _resEvents.Add(e);
    private static void OnLog(string msg, string stack, LogType type)
    {
        if (msg == null) return;
        if (msg.Contains("[OrcLoot]")) _orcLootLogs.Add(msg);
        if (type == LogType.Warning) _warnLogs.Add(msg);
    }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = SLOT, kingdomName = "M1E探针"
        };
        Log("── §A 正门进局（seed=" + SEED + " Small/difficulty=2）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }
        EventBus.Subscribe<UnitDiedEvent>(OnDied);
        EventBus.Subscribe<RulerResourceChangedEvent>(OnRes);
        Application.logMessageReceived += OnLog;
        Log("§A 起局：地图=" + map.width + "x" + map.height + " 日=" + Day() + " 箱数=" + ChestTotal() + " 倍率=" + Time.timeScale);

        var anchorSub = GridSystem.Instance.WorldToSubCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        Vector2Int anchorCell;
        if (anchorSub.HasValue)
        {
            var ac = GridSystem.Instance.SubToCell(anchorSub.Value);
            anchorCell = new Vector2Int(ac.x, ac.y);
        }
        else anchorCell = MapGenRules.NearestWalkable(map, map.width / 2, map.height / 2);
        anchorCell = MapGenRules.NearestWalkable(map, anchorCell.x, anchorCell.y);

        var ruler = RulerController.Instance;
        var tv = TreasureVault.Instance;
        if (ruler == null || tv == null)
        {
            Check(false, "§A 前置（RulerController/TreasureVault 在场）",
                "ruler=" + (ruler != null) + " vault=" + (tv != null) + " ⇒ 判据 1~5 依赖不在场");
            Finish(); yield break;
        }
        Log("§A 玩家国库：容器=" + tv.name + " 容量=" + tv.BaseCapacity + " 声明=[res_material,res_food,res_currency]（M1-E 后）");
        // `TreasureVault._container` 为 private ⇒ 走子物体（"Vault"）取其 `StorageComponent`（`UsedSpace`/`TotalCount` 在读口上）
        var vaultStore = tv.GetComponentInChildren<StorageComponent>();
        if (vaultStore == null) Log("⚠️ 未找到国库容器 StorageComponent（判据1b 的 UsedSpace/件数读数将失真）");

        // ================= §B 判据1 金进仓 ＋ 不占容量 =================
        Log("");
        Log("## §B 判据1 金进仓 ＋ 不占容量（体积 0 · `StorageComponent:149` 金用例）");
        Log("§B 鉴别力声明：改前金在 `RulerController.Gold` 字段（`Vault.GetAmount(Gold)` **恒 0**）");
        Log("§B         ⇒ 本次注金后仓读数应增 100 且 `UsedSpace`（Σ量×体积）**不变**（金体积 0）。");
        int gold0 = tv.GetAmount(ResourceType.Gold);
        int used0 = vaultStore != null ? vaultStore.UsedSpace : -1;
        int total0 = vaultStore != null ? vaultStore.TotalCount : -1;
        ruler.ModifyResource(ResourceType.Gold, true, 100);
        int gold1 = tv.GetAmount(ResourceType.Gold);
        int used1 = vaultStore != null ? vaultStore.UsedSpace : -1;
        int total1 = vaultStore != null ? vaultStore.TotalCount : -1;
        Check(gold1 == gold0 + 100, "判据1a 金进仓（Vault 真源）",
            "Vault.GetAmount(Gold) " + gold0 + "→" + gold1 + "（期望 +100）｜鉴别力：改前恒 0");
        Check(vaultStore != null && used1 == used0, "判据1b 金不占容量（体积 0）",
            "UsedSpace " + used0 + "→" + used1 + "（期望不变）·件数 " + total0 + "→" + total1 + "（件数计入、容量不计）");

        // ================= §C 判据2 玩家扣费走仓（＋事件补发）=================
        Log("");
        Log("## §C 判据2 玩家扣费走仓（`WarehouseHelper.TrySettle` ＝ `BuildController.PayOrderCost` 玩家分支唯一终点）");
        Log("§C 鉴别力声明：改前 `WarehouseHelper:65-66` 金走 `RulerController.Spend`（字段直减 ⇒ Vault 读数**不变**）；");
        Log("§C         改后金与普通资源同路（`LockTakes`/`ApplyTakes`）⇒ 应从国库容器扣 ＋ 事件补发一条 Gold。");
        int gA = tv.GetAmount(ResourceType.Gold);
        _resEvents.Clear();
        bool settled = WarehouseHelper.TrySettle(ResourceList.Of(new ResourceAmount(ResourceType.Gold, 30)));
        int gB = tv.GetAmount(ResourceType.Gold);
        Check(settled && gB == gA - 30, "判据2a 扣费从国库容器扣",
            "TrySettle(金30)=" + settled + " ⇒ Vault 金 " + gA + "→" + gB + "（期望 −30）");
        bool evtGold = false; string evtDetail = "（无事件）";
        for (int i = 0; i < _resEvents.Count; i++)
            if (_resEvents[i].Type == ResourceType.Gold)
            { evtGold = true; evtDetail = "Gold " + _resEvents[i].OldValue + "→" + _resEvents[i].NewValue; break; }
        Check(evtGold, "判据2b 金扣减事件补发（四订阅者刷新依赖）", evtDetail + "｜共 " + _resEvents.Count + " 条事件");
        // 负向：余额不足 ⇒ 不扣（原子性保持）
        int gC = tv.GetAmount(ResourceType.Gold);
        bool huge = WarehouseHelper.TrySettle(ResourceList.Of(new ResourceAmount(ResourceType.Gold, gC + 1000)));
        Check(!huge && tv.GetAmount(ResourceType.Gold) == gC, "判据2c 余额不足 ⇒ 整笔不扣（原子性）",
            "TrySettle(超额)=" + huge + " ⇒ Vault 金 " + gC + "→" + tv.GetAmount(ResourceType.Gold) + "（期望不变）");

        // ================= §D 判据3 AI 金面 =================
        Log("");
        Log("## §D 判据3 AI 金面（Q0「#52 金面并入」：入仓／扣费／读口一致 ＋ 僵尸台账=0）");
        Log("§D 鉴别力声明：改前 AI 金在 `KingdomState.resources`（`TreasureVault.Get(k>0)` 读数恒 0）；");
        Log("§D         若「进了仓但消费面读不到」（双真源/黑洞）⇒ 3a/3c 必红。");
        var reg = KingdomRegistry.Instance;
        var k1 = reg != null ? reg.Get(1) : null;
        var tv1 = TreasureVault.Get(1);
        if (k1 == null || tv1 == null)
        {
            Check(false, "判据3 前置（AI 国 k1 ＋ 其国库仓在场）",
                "k1=" + (k1 != null) + " tv1=" + (tv1 != null) + " ⇒ 本档位无 AI 国或主城未生成");
        }
        else
        {
            int a0 = tv1.GetAmount(ResourceType.Gold);
            int r0 = k1.GetResourceValue(ResourceType.Gold);
            k1.AddResources(ResourceList.Of(new ResourceAmount(ResourceType.Gold, 50)));
            int a1 = tv1.GetAmount(ResourceType.Gold);
            int r1 = k1.GetResourceValue(ResourceType.Gold);
            int zombie = k1.resources.Get(ResourceType.Gold);
            Check(a1 == a0 + 50 && r1 == a1, "判据3a AI 金入仓＋读口一致",
                "Vault.Get(1).金 " + a0 + "→" + a1 + "（期望 +50）·k1.GetResourceValue(Gold)=" + r1 + "（r0=" + r0 + "）");
            Check(zombie == 0, "判据3b 僵尸台账=0（⛔ 不双写 · 禁双写红线）",
                "k1.resources.Get(Gold)=" + zombie + "（期望 0 —— 入仓后台账不得残留第二真源）");
            k1.Spend(ResourceList.Of(new ResourceAmount(ResourceType.Gold, 20)));
            int a2 = tv1.GetAmount(ResourceType.Gold);
            int r2 = k1.GetResourceValue(ResourceType.Gold);
            Check(a2 == a1 - 20 && r2 == a2, "判据3c AI 扣费读那座仓（消费面同批）",
                "扣 20 ⇒ Vault 金 " + a1 + "→" + a2 + "（期望 −20）·读口=" + r2);
            // 负向：AI 余额不足 ⇒ CanAfford false（读口同源）
            bool aff = k1.CanAfford(ResourceList.Of(new ResourceAmount(ResourceType.Gold, a2 + 1000)));
            bool aff2 = k1.CanAfford(ResourceList.Of(new ResourceAmount(ResourceType.Gold, Mathf.Max(1, a2))));
            Check(!aff && (a2 <= 0 || aff2), "判据3d AI CanAfford 走读口",
                "超额可负担=" + aff + "（期望 False）· 足额可负担=" + aff2);
        }

        // ================= §E 判据4 Gold 只读门面 =================
        Log("");
        Log("## §E 判据4 `Gold` 只读门面（UI 读点零改 · 读数＝Vault 金）");
        Log("§E 鉴别力声明：改前 `Gold` 是独立字段（与 Vault 无联动）；改后它必等于 Vault 金（同一真源）。");
        Check(ruler.Gold == tv.GetAmount(ResourceType.Gold), "判据4a Gold == Vault 金",
            "RulerController.Gold=" + ruler.Gold + " · Vault=" + tv.GetAmount(ResourceType.Gold));
        ruler.ModifyResource(ResourceType.Gold, true, 7);
        Check(ruler.Gold == tv.GetAmount(ResourceType.Gold), "判据4b 门面随仓同步（只读转发）",
            "注金 7 后 Gold=" + ruler.Gold + " · Vault=" + tv.GetAmount(ResourceType.Gold));
        // 只读面：其它资源读口仍通（零回归）
        int stoneViaFacade = ruler.GetResource(ResourceType.Stone);
        Check(stoneViaFacade == tv.GetAmount(ResourceType.Stone), "判据4c 非金读口零回归",
            "GetResource(Stone)=" + stoneViaFacade + " · Vault=" + tv.GetAmount(ResourceType.Stone));

        // ================= §F 判据5 存档 =================
        Log("");
        Log("## §F 判据5 存档（`D805` `Q5` 改裁：⛔ 无迁移桥 · 新档不写 gold ＋ 旧档告警）");
        Log("§F 鉴别力声明：改前 `SaveState` 写 `gold=Gold` 且 `LoadState` 恢复它；改后新档 gold=0、旧档告警且金不入仓。");
        var payload = ruler.SaveState();
        var saved = JsonUtility.FromJson<RulerSaveData>(payload.json);
        Check(saved.gold == 0, "判据5a 新档不写 gold（schema 保留 · 值 0）",
            "SaveState().json ⇒ gold=" + saved.gold + "（期望 0 · ⛔ 不再写）｜json 片段="
            + (payload.json != null && payload.json.Length > 80 ? payload.json.Substring(0, 80) : payload.json));
        int gb5 = tv.GetAmount(ResourceType.Gold);
        int w0 = _warnLogs.Count;
        const string legacyJson = "{\"rulerName\":\"旧档君主\",\"gold\":50}";
        ruler.LoadState(new SavePayload
        {
            typeName = typeof(RulerSaveData).AssemblyQualifiedName,
            json = legacyJson,
            version = 1
        });
        int w1 = _warnLogs.Count;
        bool warnHit = false; string warnMsg = "（无）";
        for (int i = w0; i < w1; i++) if (_warnLogs[i].Contains("M1-E")) { warnHit = true; warnMsg = _warnLogs[i]; break; }
        Check(warnHit, "判据5b 旧档金告警在场（D788 §4 口径）", warnMsg);
        Check(tv.GetAmount(ResourceType.Gold) == gb5, "判据5c 旧档金不入仓（⛔ 无桥）",
            "Vault 金 " + gb5 + "→" + tv.GetAmount(ResourceType.Gold) + "（期望不变 ⇒ 旧档金作废）");

        // ================= §G 判据6 U-7 ＋ 金走仓落点 =================
        Log("");
        Log("## §G 判据6 `U-7`（Well 声明 `res_fluid.water` ⇒ ⛔ 不收金）＋ 件4 金走仓落点可达");
        Log("§G 鉴别力声明：改前 `Well.asset` 声明 `res_currency.gold`（全库唯一收金仓）⇒ `Add(Gold)` 收得进；");
        Log("§G         改后声明水族（`ResourceType.Water` 未落枚举 ⇒ 匹配不到任何资源）⇒ 应拒收。");
        var wellDef = FindDefById("Well");
        var well = wellDef != null ? MakeAt(wellDef, FreeCell(wellDef, anchorCell), 0, true, BuildingState.Active) : null;
        if (well == null) { Check(false, "§G Well 直建", "失败（def=" + (wellDef != null) + "）"); }
        else
        {
            _cleanup.Add(well.gameObject);
            var ws = well.GetComponent<StorageComponent>();
            bool acceptsGold = ws != null && ws.Accepts(ResourceType.Gold);
            int added = ws != null ? ws.Add(ResourceType.Gold, 10) : -1;
            Check(ws != null && !acceptsGold && added == 0, "判据6a Well 拒收金（U-7 已修）",
                "Accepts(Gold)=" + acceptsGold + "（期望 False）· Add(Gold,10)=" + added + "（期望 0）");
            string dp = ws != null && ws.DeclaredPaths.Length > 0 ? string.Join(",", ws.DeclaredPaths) : "（空）";
            Check(ws != null && dp == "res_fluid.water", "判据6b 声明已改为 res_fluid.water（⛔ 非删行）", "DeclaredPaths=[" + dp + "]");
        }
        var found = WarehouseRegistry.FindNearestAvailable(ResourceType.Gold, tv.transform.position, 0);
        Check(found != null, "判据6c 金走仓落点可达（件4 依赖：FindNearestAvailable(Gold)）",
            "命中=" + (found != null ? found.name + "（host=" + (found.GetComponentInParent<Building>() != null ? found.GetComponentInParent<Building>().def.id : "?") + "）" : "null ⇒ 金只能走 AddGatherOverflow 兜底"));

        // ================= §H 判据7 ×1.5 =================
        Log("");
        Log("## §H 判据7 兽人 `×1.5`（只乘金 · `RoundToInt` · `09` §9.9 生成口径）");
        Log("§H 鉴别力声明：改前（`M1-D`）落箱金＝死者背包原量（10）；本次兽人击杀 ⇒ 15（10×1.5）；");
        Log("§H         ⚠️ 非兽人 ⇒ `M1-D` 已定口径＝**不落箱**（任务书「非兽人⇒原量」字样按 `M1-D` 口径校正为「不落箱」）。");
        var ds = DamageSystem.Instance;
        if (ds == null) { Check(false, "§H DamageSystem", "不在场"); }
        else
        {
            var kO = SpawnUnitDirect(Occupation.Berserker, CellCenter(NearCell(anchorCell, 8)));
            var vG = SpawnUnitDirect(Occupation.Warrior, CellCenter(NearCell(anchorCell, 9)));
            if (kO == null || vG == null) { Check(false, "§H 直构", "kO/vG 失败"); }
            else
            {
                _cleanup.Add(kO.gameObject);
                kO.raceId = RaceIds.Orc;
                vG.kingdomId = -1;
                var iv = vG.GetOrAddInventory();
                int storedG = iv.TryStore(ResourceType.Gold, 10);
                var beforeC = AllChests();
                int logs0 = _orcLootLogs.Count;
                ds.ApplyDamage(kO, vG, 9999);
                var nc = FindNewChest(beforeC);
                int gotG = nc != null ? nc.contents.Get(ResourceType.Gold) : -1;
                Check(storedG == 10 && nc != null && gotG == 15, "判据7a 兽人击杀 ⇒ 金 ×1.5",
                    "死者背包金=" + storedG + " ⇒ 箱内金=" + gotG + "（期望 15 ＝ RoundToInt(10×1.5)）");
                string lastLog = _orcLootLogs.Count > logs0 ? _orcLootLogs[_orcLootLogs.Count - 1] : "（无）";
                Check(lastLog.Contains("×1.5"), "判据7b 日志命中分支（⛔ 不用乘后值反推）", lastLog);
            }
            var kN = SpawnUnitDirect(Occupation.Berserker, CellCenter(NearCell(anchorCell, 10)));
            var vN = SpawnUnitDirect(Occupation.Warrior, CellCenter(NearCell(anchorCell, 11)));
            if (kN == null || vN == null) { Check(false, "§H 负例直构", "kN/vN 失败"); }
            else
            {
                _cleanup.Add(kN.gameObject);
                kN.raceId = RaceIds.Human;             // ⛔ 非兽人 ⇒ 不触发战利品（M1-D 口径）
                vN.kingdomId = -1;
                vN.GetOrAddInventory().TryStore(ResourceType.Gold, 10);
                var beforeN = AllChests();
                ds.ApplyDamage(kN, vN, 9999);
                Check(FindNewChest(beforeN) == null, "判据7c 非兽人 ⇒ ⛔ 不落箱（M1-D 已定口径）",
                    "箱数 " + beforeN.Count + "→" + AllChests().Count + "（期望不变）");
            }
        }

        Finish();
    }

    // ======================= 辅助 =======================

    private static int Day() => TimeManager.Instance != null ? Mathf.RoundToInt(TimeManager.Instance.CurrentDay) : -1;
    private static int ChestTotal() => ChestManager.HasInstance ? ChestManager.Instance.Count : -1;

    /// <summary>权威箱集合：走 `ChestManager._chests`（经 `FillChestsInCellRect` 全图矩形）——
    /// ⛔ 不用 `FindObjectsOfType`（同帧 `Destroy` 的旧箱尚未真正销毁 ⇒ 幽灵对象污染读数 · HH317 首跑教训）。</summary>
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

    private static Vector3 CellCenter(Vector2Int c) => GridSystem.Instance.CoordToWorld(new GridCoord(c.x, c.y));

    private static UnitController SpawnUnitDirect(Occupation occ, Vector2 pos)
    {
        var data = UnitDataManager.Instance != null ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ) : null;
        if (data == null) return null;
        var go = new GameObject("hh318_probe_" + occ);
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

    /// <summary>确定性找一块空的可放置格（环形外扩 · ⛔ 不用 Random）。</summary>
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
                    return MapGenRules.NearestWalkable(map, center.x + dx, center.y + dy);
                }
            }
        }
        return center;
    }

    // ======================= 收尾（L-32）＋ 落盘 =======================

    private static void Finish()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnDied);
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnRes);
        Application.logMessageReceived -= OnLog;
        int cleaned = 0;
        for (int i = 0; i < _cleanup.Count; i++) if (_cleanup[i] != null) { Object.DestroyImmediate(_cleanup[i]); cleaned++; }
        _cleanup.Clear();
        Time.timeScale = 0f;                                     // L-32 条文3：真暂停
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var tv = TreasureVault.Instance;
        Log("★ 收尾三态：探针残留=0（cleanup 清 " + cleaned + "）｜isDirty=" + scene.isDirty + "｜根对象数=" + scene.rootCount
            + "｜玩家国库金=" + (tv != null ? tv.GetAmount(ResourceType.Gold) : -1) + "｜全局箱数=" + ChestTotal());
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();
        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + SLOT + "）→ 退 Play（L-32 条文1）");
        Log("★ 总判：PASS=" + _pass + " FAIL=" + _fail + "（本探针覆盖判据 1~7；判据8 回归＝M1-C/M7/HH317 另跑）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh318_m1e");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh318_m1e_smoke.txt");
            File.WriteAllText(stable, Sb.ToString());
            File.WriteAllText(Path.Combine(dir, "hh318_m1e_smoke_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}
