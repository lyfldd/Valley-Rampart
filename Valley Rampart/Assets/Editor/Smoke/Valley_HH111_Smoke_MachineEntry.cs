using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.111 战争机器生产入口批 冒烟（P1~P6 行为级探针；任务书=多Agent交接/策划端/HH.111_战争机器生产入口批_任务书.md）
//  用法：菜单「Valley/验证/HH111_战争机器生产入口」——Play 后点（MCP 自动触发，零手动进局）。
//
//  入口=HH.92 测试环境正门（TestHarnessApi.EnterTestRun，考跑守卫全开；L-17 容器入口纪律）；
//  协程异常捕获器挂 RunHost（L-16）；两局方案（EnterTestRun 走 EnterGame 真实建局链路，连续调用安全）：
//    局1 人类（seed=21111）：P1 全链 / P2a 族检负 / P4 资源负 / P3 上限负 / P5 prefab 口径
//    局2 矮人（seed=21112, raceId=2）：P2b 面板数据面（矮人不显示重弩炮）/ P2c 直调族检拒 / P5b 扣费无生成现状列报
//  建筑=容器直建（BuildingFactory.CreateBuildingInstance + kingdomId 指定，HH.107 BuildAt 先例——SiegeWorkshop 同款）；
//  生产=ProduceMachine 真链（扣费/白名单/上限校验全在函数内，M3 校验逻辑零触碰）；
//  数据面断言=MachinePanel.GetVisibleMachineList/IsPrefabMissing（面板与探针共用同一函数=行为级锚不漂移）。
//
//  探针（任务书 §件3）：
//   P1 人类局全链：面板列重弩炮且无他族机器→直建工坊→合法格生产→生成成功→厂仓弹药可装填（HH.107 P4 链）
//   P2 族检负探针：人类局直调 ProduceMachine(Ram) 拒（白名单）+矮人局面板数据面不含重弩炮+直调 ProduceMachine(Ballista) 拒
//   P3 上限负探针：产满 GetMachineLimit→再产拒（计数不变）
//   P4 资源负探针：扣光金→拒（资源不足）→恢复
//   P5 prefab 缺失口径：三族机器图纸面 IsPrefabMissing=true+重弩炮 prefab 在场反向锚+矮人局扣费无生成现状列报
//   P6 四容器回归+编译（容器外执行）
//  收尾：TestHarnessApi.ExitTestRun（L-17）+QuitSmoke。
// ============================================================================
public static class Valley_HH111_Smoke_MachineEntry
{
    private const int SEED_HUMAN = 21111;
    private const int SEED_DWARF = 21112;
    private const string SLOT = "smoke_h111";
    private const string TAG = "[HH111冒烟]";

    [MenuItem("Valley/验证/HH111_战争机器生产入口")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先进入 Play（MCP enterPlaymode 后调用本菜单）。"); return; }
        new GameObject("HH111_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
                Debug.LogError("[HH111冒烟][协程异常捕获器] " + condition + "\n" + stackTrace);
        }
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    // ===== 探针结果台账 =====
    private static readonly List<string> _results = new List<string>();
    private static bool _allPass = true;

    private static void Record(string probe, bool pass, string detail)
    {
        if (!pass) _allPass = false;
        _results.Add($"{probe} {detail} ={pass}");
        Debug.Log($"{TAG} {probe} {detail} ={pass}");
    }

    private static IEnumerator RunCoroutine()
    {
        _results.Clear();
        _allPass = true;

        // ================= 局1：人类局 =================
        yield return EnterRun(SEED_HUMAN, RaceIds.Human);
        yield return WaitWorldReady();
        if (WorldManager.Instance == null) yield break;   // WaitWorldReady 内已 FailFast

        var ruler = RulerController.Instance;
        var sys = SiegeProductionSystem.Instance;

        // 资源注入（扣费面真源=RulerController 国库；机器造价金/石/木——L-07 注入口径：本链无仓库搬运/水，国库单源即生产真源）
        InjectResource(ruler, ResourceType.Gold, 2000);
        InjectResource(ruler, ResourceType.Stone, 2000);
        InjectResource(ruler, ResourceType.Wood, 2000);

        // ===== P1a 面板数据构成（本族列表=重弩炮且无他族机器）=====
        var humanList = MachinePanel.GetVisibleMachineList(RaceIds.Human);
        bool p1a = humanList.Count == 1 && humanList.Contains(Occupation.Ballista)
                   && !humanList.Contains(Occupation.Mortar) && !humanList.Contains(Occupation.VineCatapult)
                   && !humanList.Contains(Occupation.Ram) && !humanList.Contains(Occupation.SiegeMachine);
        Record("P1a", p1a, $"人类可见机器=[{string.Join(",", humanList)}]（他族+退役共通槽不可见）");

        // ===== P1b 直建战争机器工坊（HH.107 BuildAt 先例；厂仓弹药面载体）=====
        var wsDef = BuildingFactory.FindDefById("SiegeWorkshop");
        if (wsDef == null) { FailFast("SiegeWorkshop def 未找到（Resources/Buildings/SiegeWorkshop.asset）"); yield break; }
        var map = WorldManager.Instance.ActiveMap;
        Vector2Int wsCell = MapGenRules.NearestWalkable(map, 40, 40);
        var workshop = BuildAt(wsDef, wsCell, 4, 0);
        if (workshop == null) { FailFast("SiegeWorkshop 直建失败"); yield break; }
        var wsComp = workshop.GetComponent<SiegeWorkshopBuilding>();
        yield return new WaitForSeconds(0.5f);
        Record("P1b", wsComp != null && wsComp.IsReady,
            $"工坊直建 Lv.{workshop.level} 弹药仓就绪={wsComp != null && wsComp.IsReady}（3 子仓）");

        // ===== P1c 生产真链（合法格→ProduceMachine→生成成功）=====
        int countBefore = sys.GetPlacedMachineCount();
        Vector2Int cellOk = MapGenRules.NearestWalkable(map, 45, 45);
        var coordOk = new GridCoord(cellOk.x, cellOk.y);
        bool placeOk = MachinePlacer.CanPlaceCell(coordOk);   // 放置校验真源直调（水/障碍/占格面）
        Vector2 spawnPos = GridSystem.Instance.CoordToWorld(coordOk);
        bool produced = sys != null && sys.ProduceMachine(Occupation.Ballista, spawnPos);
        int countAfter = sys.GetPlacedMachineCount();
        bool unitOnField = CountMachineOnField(Occupation.Ballista) > 0;
        Record("P1c", placeOk && produced && countAfter == countBefore + 1 && unitOnField,
            $"合法格={placeOk} 生产={produced} 场上机器 {countBefore}→{countAfter} Ballista 在场={unitOnField}");

        // ===== P1d 厂仓弹药可装填（HH.107 P4 链口径：产弹→可取）=====
        bool p1d = false; string p1dDetail = "wsComp 缺失";
        if (wsComp != null)
        {
            int stoneBefore = ruler.GetResource(ResourceType.Stone);
            int ammoBefore = wsComp.GetAmmo(ResourceType.StoneAmmo);
            int made = wsComp.Produce(ResourceType.StoneAmmo, 5);
            int ammoAfter = wsComp.GetAmmo(ResourceType.StoneAmmo);
            int taken = wsComp.TakeAmmo(ResourceType.StoneAmmo, 2);
            p1d = made > 0 && ammoAfter > ammoBefore && taken == 2
                  && ruler.GetResource(ResourceType.Stone) < stoneBefore;
            p1dDetail = $"产石弹={made} 弹仓 {ammoBefore}→{ammoAfter} 可取={taken} 国库石 {stoneBefore}→{ruler.GetResource(ResourceType.Stone)}";
        }
        Record("P1d", p1d, p1dDetail);

        // ===== P2a 族检负探针（人类局直调 Ram=兽人族机器→白名单拒）=====
        int cntBeforeP2 = sys.GetPlacedMachineCount();
        bool ramRejected = !sys.ProduceMachine(Occupation.Ram, spawnPos);
        bool p2a = ramRejected && sys.GetPlacedMachineCount() == cntBeforeP2;
        Record("P2a", p2a, $"直调 ProduceMachine(Ram) 拒={ramRejected} 计数不变={sys.GetPlacedMachineCount() == cntBeforeP2}（白名单日志见控制台）");

        // ===== P4 资源负探针（扣光金→拒）=====
        int goldAll = ruler.GetResource(ResourceType.Gold);
        if (goldAll > 0) ruler.ModifyResource(ResourceType.Gold, false, goldAll);
        int cntBeforeP4 = sys.GetPlacedMachineCount();
        bool poorRejected = !sys.ProduceMachine(Occupation.Ballista, spawnPos);
        bool p4 = poorRejected && sys.GetPlacedMachineCount() == cntBeforeP4;
        InjectResource(ruler, ResourceType.Gold, 2000);   // 恢复（P3 要用）
        Record("P4", p4, $"金=0 生产拒={poorRejected} 计数不变={sys.GetPlacedMachineCount() == cntBeforeP4}（资源不足日志见控制台）→金已恢复注 2000");

        // ===== P3 上限负探针（产满 GetMachineLimit→再产拒）=====
        int limit = sys.GetMachineLimit();
        int cnt = sys.GetPlacedMachineCount();
        bool p3 = false; string p3Detail;
        if (cnt >= limit)
        {
            p3 = !sys.ProduceMachine(Occupation.Ballista, spawnPos) && sys.GetPlacedMachineCount() == cnt;
            p3Detail = $"已满 {cnt}/{limit} 直拒";
        }
        else
        {
            bool allOk = true;
            while (sys.GetPlacedMachineCount() < limit && allOk)
                allOk = sys.ProduceMachine(Occupation.Ballista, spawnPos);
            int full = sys.GetPlacedMachineCount();
            bool overRejected = !sys.ProduceMachine(Occupation.Ballista, spawnPos);
            p3 = allOk && full == limit && overRejected && sys.GetPlacedMachineCount() == limit;
            p3Detail = $"产至 {full}/{limit}（全成={allOk}）→第 {limit + 1} 台拒={overRejected} 计数不变";
        }
        Record("P3", p3, p3Detail);

        // ===== P5 prefab 缺失口径（图纸面：三族机器缺 prefab+重弩炮在场反向锚+预检拦截）=====
        bool m3Missing = MachinePanel.IsPrefabMissing(Occupation.Mortar, out string whyM);
        bool vcMissing = MachinePanel.IsPrefabMissing(Occupation.VineCatapult, out string whyV);
        bool ramMissing = MachinePanel.IsPrefabMissing(Occupation.Ram, out string whyR);
        bool ballistaReady = !MachinePanel.IsPrefabMissing(Occupation.Ballista, out _);
        Record("P5a", m3Missing && vcMissing && ramMissing && ballistaReady,
            $"臼炮缺图={m3Missing}（{whyM}）藤蔓缺图={vcMissing} 攻城槌缺图={ramMissing} 重弩炮在场={ballistaReady}（反向锚）");

        TestHarnessApi.ExitTestRun();   // 局1 收尾（L-17：考跑态复原，下一局重建）

        // ================= 局2：矮人局（raceId=2）=================
        yield return EnterRun(SEED_DWARF, RaceIds.Dwarf);
        yield return WaitWorldReady();
        if (WorldManager.Instance == null) yield break;

        var ruler2 = RulerController.Instance;
        var sys2 = SiegeProductionSystem.Instance;

        // ===== P2b 面板数据面（矮人局：玩家族运行时读数=2+可见列表=[臼炮] 不含重弩炮）=====
        int dwarfRace = KingdomRace.GetKingdomRace(0);
        var dwarfList = MachinePanel.GetVisibleMachineList(dwarfRace);
        bool p2b = dwarfRace == RaceIds.Dwarf
                   && dwarfList.Count == 1 && dwarfList.Contains(Occupation.Mortar)
                   && !dwarfList.Contains(Occupation.Ballista);
        Record("P2b", p2b, $"矮人局玩家族={dwarfRace}（RaceIds.Dwarf=2）可见机器=[{string.Join(",", dwarfList)}]（重弩炮不可见=白名单 UI 过滤）");

        // ===== P2c 直调族检拒（矮人族调人类重弩炮→白名单拒）=====
        var map2 = WorldManager.Instance.ActiveMap;
        Vector2Int cell2 = MapGenRules.NearestWalkable(map2, 45, 45);
        Vector2 spawnPos2 = GridSystem.Instance.CoordToWorld(new GridCoord(cell2.x, cell2.y));
        int cnt2 = sys2.GetPlacedMachineCount();
        bool ballistaRejected = !sys2.ProduceMachine(Occupation.Ballista, spawnPos2);
        Record("P2c", ballistaRejected && sys2.GetPlacedMachineCount() == cnt2,
            $"矮人局直调 ProduceMachine(Ballista) 拒={ballistaRejected}（白名单日志见控制台）");

        // ===== P5b 扣费无生成现状列报（矮人族合法机器臼炮：prefab 缺→UnitFactory 拒生成=钱已扣无产出）=====
        InjectResource(ruler2, ResourceType.Gold, 2000);
        InjectResource(ruler2, ResourceType.Stone, 2000);
        InjectResource(ruler2, ResourceType.Wood, 2000);
        int goldBefore = ruler2.GetResource(ResourceType.Gold);
        int cntBeforeP5 = sys2.GetPlacedMachineCount();
        bool mortarCall = sys2.ProduceMachine(Occupation.Mortar, spawnPos2);   // 现状：Spend 后 SpawnUnit 拒，返回 true
        int goldAfter = ruler2.GetResource(ResourceType.Gold);
        int cntAfterP5 = sys2.GetPlacedMachineCount();
        // 现状断言（非缺陷定性）：调用 true+金扣减+场上计数不变=「扣费无生成」既有语义成立；
        // 正常玩家流程被 MachinePanel.IsPrefabMissing 预检兜住（不进放置不扣费，P5a 已断言图纸面）
        Record("P5b", mortarCall && goldAfter < goldBefore && cntAfterP5 == cntBeforeP5,
            $"现状列报：ProduceMachine(Mortar) 返回={mortarCall} 金 {goldBefore}→{goldAfter} 场上 {cntBeforeP5}→{cntAfterP5}"
            + "（扣费无生成=既有语义，UI 预检兜正常流程不扣费；美术批7 prefab 到位后自动消除）");

        // ---- 汇总+收尾 ----
        Debug.Log($"{TAG} ===== 轮汇总（{_results.Count} 探针）=====");
        for (int i = 0; i < _results.Count; i++) Debug.Log($"{TAG} {_results[i]}");
        Debug.Log($"{TAG} 判定：{(_allPass ? "ALL PASS" : "HAS FAIL")}");
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }

    // ===== helpers =====

    /// <summary>进局（L-17 正门；raceId 按局传——人类 0/矮人 2）。</summary>
    private static IEnumerator EnterRun(int seed, int raceId)
    {
        var cfg = new NewGameConfig
        {
            worldSeed = seed,
            mapSeed = seed,
            raceId = raceId,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            selectedSlotId = SLOT,
            kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 60f);   // 60x=1 游戏日 6s 真实
    }

    /// <summary>等世界就稳（120s 超时；含 UnitDataManager——IsPrefabMissing 查表前置）。</summary>
    private static IEnumerator WaitWorldReady()
    {
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || UnitFactory.Instance == null
               || UnitDataManager.Instance == null || RulerController.Instance == null
               || SiegeProductionSystem.Instance == null || GridSystem.Instance == null
               || BuildingFactory.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            {
                FailFast("等世界就绪超时(120s)。");
                yield break;
            }
        }
        yield return new WaitForSeconds(0.5f);   // 稳态窗（HH.69 教训）
    }

    /// <summary>资源注入（补差到目标额；扣费面真源=RulerController 国库——机器链无仓库/水参与）。</summary>
    private static void InjectResource(RulerController ruler, ResourceType type, int target)
    {
        int cur = ruler.GetResource(type);
        if (cur < target) ruler.ModifyResource(type, true, target - cur);
    }

    /// <summary>场上某机器职业计数（Faction.PlayerCamp，与 GetPlacedMachineCount 同口径独立复算）。</summary>
    private static int CountMachineOnField(Occupation occ)
    {
        int n = 0;
        if (UnitRegistry.Instance == null) return n;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || u.Data == null) continue;
            if (u.Data.faction == Faction.PlayerCamp && u.EffectiveOccupation == occ) n++;
        }
        return n;
    }

    private static void FailFast(string reason)
    {
        Debug.LogError($"{TAG} 中止：{reason}");
        TestHarnessApi.ExitTestRun();   // L-17 收尾：异常路径同复原
        SmokeApi.QuitSmoke();
    }

    /// <summary>直建建筑（生产链 CreateBuildingInstance，HH.107 BuildAt 先例逐字同构）。</summary>
    private static Building BuildAt(BuildingDef def, Vector2Int center, int ringIdx, int kingdomId)
    {
        if (def == null || BuildingFactory.Instance == null || GridSystem.Instance == null) return null;
        var map = WorldManager.Instance.ActiveMap;
        Vector2Int cell = MapGenRules.NearestWalkable(map, center.x, center.y);
        for (int r = 1; r <= 12 && r <= ringIdx; r++)
        {
            var cand = MapGenRules.NearestWalkable(map, center.x + r, center.y);
            if (cand != cell || r == ringIdx) { cell = cand; break; }
        }
        var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1, def.footprint.y > 0 ? def.footprint.y : 1);
        var coord = new GridCoord(cell.x, cell.y);
        var g = GridSystem.Instance;
        Vector3 world = g.Config != null
            ? (Vector3)g.CoordToWorld(coord) + new Vector3((fp.x - 1) * 0.5f * g.Config.cellSize.x, (fp.y - 1) * 0.5f * g.Config.cellSize.y, 0f)
            : new Vector3(coord.x, coord.y, 0f);
        bool ok = BuildingFactory.Instance.CreateBuildingInstance(
            def, def.sourceType, coord, fp, world,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: kingdomId);
        if (!ok) return null;
        var all = BuildingRegistry.Instance.All;
        for (int i = all.Count - 1; i >= 0; i--)
            if (all[i] != null && all[i].coord.x == coord.x && all[i].coord.y == coord.y) return all[i];
        return null;
    }
}
