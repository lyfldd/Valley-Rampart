using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.109 存档域小批 冒烟（D564 / DZ-074 Chest 入档 + DZ-075 训练队列入档/清场泄漏修；
//  任务书=多Agent交接/策划端/HH.109_存档域小批_任务书.md §一件3）
//  用法：菜单「Valley/验证/HH109_存档域小批」——Play 后点（MCP 自动触发，零手动进局）。
//
//  骨架=HH.107 容器结构+入口走 HH.92 测试环境正门（TestHarnessApi.EnterTestRun 固定 seed=21109，考跑守卫全开；
//  训练营/居民/箱子=容器内生产链直建——SpawnChest 唯一落箱入口+TryTrain 唯一入队入口+CreateBuildingInstance）。
//
//  探针 P1~P4（任务书 §一件3；P1~P3 全绿=验收主判据，P4=四容器回归另行触发）：
//   P1 Chest 入档（DZ-074）：两箱（不同格/内容/阵营）→存→破坏态（拾一箱+另放一箱）→读→
//      断言箱在+内容物逐项一致+bornDay/ownerFaction 一致（读档重建幂等=破坏态被存档态覆盖）
//   P2 队列入档（DZ-075）：训练营+居民×2→TryTrain 在训+排队→快进半程→存→
//      清场复刻（TeardownScene+ClearAllBuildings，真实读档链 ContinueFromSave 语义单场景复刻）→读→
//      断言队列恢复条目数/startDay/effCostDays/inTraining 逐项一致（进度=游戏日保留）→
//      续快进到完成日→断言在训条目转职完成（续训可完成）
//   P3 清场无死键（L-11 口径 ≥3 轮建拆建，第 3 轮断言为判定口径）：每轮=入队→清场编排两步
//      （ClearAllBuildings+TrainingSystem.ResetState=WorldLifecycle 编排行直调）→断言 _queues 零死键；
//      末尾 ResetWorldForNext 全链整合验证（编排行真实接通）
//   P4 四容器回归（2_20/2_20B/2_20C/2_13_C）：独立菜单手动触发，不计入本容器台账
//  收尾：QuitSmoke（自动清 smoke_ 槽+退 Play）。
// ============================================================================
public static class Valley_HH109_Smoke_SaveDomain
{
    private const int SEED = 21109;
    private const string SLOT_P1 = "smoke_h109a";   // P1 箱入档槽
    private const string SLOT_P2 = "smoke_h109b";   // P2 队列入档槽
    private const string TAG = "[HH109冒烟]";

    [MenuItem("Valley/验证/HH109_存档域小批")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先进入 Play（MCP enterPlaymode 后调用本菜单）。"); return; }
        new GameObject("HH109_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine)
        {
            Application.logMessageReceived += OnLog;   // 协程异常捕获器：Unity 协程内异常=静默终止（不留日志），
                                                       // 挂全局监听把 Exception 打成显式记录（P2 协程死亡定位）
            StartCoroutine(routine);
        }
        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return;
            string first = stackTrace != null && stackTrace.Length > 0
                ? stackTrace.Split('\n')[0] : "";
            Debug.LogError("[HH109冒烟] [协程异常捕获] " + condition + " @ " + first);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }
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

        // ---- 测试环境正门进局（HH.92/D552 TestHarnessApi：考跑守卫全开——ThroneAnchor 判负封死/
        //      全怪源静默/补员静默/sim 日判豁免/战斗降速豁免；真实玩家实体保留供训练/箱探针）----
        //  判负事故实录（本容器第二跑）：清场复刻 TeardownScene 清光玩家单位→ThroneAnchor IsKingdomLost→
        //  GameOver「河谷失守」→timeScale=0 冻结协程→会话污染。考跑守卫=该事故的正解封死开关。
        var cfg = new NewGameConfig
        {
            worldSeed = SEED,
            mapSeed = SEED,
            raceId = 0,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            selectedSlotId = SLOT_P1,
            kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 60f);   // 60x 直通=6s 真实/游戏日；单帧跨度≤60s<360s/天（M4 安全注）

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

        var save = SaveManager.Instance;
        var chestMgr = ChestManager.Instance;
        var training = TrainingSystem.Instance;
        if (save == null || chestMgr == null || training == null)
        {
            Debug.LogError(TAG + $" 前置缺失（Save={save != null} Chest={chestMgr != null} Training={training != null}），中止。");
            SmokeApi.QuitSmoke();
            yield break;
        }

        // 快进由考跑接管（EnterTestRun 60x=6s 真实/游戏日）；不再用 SetSecondsPerDay/SetGameSpeed
        //（L-09：SetGameSpeed 四档吸附+战斗降速钳制——考跑 timeScale 直通无此风险面）

        var castle = FindPlayerCastle();
        Vector2Int center = castle != null ? new Vector2Int(castle.coord.x, castle.coord.y)
                                           : new Vector2Int(WorldManager.Instance.ActiveMap.width / 2, WorldManager.Instance.ActiveMap.height / 2);
        Vector2Int cellA = MapGenRules.NearestWalkable(WorldManager.Instance.ActiveMap, center.x + 3, center.y + 1);
        Vector2Int cellB = MapGenRules.NearestWalkable(WorldManager.Instance.ActiveMap, center.x + 3, center.y + 2);

        // ===== P1 Chest 入档：两箱→存→破坏态→读→逐项一致 =====
        var packA = new ResourcePack { gold = 7, stone = 3 };
        var packB = new ResourcePack { wood = 5, food = 2 };
        var boxA = chestMgr.SpawnChest(new GridCoord(cellA.x, cellA.y), packA, Faction.PlayerCamp);
        var boxB = chestMgr.SpawnChest(new GridCoord(cellB.x, cellB.y), packB, Faction.None);
        Debug.Log(TAG + " [P1前置] Spawn后立即 Count=" + chestMgr.Count + "（A/B 非 null=" + (boxA != null) + "/" + (boxB != null) + "）");
        yield return null;
        Debug.Log(TAG + " [P1前置] yield后 Count=" + chestMgr.Count
            + " boxA引用存活=" + (boxA != null) + " boxA实例存活=" + (boxA != null && boxA.gameObject != null)
            + "（引用活+Count0=列表被Clear；引用死=Update RemoveAt fake-null 路径）");
        if (boxA == null || boxB == null)
        {
            Record("P1", false, "SpawnChest 直建失败（落箱入口失效）");
        }
        else
        {
            // 存档快照（逐字段：cell/contents 八字段/bornDay/ownerFaction）
            var snapA = ChestSnap.Of(boxA);
            var snapB = ChestSnap.Of(boxB);
            Debug.Log(TAG + " [P1前置] Save前 Count=" + chestMgr.Count + " boxA=" + (boxA != null) + " boxB=" + (boxB != null));
            bool saved = save.Save(SLOT_P1);
            yield return null;

            // 破坏态：移走箱A+箱B 同格再放一箱（读档后应被存档态覆盖=幂等直证）
            chestMgr.Remove(boxA);   // 破坏态用 Remove（Pickup 的 Interactor 参数为值类型不收 null；Remove=拾取/过期同款移除语义）
            chestMgr.SpawnChest(new GridCoord(cellB.x, cellB.y), new ResourcePack { gold = 99 }, Faction.Monster);
            yield return null;
            int countDirty = chestMgr.Count;

            // 读档（真实链：SaveManager.Load 全链分发；ChestManager Scene 阶段先清后建）
            bool loaded = save.Load(SLOT_P1);
            yield return null;

            var boxes = Object.FindObjectsOfType<ChestEntity>();
            var snapA2 = MatchByGold(boxes, 7);
            var snapB2 = MatchByWood(boxes, 5);
            bool p1 = loaded && saved && boxes.Length == 2
                      && snapA2 != null && snapA.EqualsSnap(snapA2)
                      && snapB2 != null && snapB.EqualsSnap(snapB2);
            Record("P1", p1, "破坏态 Count=" + countDirty + "→读后=" + boxes.Length
                + "（期望2）；箱A一致=" + (snapA2 != null && snapA.EqualsSnap(snapA2))
                + " 箱B一致=" + (snapB2 != null && snapB.EqualsSnap(snapB2))
                + "（cell/contents 八字段/bornDay/ownerFaction 逐项）");
        }

        // ===== P2 队列入档：训练营+在训+排队→存→清场复刻→读→进度保留→续训完成 =====
        Debug.Log(TAG + " [P2步进] 开始：直建 TrainingCamp");
        var camp = BuildAt(BuildingFactory.FindDefById("TrainingCamp"), center, 6, 0);
        Debug.Log(TAG + " [P2步进] camp=" + (camp != null) + " saveId=" + (camp != null ? camp.SaveId : "null"));
        yield return null;
        if (camp == null)
        {
            Record("P2", false, "TrainingCamp 直建失败");
        }
        else
        {
            // 训练定义：动态取该设施第一条条目（TrainingCamp 表无 Worker 条目实锤——不硬编码目标职业，
            // 完成断言按 def.toOccupation 动态比对；costDays>0 保证快进可观测）
            TrainingDef def = default;
            bool defFound = false;
            var trainings = training.GetTrainings(camp);
            for (int i = 0; i < trainings.Count; i++)
            {
                if (trainings[i].costDays > 0)
                { def = trainings[i]; defFound = true; break; }
            }
            var res1 = FindResident(0) ?? SpawnResident(0);
            var res2 = FindResidentExcept(res1) ?? SpawnResident(0);   // 排除 res1（Resident 在训中职业不变，FindResident 会再命中同单位）
            if (!defFound || res1 == null || res2 == null)
            {
                Record("P2", false, $"前置缺失：训练条目={defFound} 居民1={res1 != null} 居民2={res2 != null}");
            }
            else
            {
                // 资源注入（TryTrain 入队即扣费：金+水晶按条目成本；玩家走 RulerController，HH.107 P3 注入手法）
                var ruler = RulerController.Instance;
                if (ruler != null)
                {
                    if (ruler.Gold < def.costGold * 3) ruler.ModifyResource(ResourceType.Gold, true, def.costGold * 3);
                    if (def.costCrystal > 0 && ruler.GetResource(ResourceType.Crystal) < def.costCrystal * 3)
                        ruler.ModifyResource(ResourceType.Crystal, true, def.costCrystal * 3);
                }
                bool t1 = training.TryTrain(res1, def, camp);   // 有空槽→在训
                bool t2 = training.TryTrain(res2, def, camp);   // 满/空槽→排队（弹性：slots≥2 时也在训）
                Debug.Log(TAG + " [P2步进] TryTrain t1=" + t1 + " t2=" + t2 + " def=" + def.buildingId + "→" + def.toOccupation + " costDays=" + def.costDays);
                yield return null;

                // 快进半程：按游戏日轮询（60x=6s 真实/天；0.5 天≈3s 真实；上限 20s 真实防卡）
                float halfT0 = Time.realtimeSinceStartup;
                float dayAtQueue = TimeManager.Instance.CurrentDay;
                while (TimeManager.Instance.CurrentDay < dayAtQueue + 0.5f
                       && Time.realtimeSinceStartup - halfT0 < 20f)
                    yield return null;

                var snap1 = QueueSnap.Of(training, res1);
                var snap2 = QueueSnap.Of(training, res2);
                bool p2snapOk = snap1 != null && snap2 != null;
                int dayAtSave = (int)TimeManager.Instance.CurrentDay;
                bool saved2 = p2snapOk && save.Save(SLOT_P2);
                yield return null;

                // 清场复刻（真实读档链 ContinueFromSave=场景重进：单位不在场+建筑不在场；
                // 容器单场景复刻=TeardownScene 清单位+ClearAllBuildings 清建筑，随后 Load 全权重建）
                if (TeardownManager.Instance != null) TeardownManager.Instance.TeardownScene();
                if (BuildingFactory.Instance != null) BuildingFactory.Instance.ClearAllBuildings();
                yield return null;

                bool loaded2 = save.Load(SLOT_P2);
                yield return null;

                // 恢复后反查：按 unitSaveId 匹配（SaveId 随档恢复机制直证）
                var snap1r = QueueSnap.Of(training, snap1.unitSaveId);
                var snap2r = QueueSnap.Of(training, snap2.unitSaveId);
                bool restored = loaded2 && saved2 && snap1r != null && snap2r != null
                                && snap1.EqualsEntry(snap1r) && snap2.EqualsEntry(snap2r);

                // 续训可完成：按游戏日轮询（4 天断路=60s 真实上限内；目标职业按 def 动态比对）
                bool done1 = false, promoted2 = false;
                float doneT0 = Time.realtimeSinceStartup;
                float dayAtRestore = TimeManager.Instance.CurrentDay;
                while (Time.realtimeSinceStartup - doneT0 < 60f)
                {
                    yield return null;
                    if (!done1 && IsOccupation(snap1r.unitSaveId, def.toOccupation)) done1 = true;
                    if (done1 && IsOccupation(snap2r.unitSaveId, def.toOccupation)) { promoted2 = true; break; }
                    if (TimeManager.Instance.CurrentDay >= dayAtRestore + 4f) break;   // 4 天窗口仍无转职=失败退出
                }
                Record("P2", restored && done1,
                    $"存档日={dayAtSave} 恢复一致={restored}（条目2+startDay/effCostDays/inTraining 逐项；目标={def.toOccupation}）"
                    + $" 续训完成={done1} 排队晋升={promoted2}");
            }
        }

        // ===== P3 清场无死键（L-11：≥3 轮建拆建，第 3 轮断言为判定口径）=====
        bool p3All = true;
        string p3Detail = "";
        for (int round = 1; round <= 3; round++)
        {
            var campR = BuildAt(BuildingFactory.FindDefById("TrainingCamp"), center, 6 + round, 0);
            yield return null;
            var resR = FindResident(0) ?? SpawnResident(0);
            bool queued = false;
            if (campR != null && resR != null)
            {
                // 资源注入（同 P2：入队即扣费）+动态取第一条条目
                var trainingsR = training.GetTrainings(campR);
                TrainingDef defR = default;
                for (int i = 0; i < trainingsR.Count; i++)
                    if (trainingsR[i].costDays > 0) { defR = trainingsR[i]; break; }
                if (defR.buildingId != null)
                {
                    var rulerR = RulerController.Instance;
                    if (rulerR != null)
                    {
                        if (rulerR.Gold < defR.costGold * 3) rulerR.ModifyResource(ResourceType.Gold, true, defR.costGold * 3);
                        if (defR.costCrystal > 0 && rulerR.GetResource(ResourceType.Crystal) < defR.costCrystal * 3)
                            rulerR.ModifyResource(ResourceType.Crystal, true, defR.costCrystal * 3);
                    }
                    queued = training.TryTrain(resR, defR, campR);
                }
            }
            yield return null;
            var before = QueueCountSnapshot();
            // 清场编排两步直调（=WorldLifecycle L43-44 编排行的真实语义：ClearAllBuildings 不走 Die → ResetState 清偿）
            if (BuildingFactory.Instance != null) BuildingFactory.Instance.ClearAllBuildings();
            if (TrainingSystem.Instance != null) TrainingSystem.Instance.ResetState();
            yield return null;
            var after = QueueCountSnapshot();
            bool roundOk = queued && before.count > 0 && after.count == 0 && after.deadKeys == 0;
            if (!roundOk) p3All = false;
            p3Detail += $"R{round}:入队={queued} 清前={before.count} 清后={after.count} 死键={after.deadKeys} ";
            if (round < 3) yield return new WaitForSeconds(0.2f);
        }
        Record("P3", p3All, "三轮建拆建（第3轮判定）" + p3Detail);

        // P3 附加：ResetWorldForNext 全链整合验证（编排行真实接通；清场后世界空，本探针末步）
        bool queued4 = false;
        var campW = BuildAt(BuildingFactory.FindDefById("TrainingCamp"), center, 6, 0);
        yield return null;
        if (campW != null)
        {
            var resW = FindResident(0) ?? SpawnResident(0);
            if (resW != null)
            {
                var trainingsW = training.GetTrainings(campW);
                TrainingDef defW = default;
                for (int i = 0; i < trainingsW.Count; i++)
                    if (trainingsW[i].costDays > 0) { defW = trainingsW[i]; break; }
                if (defW.buildingId != null)
                {
                    var rulerW = RulerController.Instance;
                    if (rulerW != null)
                    {
                        if (rulerW.Gold < defW.costGold * 3) rulerW.ModifyResource(ResourceType.Gold, true, defW.costGold * 3);
                        if (defW.costCrystal > 0 && rulerW.GetResource(ResourceType.Crystal) < defW.costCrystal * 3)
                            rulerW.ModifyResource(ResourceType.Crystal, true, defW.costCrystal * 3);
                    }
                    queued4 = training.TryTrain(resW, defW, campW);
                }
            }
        }
        yield return null;
        WorldLifecycle.ResetWorldForNext();
        yield return null;
        var afterWorld = QueueCountSnapshot();
        Record("P3W", queued4 && afterWorld.count == 0 && afterWorld.deadKeys == 0,
            $"ResetWorldForNext 整合：入队={queued4} 清后={afterWorld.count} 死键={afterWorld.deadKeys}（编排行接通）");

        // ---- 收尾：考跑全量恢复 + 汇总 + 退 Play ----
        TestHarnessApi.ExitTestRun();   // timeScale/maximumDeltaTime/渲染设置复原（考跑态收尾纪律）
        Debug.Log($"{TAG} ===== 轮汇总（{_results.Count} 探针）=====");
        for (int i = 0; i < _results.Count; i++) Debug.Log($"{TAG} {_results[i]}");
        Debug.Log($"{TAG} 判定：{(_allPass ? "ALL PASS" : "HAS FAIL")}");
        SmokeApi.QuitSmoke();
    }

    // ===== helpers =====

    private static void FailFast(string reason)
    {
        Debug.LogError($"{TAG} 中止：{reason}");
        TestHarnessApi.ExitTestRun();   // 中止路径同样全量恢复（防考跑态残留）
        SmokeApi.QuitSmoke();
    }

    /// <summary>直建建筑（生产链 CreateBuildingInstance，HH.107 BuildAt 同款：isPlayerBuilt=false+Active+kingdomId 指定）。</summary>
    private static Building BuildAt(BuildingDef def, Vector2Int center, int ringIdx, int kingdomId)
    {
        if (def == null || BuildingFactory.Instance == null || GridSystem.Instance == null) return null;
        var map = WorldManager.Instance.ActiveMap;
        if (map == null) return null;
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

    /// <summary>找玩家 Resident（转职公共源；快照副本纪律 HH.76）。</summary>
    private static UnitController FindResident(int kid)
    {
        return FindResidentExcept(null, kid);
    }

    /// <summary>找玩家 Resident 并排除指定单位（防同单位重复入队——Resident 在训中职业不变会被再次命中）。</summary>
    private static UnitController FindResidentExcept(UnitController exclude, int kid = 0)
    {
        if (UnitRegistry.Instance == null) return null;
        var all = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
            if (u == exclude) continue;
            if (u.EffectiveOccupation != Occupation.Resident) continue;
            return u;
        }
        return null;
    }

    /// <summary>生产链补造玩家 Resident（HH.107 SpawnResident 同款）。</summary>
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

    /// <summary>按 unitSaveId 反查该单位当前在训目标职业（P2 续训完成断言；读档注册表反查）。</summary>
    private static bool IsOccupation(string unitSaveId, Occupation occ)
    {
        var u = FindUnitBySaveId(unitSaveId);
        return u != null && u.EffectiveOccupation == occ;
    }

    private static UnitController FindUnitBySaveId(string unitSaveId)
    {
        if (SaveManager.Instance == null || !SaveManager.Instance.TryGetSaveable(unitSaveId, out var s)) return null;
        return s as UnitController;
    }

    /// <summary>_queues 快照（反射读 private 字段：总桶数+fake-null 死键计数；P3 判据，HH.107 _sources 反射先例）。</summary>
    private static (int count, int deadKeys) QueueCountSnapshot()
    {
        var fld = typeof(TrainingSystem).GetField("_queues", BindingFlags.NonPublic | BindingFlags.Instance);
        var queues = fld != null ? fld.GetValue(TrainingSystem.Instance) as IDictionary : null;
        if (queues == null) return (0, 0);
        int dead = 0;
        foreach (var key in queues.Keys)
        {
            if (key is Object o && o == null) dead++;   // Unity fake-null 死键
        }
        return (queues.Count, dead);
    }

    // ===== 箱子快照（P1 逐项断言载体）=====

    private class ChestSnap
    {
        public int cx, cy;
        public float bornDay;
        public int faction;
        public int gold, stone, wood, food, metal, stoneAmmo, fireballAmmo, magicAmmo;

        public static ChestSnap Of(ChestEntity c)
        {
            return new ChestSnap
            {
                cx = c.cell.x, cy = c.cell.y,
                bornDay = c.bornDay,
                faction = (int)c.ownerFaction,
                gold = c.contents.gold, stone = c.contents.stone,
                wood = c.contents.wood, food = c.contents.food,
                metal = c.contents.metal,
                stoneAmmo = c.contents.stoneAmmo, fireballAmmo = c.contents.fireballAmmo, magicAmmo = c.contents.magicAmmo
            };
        }

        public bool EqualsSnap(ChestSnap o)
        {
            return o != null && cx == o.cx && cy == o.cy
                && Mathf.Approximately(bornDay, o.bornDay)
                && faction == o.faction
                && gold == o.gold && stone == o.stone && wood == o.wood && food == o.food
                && metal == o.metal && stoneAmmo == o.stoneAmmo
                && fireballAmmo == o.fireballAmmo && magicAmmo == o.magicAmmo;
        }
    }

    private static ChestSnap MatchByGold(ChestEntity[] boxes, int gold)
    {
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i] != null && boxes[i].contents.gold == gold) return ChestSnap.Of(boxes[i]);
        return null;
    }

    private static ChestSnap MatchByWood(ChestEntity[] boxes, int wood)
    {
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i] != null && boxes[i].contents.wood == wood) return ChestSnap.Of(boxes[i]);
        return null;
    }

    // ===== 队列条目快照（P2 逐项断言载体；反射读 _queues 按单位匹配）=====

    private class QueueSnap
    {
        public string unitSaveId;
        public string buildingSaveId;
        public Occupation targetOcc;
        public int startDay;
        public bool inTraining;
        public int effCostDays;
        public int kingdomId;

        /// <summary>按运行时单位引用取快照（存档前）。</summary>
        public static QueueSnap Of(TrainingSystem ts, UnitController unit)
        {
            foreach (var e in IterateEntries(ts))
            {
                var u = e.unitRef as UnitController;
                if (u == unit) return FromEntry(e);
            }
            return null;
        }

        /// <summary>按存档 unitSaveId 取快照（读档后——SaveId 随档恢复，直接比对）。</summary>
        public static QueueSnap Of(TrainingSystem ts, string unitSaveId)
        {
            foreach (var e in IterateEntries(ts))
            {
                var u = e.unitRef as UnitController;
                if (u != null && u.SaveId == unitSaveId) return FromEntry(e);
            }
            return null;
        }

        private static QueueSnap FromEntry(Entry e)
        {
            return new QueueSnap
            {
                unitSaveId = e.unitSaveId,
                buildingSaveId = e.buildingSaveId,
                targetOcc = e.targetOcc,
                startDay = e.startDay,
                inTraining = e.inTraining,
                effCostDays = e.effCostDays,
                kingdomId = e.kingdomId
            };
        }

        /// <summary>恢复一致性断言（startDay/effCostDays/inTraining/建筑归属逐项；进度=游戏日保留直证）。</summary>
        public bool EqualsEntry(QueueSnap o)
        {
            return o != null
                && buildingSaveId == o.buildingSaveId
                && startDay == o.startDay
                && inTraining == o.inTraining
                && effCostDays == o.effCostDays
                && kingdomId == o.kingdomId
                && targetOcc == o.targetOcc;
        }

        public struct Entry
        {
            public Object unitRef;
            public string unitSaveId;
            public string buildingSaveId;
            public Occupation targetOcc;
            public int startDay;
            public bool inTraining;
            public int effCostDays;
            public int kingdomId;
        }

        private static IEnumerable<Entry> IterateEntries(TrainingSystem ts)
        {
            var result = new List<Entry>();
            var fld = typeof(TrainingSystem).GetField("_queues", BindingFlags.NonPublic | BindingFlags.Instance);
            var queues = fld != null ? fld.GetValue(ts) as IDictionary : null;
            if (queues == null) return result;
            foreach (DictionaryEntry kv in queues)
            {
                var building = kv.Key as Building;
                if (building == null) continue;   // 死键跳过
                var q = kv.Value;
                if (q == null) continue;
                var entriesList = q.GetType().GetField("Entries").GetValue(q) as IEnumerable;
                if (entriesList == null) continue;
                foreach (var raw in entriesList)
                {
                    var et = raw.GetType();
                    var unit = et.GetField("unit").GetValue(raw) as UnitController;
                    var def = et.GetField("def").GetValue(raw);
                    var e = new Entry
                    {
                        unitRef = unit,
                        unitSaveId = unit != null ? unit.SaveId : null,
                        buildingSaveId = building.SaveId,
                        startDay = (int)et.GetField("startDay").GetValue(raw),
                        inTraining = (bool)et.GetField("inTraining").GetValue(raw),
                        effCostDays = (int)et.GetField("effCostDays").GetValue(raw),
                        kingdomId = (int)et.GetField("kingdomId").GetValue(raw),
                    };
                    // toOccupation 在 TrainingDef（def 装箱 struct）上，不在 TrainingQueueEntry（et）上——此前在 et 上查返回 null → GetValue NRE
                    e.targetOcc = def != null ? (Occupation)def.GetType().GetField("toOccupation").GetValue(def) : Occupation.Resident;
                    result.Add(e);
                }
            }
            return result;
        }
    }
}
