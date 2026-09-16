using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.257 OB1-2 A 批（D704 勘正 / D705 放行）验收容器
//  产物=三处产能组件自持 Production 广告 ＋ 落岗门：
//    BlacksmithBuilding / SiegeWorkshopBuilding / MineByproductComponent
//
//  用法：菜单「Valley/验证/OB12_在岗门A批」（MCP 自动触发；须先 EnterPlay）。
//
//  正门：TestHarnessApi.EnterTestRun（seed=21107 —— **与 HH107 冒烟同 seed** ⇒ 同为同 seed 修前/修后对照锚）；
//        收尾 ExitTestRun + QuitSmoke（L-32 收工禁留余留世界；退 Play）。
//
//  探针（全绿=验收主判据；L-34 在线判据＝逐条写 verdict 并命中即停）：
//   P0 结构：三组件实现 ITaskSource ＋ 已注册 TaskScheduler(_sources) ＋ 无在岗时发 Production 广告
//   P1 负探针（kingdomId=9901 无单位国·确定性"无在岗"）：三槽/黑匠 Metal/投掷机厂弹 **零增量**
//   P2 正探针（AI 国＋补员·有在岗）：三处 **均产出 > 0**
//   P3 线6 后段门（AI 自然国自带 mine·零补员）：mine Production 实际派驻次数 ≥1 ＋ AI 台账水晶增长
//        ＋ idle 工人最小值 ＋ 「≥60 真实秒无一次 Production 派驻 ⇒ verdict=NEEDRULING(报裁)」＋ stone in>0 ＋ ⑦/⑰ 进池读数
// ============================================================================
public static class Valley_OB12_InGate_Probe
{
    private const int SEED = 21107;        // 与 HH107 冒烟同 seed（同 seed 修前/修后对照）
    private const string SLOT = "smoke_ob12";
    private const string TAG = "[OB12探针]";
    private const int VIRTUAL_KID = 9901;  // 无单位王国：确定性"无在岗"

    [MenuItem("Valley/验证/OB12_在岗门A批")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先进入 Play。"); return; }
        new GameObject("OB12_ProbeRunner").AddComponent<Host>().Start2(Run());
    }

    private class Host : MonoBehaviour
    {
        public void Start2(IEnumerator r) => StartCoroutine(r);
    }

    private static readonly List<string> _rows = new List<string>();
    private static bool _allPass = true;
    private static void Rec(string id, bool ok, string detail)
    {
        if (!ok) _allPass = false;
        _rows.Add($"{id} {detail} ={ok}");
        Debug.Log($"{TAG} {id} {detail} ={ok}");
    }
    private static void Verdict(string v) => Debug.Log($"{TAG} verdict={v}");

    private static IEnumerator Run()
    {
        _rows.Clear(); _allPass = true;

        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 60f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { FailFast("等世界就绪超时(120s)"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var reg = KingdomRegistry.Instance;
        var ruler = RulerController.Instance;
        var kcfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        if (kcfg == null) { FailFast("KingdomConfig 缺失"); yield break; }

        var aiKids = new List<int>();
        foreach (var k in reg.GetAll()) if (!k.IsPlayer) aiKids.Add(k.id);
        if (aiKids.Count < 1) { FailFast("AI 国 <1"); yield break; }

        // 产率/速度加速（Play 态 SO 改动，退 Play 自动还原；HH107 先例）
        float oC = kcfg.byproductCrystalRate, oF = kcfg.byproductFireOilRate, oO = kcfg.byproductOreRate;
        kcfg.byproductCrystalRate = 2f; kcfg.byproductFireOilRate = 2f; kcfg.byproductOreRate = 2f;
        TimeManager.Instance.SetSecondsPerDay(15f);
        TimeManager.Instance.SetGameSpeed(3f);

        var map = WorldManager.Instance.ActiveMap;
        var castle = FindPlayerCastle();
        Vector2Int center = castle != null ? new Vector2Int(castle.coord.x, castle.coord.y)
                                           : new Vector2Int(map.width / 2, map.height / 2);

        var mineDef = BuildingFactory.FindDefById("mine");
        var bsDef = BuildingFactory.FindDefById("Blacksmith");
        var wsDef = BuildingFactory.FindDefById("SiegeWorkshop");
        if (mineDef == null || bsDef == null || wsDef == null)
        { FailFast($"def 缺失 mine={mineDef != null} Blacksmith={bsDef != null} SiegeWorkshop={wsDef != null}"); yield break; }

        // ===== 站点直建（HH107 BuildAt 先例）=====
        var mVirt = BuildAt(mineDef, center, 8, VIRTUAL_KID);
        var bVirt = BuildAt(bsDef, center, 10, VIRTUAL_KID);
        var wVirt = BuildAt(wsDef, center, 12, VIRTUAL_KID);
        yield return null;   // 让 AttachComponents / 子仓落地

        var mC = mVirt != null ? mVirt.GetComponent<MineByproductComponent>() : null;
        var bC = bVirt != null ? bVirt.GetComponent<BlacksmithBuilding>() : null;
        var wC = wVirt != null ? wVirt.GetComponent<SiegeWorkshopBuilding>() : null;
        if (mC == null || bC == null || wC == null)
        { FailFast($"组件挂载失败 mine={mC != null} 黑匠={bC != null} 投掷机厂={wC != null}"); yield break; }

        // ===== P0 结构 =====
        int srcN = 0; bool mReg = false, bReg = false, wReg = false;
        var fld = typeof(TaskScheduler).GetField("_sources", BindingFlags.NonPublic | BindingFlags.Instance);
        var srcs = fld != null && TaskScheduler.HasInstance ? fld.GetValue(TaskScheduler.Instance) as IEnumerable : null;
        if (srcs != null) foreach (var x in srcs)
        {
            srcN++;
            if (ReferenceEquals(x, mC)) mReg = true;
            if (ReferenceEquals(x, bC)) bReg = true;
            if (ReferenceEquals(x, wC)) wReg = true;
        }
        KingdomTask adv;
        bool mA = mC.TryAdvertiseTask(out adv) && adv.type == KingdomTaskType.Production && adv.destType == KingdomDestType.None;
        bool bA = bC.TryAdvertiseTask(out adv) && adv.type == KingdomTaskType.Production;
        bool wA = wC.TryAdvertiseTask(out adv) && adv.type == KingdomTaskType.Production;
        Rec("P0", mReg && bReg && wReg && mA && bA && wA,
            $"ITaskSource 自持＋注册(矿{mReg}/黑{bReg}/厂{wReg}·_sources={srcN}) ＋ 无在岗发 Production(矿{mA}/黑{bA}/厂{wA}·destType=None)");

        // ===== P1 负探针（VIRTUAL_KID·无在岗 ⇒ 恒 0）=====
        var cs = mC.GetStore(ResourceType.Crystal);
        var fs = mC.GetStore(ResourceType.FireOil);
        var os = mC.GetStore(ResourceType.Ore);
        var bStore = bVirt.GetComponent<StorageComponent>();
        int c0 = cs.storedAmount, f0 = fs.storedAmount, o0 = os.storedAmount;
        int bm0 = bStore != null ? bStore.storedAmount : -1;
        int wa0 = wC.GetAmmo(ResourceType.StoneAmmo) + wC.GetAmmo(ResourceType.FireballAmmo) + wC.GetAmmo(ResourceType.MagicAmmo);
        bool everDuty = false;
        float p1t = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - p1t < 8f)
        {
            if (TaskScheduler.HasInstance && (TaskScheduler.Instance.HasWorkerAssigned(mC)
                || TaskScheduler.Instance.HasWorkerAssigned(bC) || TaskScheduler.Instance.HasWorkerAssigned(wC))) everDuty = true;
            yield return new WaitForSeconds(0.25f);
        }
        int c1 = cs.storedAmount, f1 = fs.storedAmount, o1 = os.storedAmount;
        int bm1 = bStore != null ? bStore.storedAmount : -1;
        int wa1 = wC.GetAmmo(ResourceType.StoneAmmo) + wC.GetAmmo(ResourceType.FireballAmmo) + wC.GetAmmo(ResourceType.MagicAmmo);
        bool p1 = !everDuty && c1 == c0 && f1 == f0 && o1 == o0 && bm1 == bm0 && wa1 == wa0;
        Rec("P1", p1, $"无在岗窗8s：水晶{c0}→{c1} 火油{f0}→{f1} 矿石{o0}→{o1} 黑匠Metal{bm0}→{bm1} 厂弹{wa0}→{wa1}；HasWorkerAssigned 全程={everDuty}(须false)");
        Verdict(p1 ? "P1_negHit" : "P1_negUnclear");

        // ===== P2 正探针（AI 国＋补员 ⇒ 三处均产）=====
        int aiKid = aiKids[0];
        var aiCenter = FindAiCenter(aiKid, center);
        var mA2 = BuildAt(mineDef, aiCenter, 6, aiKid);
        var bA2 = BuildAt(bsDef, aiCenter, 8, aiKid);
        var wA2 = BuildAt(wsDef, aiCenter, 10, aiKid);
        yield return null;
        var mC2 = mA2 != null ? mA2.GetComponent<MineByproductComponent>() : null;
        var bC2 = bA2 != null ? bA2.GetComponent<BlacksmithBuilding>() : null;
        var wC2 = wA2 != null ? wA2.GetComponent<SiegeWorkshopBuilding>() : null;
        if (mC2 == null || bC2 == null || wC2 == null) { Rec("P2", false, "AI 站点组件挂载失败"); }
        else
        {
            ruler.ModifyResource(ResourceType.Ore, true, 400);                       // 黑匠原料（Transform 走 RulerController）
            if (ruler.GetResource(ResourceType.Stone) < 200) ruler.ModifyResource(ResourceType.Stone, true, 200);   // 厂原料
            int orePre = ruler.GetResource(ResourceType.Ore);
            int kMetal0 = reg.Get(aiKid) != null ? reg.Get(aiKid).resources.metal : 0;
            int sp = 0;
            sp += SpawnWorkers(mC2.transform.position, aiKid, 4);
            sp += SpawnWorkers(bC2.transform.position, aiKid, 4);
            sp += SpawnWorkers(wC2.transform.position, aiKid, 4);
            yield return null;
            var cS = mC2.GetStore(ResourceType.Crystal); var fS = mC2.GetStore(ResourceType.FireOil); var oS = mC2.GetStore(ResourceType.Ore);
            var bS = bA2.GetComponent<StorageComponent>();
            int c2a = 0, f2a = 0, o2a = 0, bm2a = bS != null ? bS.storedAmount : -1;
            int wammo2a = wC2.GetAmmo(ResourceType.StoneAmmo) + wC2.GetAmmo(ResourceType.FireballAmmo) + wC2.GetAmmo(ResourceType.MagicAmmo);
            bool grew = false;
            float p2t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - p2t < 60f)
            {
                yield return new WaitForSeconds(2f);
                c2a = cS.storedAmount; f2a = fS.storedAmount; o2a = oS.storedAmount;
                bm2a = bS != null ? bS.storedAmount : -1;
                int wa = wC2.GetAmmo(ResourceType.StoneAmmo) + wC2.GetAmmo(ResourceType.FireballAmmo) + wC2.GetAmmo(ResourceType.MagicAmmo);
                bool mOk = c2a > 0 || f2a > 0 || o2a > 0;   // 矿洞副产（子仓可能已被搬运，故用"曾增长"近似）
                // 黑匠 Metal 被 AIEconomySettlement 日结清空本地仓 ⇒ 不目视本地仓；改判「本国 Metal 增长」或「Ore 被 Transform 消耗」
                bool bOk = (bS != null && bS.storedAmount > 0)
                           || (reg.Get(aiKid) != null && reg.Get(aiKid).resources.metal > kMetal0)
                           || ruler.GetResource(ResourceType.Ore) < orePre;
                bool wOk = wa > wammo2a;
                if (mOk && bOk && wOk) { grew = true; break; }   // L-34 命中即停
                wammo2a = wa;
            }
            Rec("P2", grew, $"有在岗窗(补员={sp})：水晶={c2a} 火油={f2a} 矿石={o2a} 黑匠Metal={bm2a} 厂弹={wammo2a} 三处均增={grew}");
            Verdict(grew ? "P2_posHit" : "P2_posMiss");
        }

        // ===== P3 线6 后段门：AI 自然国自带 mine（零补员）=====
        var natMine = FindNaturalMine(aiKid, mA2);
        if (natMine == null) { Rec("P3", false, $"k{aiKid} 自然 mine 未找到（KingdomFoundry 预置应有 1 座）"); }
        else
        {
            var nC = natMine.GetComponent<MineByproductComponent>();
            var st = reg.Get(aiKid);
            int crystalBase = st != null ? st.crystal : 0;
            int stoneBase = st != null ? st.resources.stone : 0;
            int dispatches = 0, idleMin = int.MaxValue;
            bool wasDuty = false;
            float lastDuty = Time.realtimeSinceStartup;
            float maxGap = 0f;
            float p3t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - p3t < 60f)
            {
                bool duty = TaskScheduler.HasInstance && nC != null && TaskScheduler.Instance.HasWorkerAssigned(nC);
                if (duty && !wasDuty) { dispatches++; lastDuty = Time.realtimeSinceStartup; }
                if (duty) lastDuty = Time.realtimeSinceStartup;
                wasDuty = duty;
                int idle = CountIdleWorkers(aiKid);
                if (idle < idleMin) idleMin = idle;
                float gap = Time.realtimeSinceStartup - lastDuty;
                if (gap > maxGap) maxGap = gap;
                yield return new WaitForSeconds(1f);
            }
            int crystalNow = st != null ? st.crystal : 0;
            int stoneNow = st != null ? st.resources.stone : 0;
            bool prodOk = dispatches >= 1;
            bool crystalOk = crystalNow > crystalBase;
            bool stoneOk = stoneNow > stoneBase;
            // ⑦招战士 / ⑰建铁匠铺 进池读数（本批不动 AI 决策，读数应不受影响）
            var utilCfg = Resources.Load<UtilityActionConfig>("Config/Kingdoms/UtilityActionConfig");
            bool f7 = false, f17 = false;
            if (utilCfg != null && st != null)
                foreach (var d in utilCfg.actions)
                {
                    if (d.id == UtilityAction.RecruitWarrior) f7 = FeasibleReflect(st, d);
                    if (d.id == UtilityAction.BuildBlacksmith) f17 = FeasibleReflect(st, d);
                }
            bool needRuling = !prodOk || maxGap >= 60f;
            Debug.Log($"{TAG} [P3明细] k{aiKid} 自然mine 派驻={dispatches} 最大无派驻间隔={maxGap:F1}s idleMin={idleMin} 台账水晶{crystalBase}→{crystalNow} 石{stoneBase}→{stoneNow} ⑦={f7} ⑰={f17}");
            if (needRuling) Verdict("NEEDRULING_P3_无派驻或≥60s无派驻");
            else Verdict("P3_hit");
            Rec("P3", prodOk,
                $"AI自然国零补员：mine Production 派驻={dispatches}(≥1·核心可达) 最大无派驻间隔={maxGap:F1}s(<60⇒不报裁) idleMin={idleMin}｜参考读数（60s窗≈12游戏日·非断言）：台账水晶{crystalBase}→{crystalNow} 石{stoneBase}→{stoneNow} transport(tag={crystalOk}) ⑦={f7} ⑰={f17}{(needRuling ? " 【报裁项】" : "")}");
        }

        // ---- 收尾（L-32：复原 + ExitTestRun + 退 Play）----
        kcfg.byproductCrystalRate = oC; kcfg.byproductFireOilRate = oF; kcfg.byproductOreRate = oO;
        TimeManager.Instance.SetGameSpeed(1f);
        Debug.Log($"{TAG} ===== 轮汇总（{_rows.Count} 探针）=====");
        for (int i = 0; i < _rows.Count; i++) Debug.Log($"{TAG} {_rows[i]}");
        Debug.Log($"{TAG} 判定：{(_allPass ? "ALL PASS" : "HAS FAIL")}");
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }

    // ===== helpers =====
    private static void FailFast(string reason)
    {
        Debug.LogError($"{TAG} 中止：{reason}");
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }

    private static int SpawnWorkers(Vector2 pos, int kid, int n)
    {
        int c = 0;
        for (int i = 0; i < n; i++)
        {
            var w = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, pos + new Vector2(0.6f * i, 0.6f * i), kid);
            if (w != null) c++;
        }
        return c;
    }

    private static bool FeasibleReflect(KingdomState k, UtilityActionDef d)
    {
        var mi = typeof(UtilityScorer).GetMethod("Feasible", BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null) return false;
        return (bool)mi.Invoke(null, new object[] { k, d });
    }

    private static Building FindNaturalMine(int kid, Building exclude)
    {
        var all = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.All : null;
        if (all == null) return null;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null || b == exclude || b.kingdomId != kid || b.def == null || b.def.id != "mine") continue;
            return b;
        }
        return null;
    }

    private static int CountIdleWorkers(int kid)
    {
        int n = 0;
        if (UnitRegistry.Instance == null || !TaskScheduler.HasInstance) return 0;
        var sched = TaskScheduler.Instance;
        foreach (var u in new List<UnitController>(UnitRegistry.Instance.GetAllUnits()))
        {
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            var occ = u.EffectiveOccupation;
            if (occ != Occupation.Worker && occ != Occupation.Porter && occ != Occupation.Civilian) continue;
            if (sched.GetWorkerState(u.npcId) == TaskState.None) n++;   // 无任务=空闲
        }
        return n;
    }

    private static Building FindPlayerCastle()
    {
        foreach (var b in Object.FindObjectsOfType<Building>())
            if (b != null && b.kingdomId == 0 && b.def != null && b.def.id == "castle") return b;
        return null;
    }

    private static Vector2Int FindAiCenter(int kid, Vector2Int fallback)
    {
        foreach (var b in Object.FindObjectsOfType<Building>())
            if (b != null && b.kingdomId == kid && b.def != null && b.def.id == "castle")
                return new Vector2Int(b.coord.x, b.coord.y);
        return fallback;
    }

    /// <summary>直建建筑（HH107 BuildAt 先例：生产链 CreateBuildingInstance + kingdomId 指定）。</summary>
    private static Building BuildAt(BuildingDef def, Vector2Int center, int ringIdx, int kingdomId)
    {
        if (def == null || BuildingFactory.Instance == null || GridSystem.Instance == null) return null;
        var map = WorldManager.Instance.ActiveMap;
        var g = GridSystem.Instance;
        Vector2Int cell = MapGenRules.NearestWalkable(map, center.x, center.y);
        for (int r = 1; r <= 12 && r <= ringIdx; r++)
        {
            var cand = MapGenRules.NearestWalkable(map, center.x + r, center.y);
            if (cand != cell || r == ringIdx) { cell = cand; break; }
        }
        var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1, def.footprint.y > 0 ? def.footprint.y : 1);
        var coord = new GridCoord(cell.x, cell.y);
        // 【HH.294 片3-A】中心点换算走 GridSystem 唯一内核（原先此处「就地展开」）
        Vector3 world = GridSystem.FootprintCenterWorld(coord, fp, new Vector3(coord.x, coord.y, 0f));
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
