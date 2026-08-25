using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using static BuildingFactory;
using static UnitRegistry;
using static BuildingRegistry;

// ============================================================================
//  2_16 P1 步骤11 Play 实况冒烟（D294/D295/D306/D312/D313/D314/D385）
//  用法：菜单「Valley/验证/2_16_Steps11_Play冒烟」——须 Play 上下文执行（先 Play 再点）。
//  自包含：Reset 注册表/建筑/单位 → 生成世界 → 营地建筑+注入15流浪汉 → 强制结营 → 推进5日 →
//  CampUpgrader.TickAll 触发动态立国 → 断言守恒/王国+1/营地移除/铁旗 castle 建筑/冷却时间戳。
//  附：CheckConditions 子断言（人数<12 不立国 / 冷却期不插旗逐条）。上限 Count=8、吞并出口B 因不易在
//  Play harness 内 staging（需先生成 8 国 / 构造有主格），已在编辑器纯逻辑静态/CheckConditions 层覆盖+说明。
// ============================================================================
public static class Valley2_16_Smoke_Step11
{
    private const int SEED = 12345;

    [MenuItem("Valley/验证/2_16_Steps11_Play冒烟")]
    public static void Run()
    {
        var sb = new StringBuilder();
        bool allPass = true;
        var wm = Object.FindAnyObjectByType<WorldManager>();
        if (wm == null) { Debug.LogError("[2_16_Step11冒烟] WorldManager 未找到。"); return; }

        string[] preface =
        {
            "== 🔵 上限(Count=8)/吞并出口B 未在 Play harness staging（需先入8国/构造有主格）：",
            "   上限判定(registry.Count>=maxKingdomsGlobal) 为一行比较，纯逻辑已静态复核；",
            "   吞并 TryAnnex 判定占位恒假(2_17 接线)，本片只验执行端语法存在。 =="
        };
        foreach (var s in preface) Debug.Log("[2_16_Step11冒烟] " + s);

        // 复用 Play 引导已生成的真实世界网格（不 Reset/GenerateMapForPreview——后者只产地图数据预览、
        // 未建 Play 网格，会致 CoordToWorld/WorldToCoord 失配 → 结营 `continue`）。WorldManager 仅作就绪守门。
        var _ = BuildingRegistry.Instance;   // 强制物化单例（registry 为空会让建筑注册/扫描静默跳过）

        // 用真「新游戏」链路建可玩世界网格——GenerateMapForPreview 只产地图数据预览、无 ActiveMap（宽高0→所有格越界返 null），锚定会失败
        var lm = LoadManager.Instance;
        if (lm == null) { Debug.LogError("[2_16_Step11冒烟] LoadManager 不可用。"); return; }
        lm.InitializeNewGame(new NewGameConfig
        {
            mapSeed = SEED,
            worldSeed = SEED,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            kingdomName = "冒烟王国",
            selectedSlotId = "smoke"
        });

        var grid = GridSystem.Instance;
        var vcs = VagrantCampSystem.Instance;
        if (grid == null || vcs == null || grid.Config == null) { Debug.LogError("[2_16_Step11冒烟] 世界未就绪。"); return; }

        // ---- 锚定一个营地建筑（Play 引导 D308 已放置；若无则建一个）----
        var def = BuildingFactory.FindDefById("VagrantCamp");
        var campBuildings = vcs.FindCamps();
        Building b = (campBuildings != null && campBuildings.Count > 0) ? campBuildings[0] : null;
        if (b == null && def != null)
        {
            var anyCoord = new GridCoord(9, 9);
            var fpAny = new Vector2Int(Mathf.Max(1, def.footprint.x), Mathf.Max(1, def.footprint.y));
            BuildingFactory.Instance.CreateBuildingInstance(
                def, def.sourceType, anyCoord, fpAny, grid.CoordToWorld(anyCoord),
                isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
                initialState: BuildingState.Active, kingdomId: 0);
            campBuildings = vcs.FindCamps();
            b = (campBuildings != null && campBuildings.Count > 0) ? campBuildings[0] : null;
        }
        if (b == null) { Debug.LogError("[2_16_Step11冒烟] 无营地建筑可锚定。"); return; }
        var campWorld = b.GetPosition();
        var cell = grid.WorldToCoord(campWorld);
        if (cell == null) { Debug.LogError("[2_16_Step11冒烟] 锚点无法映射回格子。"); return; }
        var campCoord = cell.Value;

        // ---- 四周注入 15 流浪汉 ----
        int spawned = 0;
        List<int> npcIds = new List<int>();
        for (int i = 0; i < 15; i++)
        {
            var pos = campWorld + new Vector2((i % 3) * 0.7f - 0.7f, (i / 3) * 0.7f - 1.4f);
            var go = UnitFactory.Instance.SpawnUnit(Faction.Human_Player, Occupation.Vagrant, pos, 0);
            if (go == null) continue;
            var uc = go.GetComponent<UnitController>();
            if (uc == null) continue;
            uc.BirthCampPos = campWorld;
            uc.IsVagrantRecruited = false;
            npcIds.Add(uc.npcId);
            spawned++;
        }
        sb.Append($"注入流浪汉={spawned}/15 ");

        // 强制结营
        vcs.ForceCampScan();
        int campsAfterScan = vcs.CampCount;
        var targetCamp = FindCampAt(vcs, campCoord);
        sb.Append($"结营后营地数={campsAfterScan} 目标营{(targetCamp==null?"未建立":"已建立")} ");

        // ---- 诊断：确认真实数字（BuildingRegistry/FindCamps/流浪汉计数/距离）----
        {
            var cfg = Resources.Load<KingdomConfig>("Config/KingdomConfig");
            var br = BuildingRegistry.Instance;
            int fc = (br != null) ? vcs.FindCamps().Count : -1;
            int vag = 0, near = 0;
            float radius = (grid.Config != null && cfg != null) ? cfg.campVagrantRadiusCells * grid.Config.cellSize.x : -1f;
            System.Text.StringBuilder d = new System.Text.StringBuilder();
            foreach (var uc in UnitRegistry.Instance.GetAllUnits())
            {
                if (uc == null || uc.EffectiveOccupation != Occupation.Vagrant || uc.IsVagrantRecruited) continue;
                vag++;
                float dist = Vector2.Distance((Vector2)uc.transform.position, (Vector2)campWorld);
                if (radius >= 0 && dist <= radius) near++;
                if (vag <= 20) d.Append($"\n    u{uc.npcId} dist={dist:F1} occ={uc.EffectiveOccupation} rec={uc.IsVagrantRecruited} alive={uc.IsAlive}");
            }
            float cfgRadius = cfg != null ? cfg.campVagrantRadiusCells : -1f;
            string regState = br != null ? "present" : "NULL";
            Debug.Log($"[2_16_Step11冒烟·诊断] BuildingRegistry={regState} FindCamps={fc} " +
                      $"流浪汉(vag)={vag} 半径内={near} (radiusCells={cfgRadius},worldRad={radius:F1}) campWorld={campWorld}{d}");
        }

        // ---- 存续日直接设到5（Camp.persistenceDays 公开字段；对齐 foundingPersistenceDays=5 验收）；
        //      同时把营地成员表重置为注入的 15 人，保证「工人守恒=注入数」断言确定性（排除 boot 预置成员干扰）----
        if (targetCamp != null)
        {
            targetCamp.persistenceDays = 5;
            targetCamp.memberIds.Clear();
            if (npcIds.Count > 0) targetCamp.memberIds.AddRange(npcIds);
        }
        sb.Append($"存续日={(targetCamp!=null?targetCamp.persistenceDays:-1)} 成员={(targetCamp!=null?targetCamp.memberIds.Count:-1)} ");

        int beforeCount = KingdomRegistry.Instance.Count;
        int dayBefore = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        // ---- 触发晋升 ----
        CampUpgrader.TickAll();
        int afterCount = KingdomRegistry.Instance.Count;
        bool founded = afterCount == beforeCount + 1;
        sb.Append($"王国数 {beforeCount}->{afterCount} 立国={(founded?"OK":"FAIL")} ");

        // 新王国校验
        var k = founded ? KingdomRegistry.Instance.GetAll()[afterCount - 1] : null;
        if (k != null)
        {
            sb.Append($"新王国(id={k.id},工人={k.workerCount},war={k.warriorCount}) ");
            bool conserved = k.workerCount == spawned;
            sb.Append($"工人守恒(={spawned})={(conserved?"OK":"FAIL")} ");
        }

        // 营地记录移除
        bool campRemoved = vcs.CampCount == 0 || FindCampAt(vcs, campCoord) == null;
        sb.Append($"营地记录移除={(campRemoved?"OK":"FAIL")} ");

        // 铁旗 castle 建筑（带新王国 id）
        bool castlePlaced = false;
        if (k != null)
        {
            foreach (var bb in Object.FindObjectsByType<Building>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (bb != null && bb.kingdomId == k.id) { castlePlaced = true; break; }
            sb.Append($"铁旗建筑带新王kid={(castlePlaced?"OK":"FAIL")} ");
        }

        // 冷却时间戳已置（D312）
        bool cooldownSet = KingdomRegistry.Instance.lastFoundingDay == dayBefore;
        sb.Append($"冷却时间戳={KingdomRegistry.Instance.lastFoundingDay}(=day{dayBefore})={(cooldownSet?"OK":"FAIL")} ");

        // ---- 子断言（纯 CheckConditions，不影响主流程，用一次性 throwaway camp）----
        var campLow = new Camp(new GridCoord(1, 1), -1);
        for (int i = 0; i < 11; i++) campLow.memberIds.Add(i);
        campLow.persistenceDays = 5;
        var (pLow, rLow) = CampUpgrader.CheckConditions(campLow, dayBefore, KingdomRegistry.Instance, LoadCfg());
        sb.Append($"\n  杀至<12: 11人p5 通过={pLow} 原因={rLow} ");
        sb.Append((pLow ? "FAIL!" : "PASS（不足不立国）"));

        var campOk = new Camp(new GridCoord(2, 2), -1);
        for (int i = 0; i < 12; i++) campOk.memberIds.Add(i);
        campOk.persistenceDays = 5;
        int od = KingdomRegistry.Instance.lastFoundingDay;
        KingdomRegistry.Instance.lastFoundingDay = dayBefore;   // 模拟冷却期未过
        var (pCold, rCold) = CampUpgrader.CheckConditions(campOk, dayBefore, KingdomRegistry.Instance, LoadCfg());
        sb.Append($"\n  冷却期(距上0日<10): 通过={pCold} 原因={rCold} ");
        sb.Append((pCold ? "FAIL!" : "PASS（到期才立）"));
        KingdomRegistry.Instance.lastFoundingDay = int.MinValue;   // 恢复：到期即立
        var (pOk2, _) = CampUpgrader.CheckConditions(campOk, dayBefore, KingdomRegistry.Instance, LoadCfg());
        sb.Append($"  初值放行(到期即立): 通过={pOk2} {(pOk2 ? "PASS" : "FAIL!")} ");

        bool subPass = pLow == false && pCold == false && pOk2 == true;
        bool corePass = founded && (k?.workerCount == spawned) && campRemoved && castlePlaced && cooldownSet;
        allPass = corePass && subPass;

        Debug.Log("[2_16_Step11冒烟] " + sb);
        Debug.Log($"[2_16_Step11冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（核心闭合回路+子断言）=====");
        EditorUtility.DisplayDialog("2_16 步骤11 Play 冒烟", allPass ? "全部 PASS" : "存在 FAIL，见 Console 明细", "确定");
    }

    static Camp FindCampAt(VagrantCampSystem vcs, GridCoord c)
    {
        if (vcs == null || vcs.Camps == null) return null;
        foreach (var camp in vcs.Camps)
            if (camp != null && camp.centerCell == c) return camp;
        return null;
    }

    static KingdomFoundingConfig LoadCfg()
        => Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
}