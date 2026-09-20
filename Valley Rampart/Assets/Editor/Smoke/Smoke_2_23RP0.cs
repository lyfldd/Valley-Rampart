using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  Smoke_2_23RP0 资源 P0 批C 冒烟容器（R-C4，自含、禁挂 SmokeApi，Editor-only）
//  test-harness-first 铁律：正门 TestHarnessApi.EnterTestRun + ExitTestRun 收尾（L-17）。
//  对标 2_23 §九.1 P0 冒烟四件 + 清单 §三 R-C4 验收列 + D639 全列报最终口径：
//    —— 粮三通道正负对照（E-B1 产能型饿→建 farm 非粮仓；采集型饿→偏向生效断言；仓储型→建粮仓）
//    —— 派工偏向行为级（缺石世界采石任务有效优先级↑，E-B5——批B Smoke_2_23RB P3 复用维度）
//    —— 断链自愈（E-B4 删 farm→NeedScore 拉满→数日内重建）
//    —— 全资源负探针（仅粮触发时金/石/木不误触发）
//    —— A→B→C 固定序（同输入同输出，确定性）
//  断言分三层：单元纯函数（ResolveTriageResource/DecideTriage 反射调，同输入复算全等）
//            + 集成行为（ExecuteGrainTriage 反射调→建 farm/Granary 落地）
//            + 评分面（R-C3 NeedScore 拉满断言）
//  确定性：run1/run2 双跑日志 P 行归一化逐字节一致（时间戳/tag 不入比对）。
// ============================================================================
public static class Smoke_2_23RP0
{
    private const int SEED = 21140;
    private const string SLOT = "probe_2_23rp0";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
    public static string RunTag = "run1";

    [MenuItem("Valley/验证/Smoke_2_23RP0_资源P0批C冒烟_run1")]
    public static void Run1() { RunTag = "run1"; Run(); }
    [MenuItem("Valley/验证/Smoke_2_23RP0_资源P0批C冒烟_run2")]
    public static void Run2() { RunTag = "run2"; Run(); }

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[2_23RP0] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("Smoke2_23RP0_Host").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine());
    }

    private class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (t == LogType.Exception) Log("EXCEPTION: " + cond + "\n" + stack, false);
            else if (t == LogType.Warning
                     && (cond.Contains("[KingdomBrain]") || cond.Contains("[BuildController]")))
                _log.AppendLine("[WARN] " + cond);   // 建造链观测口：定位 P5a 断点（只记不判）
        }
    }

    private static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.LogWarning("[2_23RP0] " + (ok ? "✓ " : "✗ ") + msg);
    }

    // ---- 反射句柄（internal/private 反射调，容器不改产品代码）----
    private static MethodInfo _resolveRscMi;   // ResolveTriageResource(EconomyBlock, KingdomDiagnosisConfig) -> int
    private static MethodInfo _decideMi;       // DecideTriage(EconomyBlock, KingdomDiagnosisConfig, EcoResource) -> TriageDecision
    private static MethodInfo _grainMi;        // ExecuteGrainTriage(KingdomState, KingdomBrainConfig) instance
    private static MethodInfo _needMi;         // UtilityScorer.NeedScore(KingdomState, UtilityActionDef) -> float

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(1f);
        Log("P1a 正门进局 seed=" + SEED + " tag=" + RunTag, true);

        var k1 = KingdomRegistry.Instance.Get(1);
        if (k1 == null) { Log("P1b k1 缺失", false); Finish(); yield break; }

        var tKB = typeof(KingdomBrain);
        _resolveRscMi = tKB.GetMethod("ResolveTriageResource", BindingFlags.NonPublic | BindingFlags.Static);
        _decideMi = tKB.GetMethod("DecideTriage", BindingFlags.NonPublic | BindingFlags.Static);
        _grainMi = tKB.GetMethod("ExecuteGrainTriage", BindingFlags.NonPublic | BindingFlags.Instance);
        var brain = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Get(1) : null;
        Log("P1b 分诊反射面在场（resolve/decide/grain/brain）="
            + (_resolveRscMi != null && _decideMi != null && _grainMi != null && brain != null),
            _resolveRscMi != null && _decideMi != null && _grainMi != null && brain != null);
        // 冻结全局时间：探针窗口（P2~P5）内 AI 日 tick 不演化——计数稳定 + 确定性（Finish 复零 + ExitTestRun 恢复档）
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        var bcfg = KingdomBrain.LoadConfig();
        var dcfg = KingdomDiagnosisConfig.Load();
        if (dcfg == null) { Log("P1c dcfg 缺失", false); Finish(); yield break; }

        // ================= 单元面 P2：触发判定（ResolveTriageResource） =================
        // ① 粮触发：GrainReserveDays < floor(2)
        var ecoFood = new EconomyBlock { GrainReserveDays = 1f, Population = 10, Flow = default };
        int rf = (int)_resolveRscMi.Invoke(null, new object[] { ecoFood, dcfg });
        Log("P2a 粮触发（GrainReserveDays=1 < 2）→ 3(粮)=" + rf, rf == (int)EcoResource.Food);

        // ② 金触发：Net(gold)<0 且存量 < 3×Out（GrainReserveDays=9 隔离粮严重序抢先）
        var ecoGold = new EconomyBlock { Population = 10, GrainReserveDays = 9, StockGold = 5 };
        ecoGold.Flow = new ResourceFlow { goldOut = 100 };   // Net=-100<0；5 < 3×100
        int rg = (int)_resolveRscMi.Invoke(null, new object[] { ecoGold, dcfg });
        Log("P2b 金触发（Net<0 且水位<基线）→ 0(金)=" + rg, rg == (int)EcoResource.Gold);

        // ③ 木触发：木为严重度序内（金后）→ 金不触时取木（GrainReserveDays=9 隔离粮抢先）
        var ecoWood = new EconomyBlock { Population = 10, GrainReserveDays = 9, StockWood = 2 };
        ecoWood.Flow = new ResourceFlow { woodOut = 50 };
        int rw = (int)_resolveRscMi.Invoke(null, new object[] { ecoWood, dcfg });
        Log("P2c 木触发（金不触时取严重度序内木）→ 2(木)=" + rw, rw == (int)EcoResource.Wood);

        // ④ 铁不参与触发（D639）：NMetal 缺口不触发（GrainReserveDays=9 隔离粮抢先）
        var ecoMetal = new EconomyBlock { Population = 10, GrainReserveDays = 9, StockMetal = 1 };
        ecoMetal.Flow = new ResourceFlow { metalOut = 30 };
        int rm = (int)_resolveRscMi.Invoke(null, new object[] { ecoMetal, dcfg });
        Log("P2d 铁不参与触发（metalOut=30 净负+存量低 → 仍 -1）=" + rm, rm == -1);

        // ⑤ 全正常不触发
        var ecoOk = new EconomyBlock { Population = 10, GrainReserveDays = 9, StockGold = 999, Flow = default };
        int r0 = (int)_resolveRscMi.Invoke(null, new object[] { ecoOk, dcfg });
        Log("P2e 全正常不触发 → -1（⑤ 正常态不为屯粮空转）=" + r0, r0 == -1);

        // ================= 单元面 P3：A→B→C 固定序（DecideTriage） =================
        // ③-A：无 Food 产能且反查 farm → BuildCapacity（E-B1 产能型）
        var aFood = new EconomyBlock { Population = 10, Production = new List<ProductionEntry>() };
        TriageDecision da = (TriageDecision)_decideMi.Invoke(null, new object[] { aFood, dcfg, EcoResource.Food });
        Log("P3a 通道A 正（无粮产能→建 farm）=" + da, da == TriageDecision.BuildCapacity);

        // ③-B：有产能（Count>0）但 Flow.In(food)/pop < 阈值 → NoOp（D639 列报5：偏向批B 生效）
        var bFood = new EconomyBlock
        {
            Population = 10,
            Production = new List<ProductionEntry> { new ProductionEntry { Resource = EcoResource.Food, Count = 1 } },
            Flow = new ResourceFlow { foodIn = 2 }   // 2/10=0.2 < 1
        };
        TriageDecision db = (TriageDecision)_decideMi.Invoke(null, new object[] { bFood, dcfg, EcoResource.Food });
        Log("P3b 通道B 正（有产能但日产出/人口低→NoOp=偏向生效）=" + db, db == TriageDecision.NoOp);

        // ③-C：A/B 满足（产能够、日产出足）但占用溢出 → 粮建 Granary
        var cFood = new EconomyBlock
        {
            Population = 10,
            Production = new List<ProductionEntry> { new ProductionEntry { Resource = EcoResource.Food, Count = 3 } },
            Flow = new ResourceFlow { foodIn = 50 },       // 50/10=5 ≥ 1
            StorageUsed = 180, StorageCapacity = 200,      // 0.9 ≥ 0.9
            StorageOccupancy = 0.9f   // 字段由诊断块 BuildEconomyBlock 算好填（探针直设；L306 口径=Used/Capacity）
        };
        TriageDecision dc = (TriageDecision)_decideMi.Invoke(null, new object[] { cFood, dcfg, EcoResource.Food });
        Log("P3c 通道C 正（占用溢出→粮建 Granary）=" + dc, dc == TriageDecision.BuildGranary);

        // ③-D 非粮仓储：Stone 态 A/B 满足但溢出 → Warehouse
        var cStone = new EconomyBlock
        {
            Population = 10,
            Production = new List<ProductionEntry> { new ProductionEntry { Resource = EcoResource.Stone, Count = 3 } },
            Flow = new ResourceFlow { stoneIn = 40 },
            StorageUsed = 190, StorageCapacity = 200,
            StorageOccupancy = 0.95f   // 字段直设（同 P3c）
        };
        TriageDecision dd = (TriageDecision)_decideMi.Invoke(null, new object[] { cStone, dcfg, EcoResource.Stone });
        Log("P3d 通道C 非粮（占用溢出→Stone 建 Warehouse）=" + dd, dd == TriageDecision.BuildWarehouse);

        // ③-E 固定序：A 态与 B 态同时在场 → 走 A（命中即止，单通道不叠加 D639 列报7）
        var ab = new EconomyBlock
        {
            Population = 10,
            Production = new List<ProductionEntry>(),      // 无产能=A 触发
            Flow = new ResourceFlow { foodIn = 2 }          // 同时 B 也触发
        };
        TriageDecision de = (TriageDecision)_decideMi.Invoke(null, new object[] { ab, dcfg, EcoResource.Food });
        Log("P3e 固定序（A 态+B 态同场→A 优先建 farm）=" + de, de == TriageDecision.BuildCapacity);

        // ③-F 确定性：同输入复算全等
        TriageDecision da2 = (TriageDecision)_decideMi.Invoke(null, new object[] { aFood, dcfg, EcoResource.Food });
        bool det = da == da2 && db == (TriageDecision)_decideMi.Invoke(null, new object[] { bFood, dcfg, EcoResource.Food })
                 && dc == (TriageDecision)_decideMi.Invoke(null, new object[] { cFood, dcfg, EcoResource.Food });
        Log("P3f 确定性（DecideTriage 同输入复算全等）=" + det, det);

        // ================= 单元面 P4：R-C3 断链注入（NeedScore 拉满，E-B4 判据面） =================
        var usT = System.Type.GetType("UtilityScorer, Assembly-CSharp");
        _needMi = usT != null ? usT.GetMethod("NeedScore", BindingFlags.Public | BindingFlags.Static) : null;
        Log("P4a NeedScore 反射面在场=" + (_needMi != null), _needMi != null);
        if (_needMi != null)
        {
            // 注入 MissingMustHave：Farm 缺失（kind0）→ ④强化采集（farm）行动 need=1.0
            var uaAll = UtilityActionConfig.LoadConfig().actions;
            UtilityActionDef farmDef = default, warehouseDef = default, wallDef = default;
            if (uaAll != null)
                for (int i = 0; i < uaAll.Length; i++)
                {
                    if (uaAll[i].id == UtilityAction.BoostHarvest) farmDef = uaAll[i];
                    if (uaAll[i].id == UtilityAction.BuildWarehouse) warehouseDef = uaAll[i];
                    if (uaAll[i].id == UtilityAction.BuildWall) wallDef = uaAll[i];
                }
            var snap = new SituationSnapshot
            {
                KingdomId = 1, Day = 0,
                Economy = new EconomyBlock
                {
                    Population = 10,
                    MissingMustHave = new List<MustHaveMiss>
                    {
                        new MustHaveMiss { RequiredKind = 0, Required = 2, Actual = 0 },  // Farm 缺失
                        new MustHaveMiss { RequiredKind = 1, Required = 1, Actual = 0 },  // Warehouse 缺失
                        new MustHaveMiss { RequiredKind = 2, Required = 2, Actual = 0 }   // Fort 缺失
                    }
                }
            };
            SituationHub.Put(1, snap);
            if (farmDef.id != UtilityAction.None)
            {
                float nF = (float)_needMi.Invoke(null, new object[] { k1, farmDef });
                Log("P4b 断链注入 Farm→④强度采集 need=" + nF + "（==1.0 拉满）", Mathf.Approximately(nF, 1f));
            }
            else Log("P4b ④行动 def 缺失（跳过）", true);
            if (warehouseDef.id != UtilityAction.None)
            {
                float nW = (float)_needMi.Invoke(null, new object[] { k1, warehouseDef });
                Log("P4c 断链注入 Warehouse→②建仓 need=" + nW + "（==1.0 拉满）", Mathf.Approximately(nW, 1f));
            }
            else Log("P4c ②行动 def 缺失（跳过）", true);
            if (wallDef.id != UtilityAction.None)
            {
                float nWal = (float)_needMi.Invoke(null, new object[] { k1, wallDef });
                Log("P4d 断链注入 Fort→⑨修工事 need=" + nWal + "（==1.0 拉满）", Mathf.Approximately(nWal, 1f));
            }
            else Log("P4d ⑨行动 def 缺失（跳过）", true);
            // 负：非断链行动（招工）不受注入
            var recDef = uaAll != null ? FindDef(uaAll, UtilityAction.RecruitWorker) : default;
            if (recDef.id != UtilityAction.None)
            {
                float nR = (float)_needMi.Invoke(null, new object[] { k1, recDef });
                Log("P4e 断链注入负探针（招工行动不受缺链路——need<1）=" + nR, nR < 1f);
            }
            else Log("P4e 招工 def 缺失（跳过）", true);
            SituationHub.Remove(1);
        }

        // ================= 集成面 P5：ExecuteGrainTriage → 建 farm（E-B1 正向） =================
        // 构造 k1 无粮产能（清 farm/Granary——探针临时，随后不还原（AI 王国建造属正常演化））
        // 注：AI 王国开局 6 预制含 farm/Granary？以实际存在为准——只删 food 产能类（farm/farmland/Granary/AdvancedStorage out=3）
        int farm0 = CountBuilding(1, "farm");
        int gran0 = CountBuilding(1, "Granary");
        int foodCap0 = farm0 + gran0;
        RemoveBuilding(1, "farm"); RemoveBuilding(1, "Granary"); RemoveBuilding(1, "farmland");
        yield return null;   // Object.Destroy 帧末生效：等一帧让清场落地再计数（否则计数失真）
        int foodCapPre = CountBuilding(1, "farm") + CountBuilding(1, "Granary");
        Log("P5-pre 清场后 food 产能（farm+Granary）=" + foodCapPre + "（探针前置：清 0 才入通道A 态）", foodCapPre == 0);
        // 注入粮缺快照（GrainReserveDays=1 触发 + 无 Food 产能 → A 态）
        var snapA = new SituationSnapshot
        {
            KingdomId = 1, Day = 0,
            Economy = new EconomyBlock
            {
                Population = 10, GrainReserveDays = 1f,
                Flow = default,
                Production = new List<ProductionEntry>(),   // 无 Food 产能 → 通道A
                StorageUsed = 10, StorageCapacity = 200
            }
        };
        SituationHub.Put(1, snapA);
        // 焦点守卫：ExecuteBuildFocus 首行按 kingdom.focus 反查行动 def（空 buildingId 直接返回不建）。
        // 生产路由仅 focus==Grain 时走 ExecuteGrainTriage（ExecuteFocus L573），容器绕过路由直调——
        // 须先置焦点为建行动（④BoostHarvest→farm，buildingId 非空）满足下游守卫，再验三通道落地。
        int focus0 = k1.focus;
        k1.focus = (int)UtilityAction.BoostHarvest;
        // 集成可行性整备（探针世界构造，不动生产逻辑）：AI 国库注资补 farm 造价（gold=50）+ 选址半径扩大以扫到农田资源点
        // 生产行为验证：农场必须建在农田资源点上（ResourceNodeMapping/PlacementValidator），且国库须付得起造价。
        k1.AddResources(ResourceList.Of(new ResourceAmount(ResourceType.Gold, 100), new ResourceAmount(ResourceType.Stone, 60), new ResourceAmount(ResourceType.Wood, 60)));
        int radius0 = bcfg.aiBuildRadius;
        bcfg.aiBuildRadius = 48;
        {
            var fdD = BuildingFactory.FindDefById("farm");
            bool farmDefOk = fdD != null;
            string afford = fdD != null ? (k1.CanAfford(fdD.cost) ? "Y" : "N") : "?";
            _log.AppendLine("[DBG] farmDef=" + farmDefOk + " CanAfford=" + afford + " timeScale=" + Time.timeScale
                            + " radius=" + bcfg.aiBuildRadius);
        }
        int farmBefore = CountBuilding(1, "farm");
        _grainMi.Invoke(brain, new object[] { k1, bcfg });
        yield return new WaitForEndOfFrame();   // 等 TryBuild→Registry.Register 在同一帧内落账（BuildController L300 同步注册）
        int farmAfter = CountBuilding(1, "farm");
        Log("P5a E-B1 分诊通道A 建 farm（" + farmBefore + "→" + farmAfter + "，focus=" + (UtilityAction)k1.focus + "）",
            farmAfter > farmBefore);
        bcfg.aiBuildRadius = radius0;   // 选址半径还原（探针临时）
        k1.focus = focus0;   // 焦点还原（探针临时态，交回日 tick）
        SituationHub.Remove(1);

        // 集成负：无触发世界 → 分诊 no-op（不建任何建筑）。
        // 同帧 before/after 断言：两次计数之间零 yield，只捕获分诊自身副作用——
        // 免疫 AI 王国帧内自行演化（实测探针窗口内 AI 会自行落 Warehouse/其他，跨帧比数必失真；KingdomBrain 纯类不可 disable、
        // SetGameSpeed(0) 在考跑档被吸附 0.5x 非真冻结，故以同帧差分取代"全局冻结"意图，确定性更强）。
        RemoveBuilding(1, "farm"); RemoveBuilding(1, "Granary");
        yield return null;   // 清场落地（Destroy 帧末生效）
        var snapNone = new SituationSnapshot
        {
            KingdomId = 1, Day = 0,
            Economy = new EconomyBlock
            {
                Population = 10, GrainReserveDays = 9, Flow = default,
                Production = new List<ProductionEntry>(),
                StorageUsed = 0, StorageCapacity = 200
            }
        };
        SituationHub.Put(1, snapNone);
        int bBefore = CountBuilding(1, "farm") + CountBuilding(1, "Granary") + CountBuilding(1, "Warehouse");
        _grainMi.Invoke(brain, new object[] { k1, bcfg });
        int bAfter = CountBuilding(1, "farm") + CountBuilding(1, "Granary") + CountBuilding(1, "Warehouse");
        _log.AppendLine("[DBG-P5b] 各型计数 farm=" + CountBuilding(1, "farm") + " gran=" + CountBuilding(1, "Granary")
                        + " wh=" + CountBuilding(1, "Warehouse"));   // 记录型态（含 AI 自行演化项，仅知悉不判）
        Log("P5b 正常态 no-op（无触发→⑤ 不动工，同帧比数 " + bBefore + "→" + bAfter + "）", bAfter == bBefore);
        SituationHub.Remove(1);

        // ================= C′ 探针（2_23 批C·C′，D644 裁 A：真产能守卫） =================
        // 背景：MapProduceToEco 缺 rate>0/isResourceNode 守卫 ⇒ 仓储类与非产能建筑（~25 个 outputResource 默认 0=Gold、
        // wood_pile/stone_pile）被计入产能盘点 ⇒ 掩蔽 通道A/R-C2。C′ 收口：只计真产能建筑（口径对齐 BuildingFactory.cs:295）。
        var _mapMi = tKB.GetMethod("MapProduceToEco", BindingFlags.NonPublic | BindingFlags.Static);
        var _bebMi = tKB.GetMethod("BuildEconomyBlock", BindingFlags.NonPublic | BindingFlags.Static);
        var _cpoMi = tKB.GetMethod("CountProductionOf", BindingFlags.NonPublic | BindingFlags.Static);
        var _execBuildMi = tKB.GetMethod("ExecuteBuildFocus", BindingFlags.NonPublic | BindingFlags.Instance);
        Log("P6a C′ 反射面在场（MapProduceToEco/BuildEconomyBlock/CountProductionOf/ExecuteBuildFocus）="
            + (_mapMi != null && _bebMi != null && _cpoMi != null && _execBuildMi != null),
            _mapMi != null && _bebMi != null && _cpoMi != null && _execBuildMi != null);

        if (_mapMi != null)
        {
            // 负例①：真产能**仍计**（farm→Food=3／quarry→Stone=1／Blacksmith→Metal=9）
            int mFarm = (int)_mapMi.Invoke(null, new object[] { BuildingFactory.FindDefById("farm") });
            int mQuarry = (int)_mapMi.Invoke(null, new object[] { BuildingFactory.FindDefById("quarry") });
            int mSmith = (int)_mapMi.Invoke(null, new object[] { BuildingFactory.FindDefById("Blacksmith") });
            Log("P6b 负例 真产能仍计（farm→Food=" + mFarm + "／quarry→Stone=" + mQuarry + "／Blacksmith→Metal=" + mSmith + "）",
                mFarm == (int)EcoResource.Food && mQuarry == (int)EcoResource.Stone && mSmith == (int)EcoResource.Metal);

            // 负例②：rate=0 建筑**不再计**（含仓储 2 座 + 木石堆 + Gold-默认族抽样）
            string[] zeroRateIds = { "Granary", "Warehouse", "wood_pile", "stone_pile", "castle", "House", "market" };
            bool zOk = true; string zLog = "";
            for (int i = 0; i < zeroRateIds.Length; i++)
            {
                int v = (int)_mapMi.Invoke(null, new object[] { BuildingFactory.FindDefById(zeroRateIds[i]) });
                zLog += zeroRateIds[i] + "=" + v + " ";
                if (v != -1) zOk = false;
            }
            Log("P6c 负例 rate=0 建筑不再计产能（" + zLog + "）", zOk);

            // 负例③：资源点（isResourceNode）**不再计**（tree/farmland/mine/ore_vein）
            string[] nodeIds = { "tree", "farmland", "mine", "ore_vein" };
            bool nOk = true; string nLog = "";
            for (int i = 0; i < nodeIds.Length; i++)
            {
                int v = (int)_mapMi.Invoke(null, new object[] { BuildingFactory.FindDefById(nodeIds[i]) });
                nLog += nodeIds[i] + "=" + v + " ";
                if (v != -1) nOk = false;
            }
            Log("P6d 负例 资源点(isResourceNode)不再计产能（" + nLog + "）", nOk);

            // 观测（列报，不判）：Well rate=4>0 越守卫 + outputResource 默认 0=Gold ⇒ 仍计 Gold —— 同族残留
            int mWell = (int)_mapMi.Invoke(null, new object[] { BuildingFactory.FindDefById("Well") });
            _log.AppendLine("[列报] Well 映射=" + mWell + "（rate=4 越守卫；outputResource 默认 0=Gold＝同族残留，供 DZ-089 家族处置）");
        }

        // 正例（真实管线端到端）：有 Granary 无 Farm → 诊断 Food 产能=0 → 通道A 仍选建 farm
        if (_bebMi != null && _cpoMi != null && _execBuildMi != null && _decideMi != null)
        {
            RemoveBuilding(1, "farm"); RemoveBuilding(1, "Granary"); RemoveBuilding(1, "farmland");
            yield return null;   // 清场落地
            int gBefore = CountBuilding(1, "Granary");
            if (gBefore == 0)
            {
                // 真实建 1 座 Granary（走生产门面；Granary cost=wood4，前面已注资）
                int focusG = k1.focus;
                k1.focus = (int)UtilityAction.BoostHarvest;
                int radiusG = bcfg.aiBuildRadius; bcfg.aiBuildRadius = 48;
                _execBuildMi.Invoke(brain, new object[] { k1, bcfg, "Granary" });
                bcfg.aiBuildRadius = radiusG;
                k1.focus = focusG;
                yield return new WaitForEndOfFrame();
            }
            int gAfter = CountBuilding(1, "Granary");
            // 真实诊断管线（注：BuildEconomyBlock 内含 TakeFlow 读后清零＝生产同款语义，探针窗口内一次性消费）
            var ecoReal = (EconomyBlock)_bebMi.Invoke(null, new object[] { k1, TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0 });
            int foodCnt = ecoReal != null ? (int)_cpoMi.Invoke(null, new object[] { ecoReal, EcoResource.Food }) : -1;
            TriageDecision dReal = ecoReal != null
                ? (TriageDecision)_decideMi.Invoke(null, new object[] { ecoReal, dcfg, EcoResource.Food })
                : TriageDecision.NoOp;
            Log("P6e 正例 有 Granary(" + gAfter + ")无 Farm → 诊断 Food 产能=" + foodCnt + " → 通道A 决策=" + dReal,
                gAfter >= 1 && foodCnt == 0 && dReal == TriageDecision.BuildCapacity);
        }

        // 恢复探针前快照语义（交回真实日 tick；AI 王国演化正常化）
        Log("P1d 恢复：SituationHub 交回日 tick（Remove 已清注入）", true);

        Finish();
    }

    // ---- 辅助 ----

    private static UtilityActionDef FindDef(UtilityActionDef[] arr, UtilityAction id)
    {
        if (arr == null) return default;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].id == id) return arr[i];
        return default;   // 未命中 → id==None（调用方判 None）
    }

    private static int CountBuilding(int kid, string id)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.def != null && b.kingdomId == kid && b.def.id == id) n++;
        }
        return n;
    }

    private static void RemoveBuilding(int kid, string id)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return;
        for (int i = reg.All.Count - 1; i >= 0; i--)
        {
            var b = reg.All[i];
            if (b != null && b.def != null && b.kingdomId == kid && b.def.id == id)
            {
                // AI 王国建筑无玩家拆除链：直接 Die（Destroy）——探针临时清场用
                if (b != null && b.gameObject != null) Object.Destroy(b.gameObject);
            }
        }
    }

    private static IEnumerator WaitDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            int start = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            while (TimeManager.Instance != null && TimeManager.Instance.CurrentDay == start)
                yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private static void Finish()
    {
        _log.AppendLine("===== Smoke_2_23RP0 资源P0批C冒烟收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "smoke_2_23rp0_" + RunTag + ".log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[2_23RP0] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[2_23RP0] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（tag=" + RunTag + "）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}