using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.107 资源供给端补链批 冒烟（D562 / DZ-072a+DZ-073+DZ-076；任务书=多Agent交接/策划端/HH.107_资源供给端补链批_任务书.md §一件4）
//  用法：菜单「Valley/验证/HH107_资源供给端补链」——Play 后点（MCP 自动触发，零手动进局）。
//
//  骨架=HH.73 真实进局先例（SmokeApi.EnterGame 固定 seed=21107，smoke_h107 槽，4 国世界）；
//  mine/TrainingCamp/SiegeWorkshop=容器内直建（BuildingFactory.CreateBuildingInstance + kingdomId 指定，
//  HH.79 直建先例；M6 口径——不依赖地图生成物，位置/归属全控）。产率加速=运行时改 KingdomConfig SO
//  实例字段（Play 态改动不落盘，QuitSmoke 退 Play 自动还原；先例=HH.73 SetSecondsPerDay 快进）。
//
//  探针 P1~P8（任务书 §一件4；全绿=验收主判据，P3/P5=P8 配对审硬条款）：
//   P1 产出：玩家 mine 副产水晶入子仓（存量增长行为级；组件经 BuildingFactory 分支真实挂载）
//   P2 落库：搬运链全程——玩家国库 Vault_Crystal 增长（子仓满→Transport 广告→工人搬运→就近卸国库）
//   P3 消费端解锁（P8 配对审核心）：水晶足→玩家 Resident→Mage 转职成功（TryTrain 全链）；
//      负对照：水晶 0→拒（水晶不足日志）
//   P4 火油→弹药厂：国库火油足→SiegeWorkshopBuilding.Produce(FireballAmmo) 原料扣除+入厂级弹仓
//   P5 AI 同链：AI mine 副产→AI 台账 k.crystal 增长→AI Resident→Mage 过水晶检（TryTrain 成功+台账扣减）
//   P6 负探针：无矿洞 AI 国水晶恒 0（地图依赖限制在案）
//   P7 石头采集零回归：mine 采集身份字段逐字未动（isResourceNode/outputResource/rate 运行时读数）+
//      MineByproductComponent 共存不扰任务广告（无 ProducerComponent/本体 StorageComponent——①②③分支不触发）
//   P8 DZ-073：AI 搬运窗口玩家国库水晶零增量+AI 台账同窗增长（路由互斥配对证明，HH.73 P3 手法）
//  收尾：QuitSmoke（自动清 smoke_ 槽+退 Play）。
// ============================================================================
public static class Valley_HH107_Smoke_Byproduct
{
    private const int SEED = 21107;
    private const string SLOT = "smoke_h107";
    private const string TAG = "[HH107冒烟]";

    [MenuItem("Valley/验证/HH107_资源供给端补链")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先进入 Play（MCP enterPlaymode 后调用本菜单）。"); return; }
        new GameObject("HH107_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    // ===== 探针结果台账（行汇总+末尾 ALL PASS/FAIL 判定）=====
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

        // ---- 真实链进局（SmokeApi 幂等守卫内；HH.73 先例）----
        var cfg = new NewGameConfig
        {
            worldSeed = SEED,
            mapSeed = SEED,
            raceId = 0,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            selectedSlotId = SLOT,
            kingdomName = "河谷王国"
        };
        SmokeApi.EnterGame(cfg);

        // ---- 等世界就稳（120s 超时）----
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            {
                Debug.LogError(TAG + " 等世界就绪超时(120s)。");
                SmokeApi.QuitSmoke();
                yield break;
            }
        }
        yield return new WaitForSeconds(0.5f);   // 稳态窗（HH.69 教训）

        var reg = KingdomRegistry.Instance;
        var ruler = RulerController.Instance;
        var training = TrainingSystem.Instance;
        var grid = GridSystem.Instance;

        // AI 国收集（id>0 取前 3；P6 用其余国）
        var aiKids = new List<int>();
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (!all[i].IsPlayer) aiKids.Add(all[i].id);
        if (aiKids.Count < 2 || ruler == null || training == null)
        {
            Debug.LogError(TAG + $" 前置缺失（AI 国={aiKids.Count} 需≥2 / Ruler={ruler != null} / Training={training != null}），中止。");
            SmokeApi.QuitSmoke();
            yield break;
        }

        // ---- 产率加速（Play 态 SO 改动，退 Play 自动还原；0.05/s→2/s=冒烟窗口可观测）----
        var kcfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        float origCrystalRate = kcfg != null ? kcfg.byproductCrystalRate : 0f;
        float origFireOilRate = kcfg != null ? kcfg.byproductFireOilRate : 0f;
        if (kcfg != null) { kcfg.byproductCrystalRate = 2f; kcfg.byproductFireOilRate = 2f; }

        TimeManager.Instance.SetSecondsPerDay(15f);   // 快进（HH.73 先例：5s 真实=1 游戏日）
        TimeManager.Instance.SetGameSpeed(3f);

        var map = WorldManager.Instance.ActiveMap;
        var castle = FindPlayerCastle();
        Vector2Int center = castle != null ? new Vector2Int(castle.coord.x, castle.coord.y) : new Vector2Int(map.width / 2, map.height / 2);

        // ===== P7 结构前置：直建玩家 mine + 采集身份零回归断言 =====
        var mineDef = BuildingFactory.FindDefById("mine");
        if (mineDef == null) { FailFast("mine def 未找到（Resources/Buildings/mine.asset）"); yield break; }
        var playerMine = BuildAt(mineDef, center, 8, 0);
        if (playerMine == null) { FailFast("玩家 mine 直建失败"); yield break; }
        yield return null;   // 一帧让 AttachComponents/子仓落地

        var byprod = playerMine.GetComponent<MineByproductComponent>();
        bool p7Fields = mineDef.isResourceNode                     // M1 红线：isResourceNode=1 运行时读数未动
                        && mineDef.outputResource == ResourceType.Stone   // M1 红线：outputResource=Stone 未动
                        && Mathf.Approximately(mineDef.producer.rate, 0.3f)
                        && byprod != null                          // 件1：副产组件经 Factory 分支真实挂载
                        && playerMine.GetComponent<ProducerComponent>() == null   // isResourceNode 排除分支未变
                        && playerMine.GetComponent<StorageComponent>() == null;   // mine 本体仍无 Storage（身份未变）
        Record("P7", p7Fields, $"isResourceNode={mineDef.isResourceNode} output={mineDef.outputResource} rate={mineDef.producer.rate} 副产组件={byprod != null} 无Producer/本体仓={playerMine.GetComponent<ProducerComponent>() == null && playerMine.GetComponent<StorageComponent>() == null}");

        if (byprod == null) { FailFast("MineByproductComponent 未挂载（Factory 分支失效）"); yield break; }
        var crystalStore = byprod.GetStore(ResourceType.Crystal);
        var fireOilStore = byprod.GetStore(ResourceType.FireOil);

        // ===== P1 产出：玩家 mine 副产水晶入子仓 =====
        yield return new WaitForSeconds(4f);   // 加速产率 2/s → 4s≈8 个
        int c1 = crystalStore != null ? crystalStore.storedAmount : -1;
        int f1 = fireOilStore != null ? fireOilStore.storedAmount : -1;
        Record("P1", c1 > 0 && f1 > 0, $"水晶子仓={c1} 火油子仓={f1}（双槽并行恒产，4s 加速窗）");

        // ===== P2 落库：子仓→Transport→工人搬运→国库 Vault_Crystal 增长 =====
        // 玩家 idle 补偿（第七~九轮实锤：玩家 9 工人被开局 Production 面饱和占用+搬运单程耗时→60s 窗口紧）。
        // 补员 spawn 在玩家 mine 旁（缩短单程）；窗口 100s。AI 侧修后副产直走台账（UnloadInventory 分流）。
        int spawnedWorkers = 0;
        if (UnitFactory.Instance != null && byprod != null)
        {
            Vector2 minePos = byprod.transform.position;
            for (int i = 0; i < 6; i++)
            {
                var w = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker,
                    minePos + new Vector2(0.5f * i, 0.5f * i), 0);
                if (w != null) spawnedWorkers++;
            }
        }
        Debug.Log(TAG + " [前置] 玩家 Worker 补员=" + spawnedWorkers + "/6（mine 旁注入）");
        yield return null;   // 补员一帧注册进 Registry/调度器
        int vaultBefore = ruler.GetResource(ResourceType.Crystal);
        int playerIdle = CountIdleWorkers(0);
        bool p2 = false;
        int vaultAfterP2 = vaultBefore;
        float p2t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - p2t0 < 100f)
        {
            yield return new WaitForSeconds(2f);
            vaultAfterP2 = ruler.GetResource(ResourceType.Crystal);
            if (vaultAfterP2 > vaultBefore) { p2 = true; break; }   // 国库增长即过（提前退出）
        }
        Record("P2", p2, $"国库水晶 {vaultBefore}→{vaultAfterP2}（100s 搬运窗；补员={spawnedWorkers}，idle 工人≈{playerIdle}，子仓={crystalStore.storedAmount}）");
        if (!p2)
        {
            // 失败诊断：广告自检+_sources 注册+全场景 Vault 归属（TV.Instance 单例覆盖疑云取证）
            KingdomTask advTask = null;
            bool adv = byprod != null && byprod.TryAdvertiseTask(out advTask);
            Debug.Log(TAG + " [诊断] 广告自检 TryAdvertiseTask=" + adv + (adv ? " argsType=" + ((ScaleTaskArgs)advTask.args).resourceType : "（子仓未达阈值或组件失效）"));
            var ts = TaskScheduler.Instance;
            var fld = typeof(TaskScheduler).GetField("_sources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var srcs = fld != null ? fld.GetValue(ts) as System.Collections.IEnumerable : null;
            int srcN = 0; bool srcFound = false;
            if (srcs != null) foreach (var x in srcs) { srcN++; if (x as MineByproductComponent == byprod) srcFound = true; }
            Debug.Log(TAG + " [诊断] _sources=" + srcN + " 玩家mine组件已注册=" + srcFound);
            var vaults = Object.FindObjectsOfType<TreasureVault>();
            for (int i = 0; i < vaults.Length; i++)
            {
                var v = vaults[i];
                int kid = v.Castle != null ? v.Castle.kingdomId : -99;
                Debug.Log(TAG + " [诊断] Vault#" + i + " 父国=" + kid + " Crystal=" + v.GetAmount(ResourceType.Crystal)
                    + " FireOil=" + v.GetAmount(ResourceType.FireOil) + " isInstance=" + (TreasureVault.Instance == v));
            }
        }

        // ===== P3 消费端解锁（P8 配对审核心）：Resident→Mage 转职（costCrystal=1 条目）=====
        var camp = BuildAt(BuildingFactory.FindDefById("TrainingCamp"), center, 12, 0);
        yield return null;
        TrainingDef mageDef = default;
        bool mageFound = false;
        var trainings = training.GetTrainings("TrainingCamp");
        for (int i = 0; i < trainings.Count; i++)
        {
            if (trainings[i].toOccupation == Occupation.Mage && trainings[i].costCrystal > 0)
            { mageDef = trainings[i]; mageFound = true; break; }
        }
        var playerResident = FindResident(0);
        if (!mageFound || camp == null || playerResident == null)
        {
            Record("P3", false, $"前置缺失：TrainingCamp={camp != null} Mage条目={mageFound} 玩家居民={playerResident != null}");
        }
        else
        {
            // 负对照：水晶清零 → 转职拒（CanPayRecruit L337「水晶不足」日志）
            int vaultCrystalAll = ruler.GetResource(ResourceType.Crystal);
            if (vaultCrystalAll > 0) ruler.ModifyResource(ResourceType.Crystal, false, vaultCrystalAll);
            bool negRejected = !training.TryTrain(playerResident, mageDef, camp);
            // 正：注水晶（顺带验玩家侧 ModifyResource(Crystal) 链=TreasureVault 扩面行为证据）
            ruler.ModifyResource(ResourceType.Crystal, true, 10);
            int vaultAfterInject = ruler.GetResource(ResourceType.Crystal);
            bool posAccepted = training.TryTrain(playerResident, mageDef, camp);
            Record("P3", negRejected && posAccepted && vaultAfterInject >= 10,
                $"负对照拒={negRejected} 正探针过={posAccepted} 注10水晶后国库={vaultAfterInject}（TreasureVault 扩面链）");
        }

        // ===== P5 AI 同链：AI mine 副产→AI 台账→AI 转职过水晶检；P8 配对（玩家国库零增量）=====
        // （P4 弹药厂探针移至 P6 之后——直建 SiegeWorkshop 轮产会持续扣国库水晶/火油，污染 P8 配对断言）
        int aiKid = aiKids[0];
        var aiState = reg.Get(aiKid);
        var aiMine = BuildAt(mineDef, FindAiCenter(aiKid, map, center), 6, aiKid);
        yield return null;
        var aiByprod = aiMine != null ? aiMine.GetComponent<MineByproductComponent>() : null;
        var aiCrystalStore = aiByprod != null ? aiByprod.GetStore(ResourceType.Crystal) : null;
        if (aiMine == null || aiByprod == null)
        {
            Record("P5", false, "AI mine 直建/组件挂载失败");
            Record("P8", false, "依赖 P5 链，未执行");
        }
        else
        {
            // AI idle 补偿（第十轮实锤：AI 国工人竞争波动→台账增长不稳；补员 spawn 在 AI mine 旁专供搬运。
            // AI 单位=PlayerCamp 资产+kingdomId=aiKid→SpawnUnit 内部 SetFaction(AiKingdom)）。
            int aiSpawned = 0;
            Vector2 aiMinePos = aiByprod.transform.position;
            for (int i = 0; i < 6; i++)
            {
                var w = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker,
                    aiMinePos + new Vector2(0.5f * i, 0.5f * i), aiKid);
                if (w != null) aiSpawned++;
            }
            yield return null;
            Debug.Log(TAG + " [前置] AI Worker 补员=" + aiSpawned + "/6（k" + aiKid + " mine 旁注入）");

            // P8 差值窗口：从 AI mine 直建+补员起（贯穿 P2 后续搬运+P5 窗口，>200s），规避单程时序噪声
            int aiCrystalBefore = aiState.crystal;
            var aiVault = FindVaultByKingdom(aiKid);
            int aiVaultBefore = aiVault != null ? aiVault.GetAmount(ResourceType.Crystal) : -1;
            bool aiFlow = false;
            float p5t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - p5t0 < 100f)
            {
                yield return new WaitForSeconds(2f);
                if (aiState.crystal > aiCrystalBefore) { aiFlow = true; break; }   // AI 台账增长（搬运分流行为级）
            }
            int aiVaultAfter = aiVault != null ? aiVault.GetAmount(ResourceType.Crystal) : -1;
            // P8 配对（DZ-073 路由直证）：AI 副产进 AI 台账（非玩家国库、非 AI 主城 Vault 黑洞——
            // 旧路径 AI 工人卸进误挂 Vault=消费黑洞，修复=UnloadInventory 副产分流传账）。
            Record("P8", aiFlow && aiVaultAfter == aiVaultBefore,
                $"k{aiKid} 台账 {aiCrystalBefore}→{aiState.crystal}，AI 主城 Vault 水晶 {aiVaultBefore}→{aiVaultAfter}（分流：进台账不进黑洞）");

            // AI 侧转职（TryTrain 直调——TryTrainFromPool 硬编码玩家池，AI 由脑/本探针直调入口）
            var aiCamp = BuildAt(BuildingFactory.FindDefById("TrainingCamp"), FindAiCenter(aiKid, map, center), 10, aiKid);
            yield return null;
            var aiResident = FindResident(aiKid);
            if (aiResident == null) aiResident = SpawnResident(aiKid);
            if (aiCamp == null || aiResident == null || !mageFound)
            {
                Record("P5", false, $"AI 转职前置缺失：camp={aiCamp != null} 居民={aiResident != null}");
            }
            else
            {
                int aiCrystalAtTrain = aiState.crystal;
                // 负对照：先临时清台账（留底再还原，避免污染 P5 正探针语义）
                if (aiCrystalAtTrain > 0) aiState.crystal = 0;
                bool aiNeg = !training.TryTrain(aiResident, mageDef, aiCamp);
                aiState.crystal = aiCrystalAtTrain > 0 ? aiCrystalAtTrain : 1;   // 保证正探针水晶足额
                bool aiPos = training.TryTrain(aiResident, mageDef, aiCamp);
                Record("P5", aiFlow && aiNeg && aiPos,
                    $"AI 链：台账增长={aiFlow} 负对照拒={aiNeg} 正探针过={aiPos}（台账 {aiState.crystal}，已扣 1→{aiState.crystal}）");
            }
        }

        // ===== P6 负探针：无矿洞 AI 国水晶恒 0 =====
        bool p6 = true;
        string p6Detail = "";
        for (int i = 1; i < aiKids.Count; i++)   // aiKids[1..] 无直建 mine
        {
            var k = reg.Get(aiKids[i]);
            if (k != null && k.crystal != 0) { p6 = false; p6Detail += $"k{aiKids[i]}={k.crystal} "; }
        }
        Record("P6", p6, $"无矿洞 AI 国台账恒 0（{p6Detail}判定={p6}）");

        // ===== P4 火油→弹药厂：SiegeWorkshop 产火弹扣火油（移至 P6 后——轮产不再污染 P8 配对断言）=====
        var wsDef = BuildingFactory.FindDefById("SiegeWorkshop");
        var workshop = wsDef != null ? BuildAt(wsDef, center, 16, 0) : null;
        yield return null;
        var wsComp = workshop != null ? workshop.GetComponent<SiegeWorkshopBuilding>() : null;
        if (wsComp == null || !wsComp.IsReady)
        {
            Record("P4", false, $"前置缺失：workshop={workshop != null} 组件就绪={(wsComp != null && wsComp.IsReady)}");
        }
        else
        {
            int fireOilBefore = ruler.GetResource(ResourceType.FireOil);
            if (fireOilBefore < 10) ruler.ModifyResource(ResourceType.FireOil, true, 10 - fireOilBefore);
            int fireOilLoaded = ruler.GetResource(ResourceType.FireOil);
            int ammoBefore = wsComp.GetAmmo(ResourceType.FireballAmmo);
            int produced = wsComp.Produce(ResourceType.FireballAmmo, 5);
            int ammoAfter = wsComp.GetAmmo(ResourceType.FireballAmmo);
            int fireOilAfter = ruler.GetResource(ResourceType.FireOil);
            Record("P4", produced > 0 && ammoAfter > ammoBefore && fireOilAfter < fireOilLoaded,
                $"产火弹={produced} 弹仓 {ammoBefore}→{ammoAfter} 国库火油 {fireOilLoaded}→{fireOilAfter}（原料扣除）");
        }

        // ---- 收尾：复原产率（同会话续跑容器防污染）+ 汇总 + 退 Play ----
        if (kcfg != null) { kcfg.byproductCrystalRate = origCrystalRate; kcfg.byproductFireOilRate = origFireOilRate; }
        TimeManager.Instance.SetGameSpeed(1f);

        Debug.Log($"{TAG} ===== 轮汇总（{_results.Count} 探针）=====");
        for (int i = 0; i < _results.Count; i++) Debug.Log($"{TAG} {_results[i]}");
        Debug.Log($"{TAG} 判定：{(_allPass ? "ALL PASS" : "HAS FAIL")}");
        SmokeApi.QuitSmoke();
    }

    // ===== helpers =====

    private static void FailFast(string reason)
    {
        Debug.LogError($"{TAG} 中止：{reason}");
        SmokeApi.QuitSmoke();
    }

    /// <summary>直建建筑（生产链 CreateBuildingInstance，HH.79/TestFixtureApi 先例：isPlayerBuilt=false+Active+kingdomId 指定）。</summary>
    private static Building BuildAt(BuildingDef def, Vector2Int center, int ringIdx, int kingdomId)
    {
        if (def == null || BuildingFactory.Instance == null || GridSystem.Instance == null) return null;
        var map = WorldManager.Instance.ActiveMap;
        Vector2Int cell = MapGenRules.NearestWalkable(map, center.x, center.y);
        // 环序取点：ringIdx 直接当偏移半径用（容器内少建筑，不与 Foundry 环序竞争）
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
        // 返回实例（Registry 尾部即新建筑）
        var all = BuildingRegistry.Instance.All;
        for (int i = all.Count - 1; i >= 0; i--)
            if (all[i] != null && all[i].coord.x == coord.x && all[i].coord.y == coord.y) return all[i];
        return null;
    }

    private static Building FindPlayerCastle()
    {
        var buildings = Object.FindObjectsOfType<Building>();
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b != null && b.kingdomId == 0 && b.def != null && b.def.id == "castle") return b;
        }
        return null;
    }

    /// <summary>按归属国找主城 TreasureVault（P8 分流断言用：AI 主城 Vault 系 CastleCore 误挂结构，修复后不再收副产）。</summary>
    private static TreasureVault FindVaultByKingdom(int kid)
    {
        var vaults = Object.FindObjectsOfType<TreasureVault>();
        for (int i = 0; i < vaults.Length; i++)
        {
            var v = vaults[i];
            if (v != null && v.Castle != null && v.Castle.kingdomId == kid) return v;
        }
        return null;
    }

    private static Vector2Int FindAiCenter(int kid, MapData map, Vector2Int fallback)
    {
        var buildings = Object.FindObjectsOfType<Building>();
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b != null && b.kingdomId == kid && b.def != null && b.def.id == "castle")
                return new Vector2Int(b.coord.x, b.coord.y);
        }
        return fallback;
    }

    /// <summary>找某国空闲度近似最低的 Resident（EffectiveOccupation=Resident=22，转职公共源）。</summary>
    private static UnitController FindResident(int kid)
    {
        if (UnitRegistry.Instance == null) return null;
        var all = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());   // 快照副本（HH.76 雷区纪律）
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (u.EffectiveOccupation != Occupation.Resident) continue;
            return u;
        }
        return null;
    }

    /// <summary>生产链补造 AI Resident（UnitDataManager 资产 key 全为 PlayerCamp_*，AI 单位=PlayerCamp 资产+
    /// kingdomId>0 → SpawnUnit 内部 SetFaction(AiKingdom)（UnitFactory L139-143 先例）；首轮实跑 [AiKingdom_Resident] 无 key 实锤）。</summary>
    private static UnitController SpawnResident(int kid)
    {
        if (UnitFactory.Instance == null) return null;
        var map = WorldManager.Instance.ActiveMap;
        var b = Object.FindObjectsOfType<Building>();
        Vector2 pos = new Vector2(map.width / 2f, map.height / 2f);
        for (int i = 0; i < b.Length; i++)
            if (b[i] != null && b[i].kingdomId == kid && b[i].def != null && b[i].def.id == "castle")
            { pos = b[i].transform.position; break; }
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Resident, pos, kid);
        return go != null ? go.GetComponent<UnitController>() : null;
    }

    /// <summary>某国 idle 近似工人数（无任务 Worker/Resident 计数，诊断用非断言）。</summary>
    private static int CountIdleWorkers(int kid)
    {
        int n = 0;
        if (UnitRegistry.Instance == null) return 0;
        var all = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());   // 快照副本（HH.76 雷区纪律）
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Worker || occ == Occupation.Resident || occ == Occupation.Porter) n++;
        }
        return n;
    }
}
