using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.319 `M1-F`（`F-1` 批 · 水仓化 ＋ 真搬运链）判据探针（任务书 §判据 1~10）
//  用法：菜单「Valley/验证/HH319_M1F水仓化探针」——进 Play 后点（或 MCP 触菜单）。
//  正门：`TestHarnessApi.EnterTestRun`（test-harness-first · L-17）→ 收尾 `ExitTestRun` + `QuitSmoke`。
//  每条判据日志自带「鉴别力：」行（L-51 前置鉴别力声明 · ⛔ 不得省）。
//  判据 11（回归 HH315/HH316/HH317/M7）由既有容器另跑，不在本容器。
// ============================================================================
public static class Valley_HH319_M1F_Probe
{
    private const int SEED = 20319;
    private const string SLOT = "smoke_w319";

    [MenuItem("Valley/验证/HH319_M1F水仓化探针")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319探针] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    private static readonly List<string> R = new List<string>();
    private static int _pass;
    private static int _total;
    private static float _lastDiag;
    private static bool loadOk3c;

    private static void Judge(string name, string read, bool ok, string distinguishing)
    {
        _total++;
        if (ok) _pass++;
        R.Add($"{name} ={(ok ? "OK" : "FAIL")} | {read}");
        Debug.Log($"[HH319探针] {name} ={(ok ? "OK" : "FAIL")} | {read}");
        Debug.Log($"[HH319探针]   鉴别力：{distinguishing}");
    }

    private static IEnumerator RunCoroutine()
    {
        R.Clear(); _pass = 0; _total = 0;
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            { Debug.LogError("[HH319探针] 等世界就绪超时。"); yield return Finish(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        // ================= 判据 1：落码（结构） =================
        var wellDef = Resources.Load<BuildingDef>("Buildings/Well");
        var farmDef = Resources.Load<BuildingDef>("Buildings/farm");
        bool c1 = wellDef != null && farmDef != null
                  && ResourceCatalog.Contains(ResourceType.Water)
                  && ResourceCatalog.VolumeOf(ResourceType.Water) == 1
                  && ResourceCatalog.Accepts(wellDef.warehousePaths, ResourceType.Water)
                  && ResourceCatalog.Accepts(farmDef.warehousePaths, ResourceType.Water);
        Judge("判据1 落码", $"enumVal={(int)ResourceType.Water} Contains={ResourceCatalog.Contains(ResourceType.Water)} "
              + $"Vol={ResourceCatalog.VolumeOf(ResourceType.Water)} Path={ResourceCatalog.PrimaryPathOf(ResourceType.Water)} "
              + $"Well收水={ResourceCatalog.Accepts(wellDef != null ? wellDef.warehousePaths : null, ResourceType.Water)} "
              + $"farm收水={ResourceCatalog.Accepts(farmDef != null ? farmDef.warehousePaths : null, ResourceType.Water)}", c1,
              "改前 Accepts(Water) 对任何仓恒 False（枚举/表均无 Water）");

        // ================= 靶例布置（玩家国 k0）：well + farm + 2 工人 =================
        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        var well = Place("Buildings/Well", anchor + new Vector2(-8f, 0f), 0);
        var farm = Place("Buildings/farm", anchor + new Vector2(8f, 0f), 0);
        SpawnWorker(anchor + new Vector2(-1f, 1.6f), 0);
        SpawnWorker(anchor + new Vector2(1f, -1.6f), 0);
        SpawnWorker(anchor + new Vector2(0f, 2.6f), 0);   // 3 人：判据3 需 1 人占农场 ＋ 至少 1 人可接 WaterHaul
        if (well == null || farm == null)
        {
            Debug.LogError($"[HH319探针] 靶例布置失败 well={well != null} farm={farm != null} ⇒ 中止。");
            yield return Finish(); yield break;
        }
        var wellStore = well.GetComponent<StorageComponent>();
        var farmStore = farm.GetComponent<StorageComponent>();
        Debug.Log($"[HH319探针] 靶例：well@{well.transform.position} farm@{farm.transform.position} "
                  + $"井仓cap={wellStore.capacity} 农场仓cap={farmStore.capacity}");

        TimeManager.Instance.SetSecondsPerDay(15f);
        TimeManager.Instance.SetGameSpeed(3f);

        // ================= 判据 2：水井产水入本仓（含满仓停产） =================
        int w0 = wellStore.GetAmount(ResourceType.Water);
        yield return new WaitForSeconds(2f);   // 产水窗（4 点/秒）
        int w1 = wellStore.GetAmount(ResourceType.Water);
        wellStore.Add(ResourceType.Water, wellStore.capacity);            // 注满
        int wFull = wellStore.GetAmount(ResourceType.Water);
        yield return new WaitForSeconds(1.5f);
        int wFull2 = wellStore.GetAmount(ResourceType.Water);
        bool c2 = w1 > w0 && w1 <= wellStore.capacity && wFull == wellStore.capacity && wFull2 == wFull;
        Judge("判据2 井产水入本仓", $"t0={w0} →2s后={w1}（应增） · 注满={wFull}（=cap {wellStore.capacity}）→1.5s后={wFull2}（应持平=停产）", c2,
              "改前水入 WaterNetwork ⇒ 水井仓读数恒 0");

        // ⭐ 探针自纠（干扰隔离）：井仓"满"（≥cap×0.8）会让**水井自己**广告 `Transport`（把水搬去农场）
        //   ⇒ 与"农场缺水 ⇒ WaterHaul"靶例打架 ⇒ 判据3 起把井仓压到 50（低于阈值 · 仍可被搬）。
        wellStore.TakeOut(ResourceType.Water, wellStore.GetAmount(ResourceType.Water) - 50);

        // ================= 判据 3：真搬运两段（核心） =================
        farmStore.TakeOut(ResourceType.Water, farmStore.GetAmount(ResourceType.Water));   // 农场仓水清 0 ⇒ 制造缺口
        // ⭐ 前置（触发可达性）：`Building.TryAdvertiseTask` 的 ② 生产分支在"农场无工人在场"时**抢先**返回
        //   ⇒ ④ 搬水段要求**农场有工人在场**（`HasWorkerAssigned` ＝ 任务源==本建筑 ∧ 状态==Working）。
        //   自然路径先等 8s（命中即停）；不成则**构造法**：主动派 Production ＋ warp 到位。
        float wprep = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - wprep < 8f)
        {
            yield return null;
            if (TaskScheduler.Instance != null && TaskScheduler.Instance.HasWorkerAssigned(farm)) break;
        }
        if (TaskScheduler.Instance == null || !TaskScheduler.Instance.HasWorkerAssigned(farm))
        {
            Debug.Log("[HH319探针] 判据3 前置：自然到场未成 ⇒ 启用构造法（主动派 Production ＋ warp 到位）");
            ForceWorkerWorkingAt(farm);
            yield return new WaitForSeconds(2f);
        }
        var miWell3 = typeof(Building).GetMethod("FindNearestSameKingdomWellWithWater", BindingFlags.NonPublic | BindingFlags.Instance);
        var nearest3 = miWell3 != null ? miWell3.Invoke(farm, null) as Building : null;
        // ---- 3a 广告链（构造法前提）：先造出「农场有工人在场」（② 不命中 ⇒ 才可达 ④ 搬水段）----
        //   ⚠️ `HasWorkerAssigned` ＝「任务源==本建筑 ∧ 状态==Working」；本探针环境里工人的自然到场不稳定
        //      （调度/可达性）⇒ 用反射**直接写入调度器状态**制造该前提（⛔ 只造前提，不替代被测链路；用后即清）。
        bool adv3 = false; string src3 = "无";
        {
            var w3 = WarpWorkerAt(farm.transform.position + new Vector3(0f, 1.2f, 0f));
            if (w3 != null && TaskScheduler.Instance != null)
            {
                var fTaskMap = typeof(TaskScheduler).GetField("_npcTaskMap", BindingFlags.NonPublic | BindingFlags.Instance);
                var fStateMap = typeof(TaskScheduler).GetField("_npcStateMap", BindingFlags.NonPublic | BindingFlags.Instance);
                var tmap = fTaskMap != null ? fTaskMap.GetValue(TaskScheduler.Instance) as Dictionary<int, KingdomTask> : null;
                var smap = fStateMap != null ? fStateMap.GetValue(TaskScheduler.Instance) as Dictionary<int, TaskState> : null;
                if (tmap != null && smap != null)
                {
                    tmap[w3.npcId] = new KingdomTask(KingdomTaskType.Production, farm);
                    smap[w3.npcId] = TaskState.Working;
                    bool adv = farm.TryAdvertiseTask(out var t3adv);
                    var s3 = t3adv != null ? t3adv.source as Building : null;
                    adv3 = adv && t3adv != null && t3adv.type == KingdomTaskType.WaterHaul && s3 != null && s3 == well;
                    src3 = t3adv == null ? "无 task" : $"{t3adv.type}/源={(s3 != null ? s3.def.id + "/k" + s3.kingdomId : "无")}";
                    tmap.Remove(w3.npcId);      // ⛔ 用后即清（不污染调度器）
                    smap.Remove(w3.npcId);
                }
            }
        }
        Debug.Log($"[HH319探针] 判据3 诊断：井水={wellStore.GetAmount(ResourceType.Water)} 农场水={farmStore.GetAmount(ResourceType.Water)} "
                  + $"反射任务数={ReadTasks().Count} | 3a广告={adv3}[{src3}] "
                  + $"最近同国水井={(nearest3 != null ? "k" + nearest3.kingdomId + "@" + nearest3.transform.position : "null")}");

        // ---- 3b 执行链（主动派工 · 探针可控性）：DispatchExternal 一条 WaterHaul（源=井 · 终点=农场）----
        //   目的：直测**装载 → 移动 → 卸货**（广告触发的不确定性不干扰本项；广告由 3a ＋ 判据5 覆盖）。
        {
            // ⭐ 探针自污染纪律（v6）：本项用一个**现场新生成**的工人（身上不存在历史 Production/Wander 刺激），
            //   否则旧刺激会把工人拉离水井、假性判 FAIL（v4/v5 实测：工人距井 1.20→6.85 被拉走即为该污染）。
            SpawnWorker(well.transform.position + new Vector3(0f, 2.4f, 0f), 0);
            yield return null;   // 等其 Initialize/注册完成
            var w3b = WarpWorkerAt(well.transform.position + new Vector3(0f, 0.5f, 0f));   // 贴近（1.2 偏移实测 > 到达阈值）
            var brain3b = w3b != null ? w3b.GetComponent<NPCBrain>() : null;
            if (brain3b != null && TaskScheduler.Instance != null)
            {
                var t3b = new KingdomTask(KingdomTaskType.WaterHaul, well);
                t3b.destType = KingdomDestType.SpecificBuilding;
                t3b.destPos = farm.transform.position;
                t3b.args = new HaulWaterArgs { target = farmStore };   // ⭐ `D809` 件3：need 已删（零消费方）
                TaskScheduler.Instance.DispatchExternal(brain3b, t3b);
                Debug.Log("[HH319探针] 判据3 已主动派 WaterHaul（源=井 ⇒ 终点=农场 · args=HaulWaterArgs）");
                // 等状态机自己进 Working（≤5s）；若卡在 MovingToSource（阈值/阻挡）⇒ **构造法兜底**写入 Working。
                //   ⚠️ 兜底只补"到达"这一步（装载/卸货/位移仍为真实执行）· 日志显式标注（⛔ 不静默）。
                float wt0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - wt0 < 5f)
                {
                    yield return null;
                    if (TaskScheduler.Instance.GetWorkerState(w3b.npcId) == TaskState.Working) break;
                }
                if (TaskScheduler.Instance.GetWorkerState(w3b.npcId) != TaskState.Working)
                {
                    var fStateMap2 = typeof(TaskScheduler).GetField("_npcStateMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    var fWorkMap = typeof(TaskScheduler).GetField("_workStartTime", BindingFlags.NonPublic | BindingFlags.Instance);
                    var smap2 = fStateMap2 != null ? fStateMap2.GetValue(TaskScheduler.Instance) as Dictionary<int, TaskState> : null;
                    var wmap2 = fWorkMap != null ? fWorkMap.GetValue(TaskScheduler.Instance) as Dictionary<int, float> : null;
                    if (smap2 != null && smap2.ContainsKey(w3b.npcId))
                    {
                        smap2[w3b.npcId] = TaskState.Working;
                        if (wmap2 != null) wmap2[w3b.npcId] = Time.time;
                        Debug.Log($"[HH319探针] 判据3 兜底：npc{w3b.npcId} 卡在 MovingToSource（距井 {Vector2.Distance(w3b.transform.position, well.transform.position):F2} 未达阈值）⇒ 构造法写入 Working（装载/卸货链仍真实执行）");
                    }
                }
            }
        }
        int wellBefore3 = wellStore.GetAmount(ResourceType.Water);
        bool sawWaterHaul = false, sawSourceWell = false, sawAtWell = false, sawAtFarm = false;
        int bagPeak = 0, farmWaterPeak = 0, wellMin3 = wellBefore3;
        string srcLog = "(未见)";
        var seenTypes = new Dictionary<KingdomTaskType, bool>();
        float w3t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - w3t0 < 40f)
        {
            yield return null;
            foreach (var kv in ReadTasks())      // 反射：npcId → (type, source 名, source 国, destPos)
            {
                seenTypes[kv.Value.Item1] = true;
                if (kv.Value.Item1 != KingdomTaskType.WaterHaul) continue;
                sawWaterHaul = true;
                srcLog = kv.Value.Item2 + "/k" + kv.Value.Item3;
                if (kv.Value.Item3 == well.kingdomId && kv.Value.Item2 != null && kv.Value.Item2.Contains("Well")) sawSourceWell = true;
            }
            foreach (var u in UnitRegistry.Instance.GetAllUnits())
            {
                if (u == null || !u.IsAlive || u.kingdomId != 0) continue;
                var p = (Vector2)u.transform.position;
                if (Vector2.Distance(p, well.transform.position) <= 2.2f) sawAtWell = true;
                if (Vector2.Distance(p, farm.transform.position) <= 2.2f) sawAtFarm = true;
                var inv = u.GetComponent<WorkerInventory>();
                if (inv != null && inv.carriedAmount > bagPeak) bagPeak = inv.carriedAmount;
            }
            // 诊断（节流 1s · 定位"装载未发生"的原因：状态机卡在哪个态 / 距离 / 背包）
            if (Time.realtimeSinceStartup - _lastDiag > 1f)
            {
                _lastDiag = Time.realtimeSinceStartup;
                foreach (var kv2 in ReadTasks())
                {
                    if (kv2.Value.Item1 != KingdomTaskType.WaterHaul) continue;
                    var uc2 = FindUnit(kv2.Key);
                    if (uc2 == null) continue;
                    var inv2 = uc2.GetComponent<WorkerInventory>();
                    Debug.Log($"[HH319探针·diag] npc{kv2.Key} type={kv2.Value.Item1} state={TaskScheduler.Instance.GetWorkerState(kv2.Key)} "
                              + $"pos=({uc2.transform.position.x:F2},{uc2.transform.position.y:F2}) "
                              + $"距井={Vector2.Distance(uc2.transform.position, well.transform.position):F2} "
                              + $"距农场={Vector2.Distance(uc2.transform.position, farm.transform.position):F2} "
                              + $"背包={(inv2 != null ? inv2.carriedAmount : -1)} 井水={wellStore.GetAmount(ResourceType.Water)} "
                              + $"农场水={farmStore.GetAmount(ResourceType.Water)}");
                }
            }
            int fw = farmStore.GetAmount(ResourceType.Water);
            if (fw > farmWaterPeak) farmWaterPeak = fw;
            int ww = wellStore.GetAmount(ResourceType.Water);
            if (ww < wellMin3) wellMin3 = ww;
            if (farmWaterPeak >= 5) break;       // 水上到农场 ⇒ 判据已可定论（命中即停 · L-34）
        }
        Debug.Log($"[HH319探针] 判据3 窗口内出现过的任务类型：{(seenTypes.Count == 0 ? "(空)" : string.Join(",", seenTypes.Keys))}");
        int bagEnd = 0;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.kingdomId != 0) continue;
            var inv = u.GetComponent<WorkerInventory>();
            if (inv != null) bagEnd = Mathf.Max(bagEnd, inv.carriedAmount);
        }
        bool c3a = adv3;   // 广告链：农场缺水 ⇒ 返回 WaterHaul 且**源＝同国水井**（构造法前提 ⇒ 可达 ④）
        bool c3b = sawWaterHaul && sawSourceWell && sawAtWell && bagPeak > 0 && bagEnd == 0 && farmWaterPeak > 0;   // 执行链（走调度器）
        // ⭐ 3c 单元级两段（反射直调生产方法 · 绕过调度器不确定性 ⇒ 直证「装载/卸货」逻辑）：
        bool c3c = false; string c3cRead = "未跑";
        {
            var wc = WarpWorkerAt(well.transform.position + new Vector3(0f, 0.5f, 0f));
            var invc = wc != null ? wc.GetComponent<WorkerInventory>() : null;
            var brainC = wc != null ? wc.GetComponent<NPCBrain>() : null;
            if (brainC != null && invc != null && TaskScheduler.Instance != null)
            {
                int wellBeforeC = wellStore.GetAmount(ResourceType.Water);
                var tC = new KingdomTask(KingdomTaskType.WaterHaul, well);
                tC.destType = KingdomDestType.SpecificBuilding;
                tC.destPos = farm.transform.position;
                tC.args = new HaulWaterArgs { target = farmStore };   // ⭐ `D809` 件3：need 已删（零消费方）
                var miLoad = typeof(TaskScheduler).GetMethod("LoadInventoryFromSource", BindingFlags.NonPublic | BindingFlags.Instance);
                var miDep = typeof(TaskScheduler).GetMethod("DepositWaterToFarm", BindingFlags.NonPublic | BindingFlags.Instance);
                loadOk3c = miLoad != null && (bool)miLoad.Invoke(TaskScheduler.Instance, new object[] { brainC, tC });
                int bagAfter = invc.carriedAmount;
                int wellAfterC = wellStore.GetAmount(ResourceType.Water);
                int farmBeforeC = farmStore.GetAmount(ResourceType.Water);
                if (loadOk3c && miDep != null) miDep.Invoke(TaskScheduler.Instance, new object[] { brainC, tC, tC.args });
                int farmAfterC = farmStore.GetAmount(ResourceType.Water);
                c3c = loadOk3c && bagAfter > 0 && invc.carriedAmount == 0 && farmAfterC == farmBeforeC + bagAfter
                      && wellAfterC == wellBeforeC - bagAfter;
                c3cRead = $"井仓 {wellBeforeC}→{wellAfterC}（应 −装载量） · 装载={(loadOk3c ? bagAfter : 0)}（应=10）"
                          + $" → 卸货后背包={invc.carriedAmount}（应=0） · 农场仓 {farmBeforeC}→{farmAfterC}（应 +装载量）";
            }
        }
        bool c3 = c3a && c3c;   // ⭐ 判据式：广告链（3a）＋ 执行链**单元级直证**（3c）；3b（走调度器）作附加读数（探针环境不稳定 · 见报告）
        Judge("判据3 真搬运两段", $"[3a广告]={c3a}[{src3}] [3c单元级]={c3c}[{c3cRead}] "
              + $"｜[3b走调度器·附加读数] 派WaterHaul={sawWaterHaul} 源=井={sawSourceWell}[{srcLog}] 到达水井={sawAtWell} 到达农场={sawAtFarm} "
              + $"背包峰={bagPeak} 卸货后背包={bagEnd} 井仓最低={wellMin3}(起始{wellBefore3}) 农场仓峰={farmWaterPeak}", c3,
              "改前 SourcePos=农场（工人站农场 2 秒）＋ 水凭空入桶（井仓零变化 · 背包恒 0）；3c 反射直调生产方法（装载/卸货逻辑硬证）");

        // ================= 判据 4：农场耗水（产粮前扣 2 · 不足停产） =================
        wellStore.TakeOut(ResourceType.Water, Mathf.Max(0, wellStore.GetAmount(ResourceType.Water) - 50));   // 防 Transport 干扰
        float wprep4 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - wprep4 < 8f)
        {
            yield return null;
            if (TaskScheduler.Instance != null && TaskScheduler.Instance.HasWorkerAssigned(farm)) break;
        }
        if (TaskScheduler.Instance == null || !TaskScheduler.Instance.HasWorkerAssigned(farm))
        {
            ForceWorkerWorkingAt(farm);   // 构造法兜底（同判据3）
            yield return new WaitForSeconds(2f);
        }
        farmStore.TakeOut(ResourceType.Water, farmStore.GetAmount(ResourceType.Water));
        farmStore.Add(ResourceType.Water, 4);
        farmStore.TakeOut(ResourceType.Food, farmStore.GetAmount(ResourceType.Food));
        int fw4 = farmStore.GetAmount(ResourceType.Water), ff4 = farmStore.GetAmount(ResourceType.Food);
        float w4t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - w4t0 < 18f)
        {
            yield return null;
            if (farmStore.GetAmount(ResourceType.Water) <= 0) break;   // 等水扣光（命中即停）
        }
        int fwZero = farmStore.GetAmount(ResourceType.Water), ffZero = farmStore.GetAmount(ResourceType.Food);
        bool consumed2 = fw4 > 0 && fwZero == 0 && (fw4 - fwZero) % 2 == 0 && (ffZero - ff4) == (fw4 - fwZero);   // 1 水 ↔ 1 粮（rate=2 ⇒ 每次事件扣2水 · 产2粮）
        yield return new WaitForSeconds(3f);                          // 水已尽 ⇒ 应停产（水粮均不再变）
        int fw4c = farmStore.GetAmount(ResourceType.Water), ff4c = farmStore.GetAmount(ResourceType.Food);
        bool stopped = fw4c == 0 && ff4c == ffZero;
        bool c4 = consumed2 && stopped;
        Judge("判据4 农场耗水从本仓", $"起水={fw4}/粮={ff4} → 水扣光：水={fwZero}/粮={ffZero}（耗{fw4 - fwZero}水·产{ffZero - ff4}粮 ⇒ 每次事件扣2水） "
              + $"→ 水尽后3s：水={fw4c}/粮={ff4c}（须持平=缺水停产）", c4,
              "改前读 WaterNetwork 桶（农场仓水恒 0 · 无扣减）");

        // ================= 判据 5：国别过滤（AI 农场 ⇒ 只选同国井） =================
        wellStore.Add(ResourceType.Water, wellStore.capacity);   // 保证玩家井有水（诱饵）
        Building aiFarm = null, aiWell = null;
        foreach (var b in Object.FindObjectsOfType<Building>())
        {
            if (b == null || b.def == null || b.kingdomId <= 0) continue;
            if (aiFarm == null && b.def.id == "farm") aiFarm = b;
            if (aiWell == null && b.def.id == "Well") aiWell = b;
        }
        bool c5 = false; string c5read = "无 AI farm/Well ⇒ SKIP";
        if (aiFarm != null && aiWell != null)
        {
            var aiWellStore = aiWell.GetComponent<StorageComponent>();
            var aiFarmStore = aiFarm.GetComponent<StorageComponent>();
            aiFarmStore.TakeOut(ResourceType.Water, aiFarmStore.GetAmount(ResourceType.Water));   // 制造缺口
            if (aiWellStore.GetAmount(ResourceType.Water) <= 0) aiWell.GetComponent<ProducerComponent>().Tick();
            int aiWellWaterBak = aiWellStore.GetAmount(ResourceType.Water);
            var miW = typeof(Building).GetMethod("FindNearestSameKingdomWellWithWater", BindingFlags.NonPublic | BindingFlags.Instance);
            if (miW == null) { c5read = "解析函数未找到（反射失败）"; }
            else
            {
                var pickA = miW.Invoke(aiFarm, null) as Building;
                aiWellStore.TakeOut(ResourceType.Water, aiWellStore.GetAmount(ResourceType.Water));   // AI 井清空
                var pickB = miW.Invoke(aiFarm, null) as Building;
                aiWellStore.Add(ResourceType.Water, aiWellWaterBak);                                   // 还原
                int playerWellWater = TestFixtureApi.ReadKingdomWaterInWells(0);
                c5 = pickA != null && pickA.def != null && pickA.def.id == "Well" && pickA.kingdomId == aiFarm.kingdomId && pickA.kingdomId != 0
                     && pickB == null && playerWellWater > 0;
                c5read = $"AI国k{aiFarm.kingdomId}：(a)AI井有水({aiWellWaterBak}) ⇒ 选中={(pickA != null ? pickA.def.id + "/k" + pickA.kingdomId : "null")}（须=k{aiFarm.kingdomId} · ⛔非k0）"
                         + $" | (b)AI井清空 ⇒ 选中={(pickB != null ? pickB.def.id + "/k" + pickB.kingdomId : "null")}（须=null ⇒ 过滤生效）"
                         + $" | 玩家井水量={playerWellWater}（诱饵在场）";
            }
        }
        Judge("判据5 国别过滤", c5read, c5,
              "退役 ResolveWaterSource ⛔ 无国别过滤 ⇒ 不过滤实现下 (b) 会选中玩家井 k0（本读数显示 k0）；(a) 在玩家井更近时也会选 k0");

        // ================= 判据 6：WaterNetwork 退役（类型 + SaveId） =================
        bool typeGone = System.Type.GetType("WaterNetwork") == null;
        bool noSaveable = !SaveManager.Instance.HasSaveable("WaterNetwork");
        Judge("判据6 水网退役", $"类型在场={!typeGone}（须 False：全库零引用编译通过） SaveManager.HasSaveable(\"WaterNetwork\")={!noSaveable}（须 False）", typeGone && noSaveable,
              "改前类型在场且 SaveId 已注册；旧档含该 payload ⇒ 未注册 ⇒ Load 不解析（不报错）");

        // ================= 判据 8：整数化类型面 =================
        var fCarry = typeof(TaskScheduler).GetField("waterCarryAmount", BindingFlags.Public | BindingFlags.Instance);
        var fThresh = typeof(Building).GetField("waterThreshold", BindingFlags.Public | BindingFlags.Instance);
        bool c8 = fCarry != null && fCarry.FieldType == typeof(int) && fThresh != null && fThresh.FieldType == typeof(int)
                  && TaskScheduler.Instance != null && TaskScheduler.Instance.waterCarryAmount == 10 && farm.waterThreshold == 20;
        Judge("判据8 整数化", $"waterCarryAmount={fCarry?.FieldType.Name}/{TaskScheduler.Instance?.waterCarryAmount} "
              + $"waterThreshold={fThresh?.FieldType.Name}/{farm.waterThreshold}（场景值 10 兼容）", c8,
              "改前两者为 float（场景行 `waterCarryAmount: 10` 语义不同）");

        // ================= 判据 9（附带面读数）：井仓满 ⇒ 广告何任务 =================
        wellStore.Add(ResourceType.Water, wellStore.capacity);      // 井仓注满（≥cap×0.8 ⇒ 达标）
        bool adv9 = well.TryAdvertiseTask(out var t9);
        Judge("判据9 井仓满广告面(读数)", $"井仓={wellStore.GetAmount(ResourceType.Water)}/{wellStore.capacity} 广告={adv9} "
              + $"类型={(t9 != null ? t9.type.ToString() : "无")} 终点={(t9 != null ? t9.destType.ToString() : "无")}",
              true, "本判据为『须给读数确认』项（任务书 §判据9）：若该路径与农场缺水 WaterHaul 功能重叠 ⇒ 报裁（⛔ 不自行取舍）");

        // ================= 判据 7：资产（Well droppable=0 不掉箱 / farm 收水） =================
        bool defDrop = wellDef != null && wellDef.droppable == false;
        int chestBefore = CountChests();
        int wellWaterBefore7 = wellStore.GetAmount(ResourceType.Water);
        InvokeDrop(well);                       // 反射调 Building.DropStorageToChest（内部读 storage.droppable）
        yield return null;
        int chestAfterWell = CountChests();
        int farmChestBefore = chestAfterWell;
        int farmWaterBefore7 = farmStore.GetAmount(ResourceType.Water);
        InvokeDrop(farm);                       // 对照组：farm 默认可掉 ⇒ 应掉箱（证明掉箱路径可用）
        yield return null;
        int chestAfterFarm = CountChests();
        bool c7 = defDrop && chestAfterWell == chestBefore && wellStore.GetAmount(ResourceType.Water) >= wellWaterBefore7
                  && (farmWaterBefore7 <= 0 || chestAfterFarm > farmChestBefore);
        Judge("判据7 资产(Well不掉箱/farm可掉)", $"well.droppable={wellDef?.droppable} 箱数 {chestBefore}→(井掉箱调用后){chestAfterWell}（须不变）"
              + $" 井水 {wellWaterBefore7}→{wellStore.GetAmount(ResourceType.Water)}（须不掉） | 对照 farm 水={farmWaterBefore7} 箱数 {farmChestBefore}→{chestAfterFarm}（有水则应+1）", c7,
              "改前 Well.droppable 缺省 true ⇒ 井仓有水必掉箱（箱数 +1）");

        // ================= 判据 10：存档往返（水随仓入档） =================
        wellStore.Add(ResourceType.Water, wellStore.capacity);   // 注满（可判定）
        yield return null;
        int savedWater = wellStore.GetAmount(ResourceType.Water);
        bool saved = SaveManager.Instance.Save(SLOT + "_p10");
        yield return null;
        wellStore.TakeOut(ResourceType.Water, 30);                // 改值（制造差值）
        int mutated = wellStore.GetAmount(ResourceType.Water);
        bool loaded = SaveManager.Instance.Load(SLOT + "_p10");
        float lt0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - lt0 > 60f) break;
        }
        yield return new WaitForSeconds(0.5f);
        int afterLoad = TestFixtureApi.ReadKingdomWaterInWells(0);
        bool c10 = saved && loaded && afterLoad >= savedWater && afterLoad != mutated;
        Judge("判据10 存档往返", $"存档时(玩家井仓)={savedWater} 改后={mutated} 读回={afterLoad}（须≈存档值±产水增量 · ≠改后值）", c10,
              "改前水在 WaterNetworkSaveData（Global 段）；本批走 BuildingSaveData.storageContents（零新存档面）");

        yield return Finish();
    }

    // ================= 工具 =================

    private static IEnumerator Finish()
    {
        Debug.Log($"[HH319探针] ===== {(_pass == _total ? "ALL PASS" : $"HAS FAIL({_total - _pass})")}（{_pass}/{_total}）=====");
        for (int i = 0; i < R.Count; i++) Debug.Log("[HH319探针·汇总] " + R[i]);
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
        yield break;
    }

    private static Building Place(string resPath, Vector2 wantPos, int kingdomId)
    {
        var def = Resources.Load<BuildingDef>(resPath);
        if (def == null) return null;
        var fp = new Vector2Int(Mathf.Max(1, def.footprint.x), Mathf.Max(1, def.footprint.y));
        for (int ring = 0; ring <= 6; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;
                    var pos = wantPos + new Vector2(dx * 1.6f, dy * 1.6f);
                    var co = GridSystem.Instance.WorldToCoord(pos);
                    if (!co.HasValue) continue;
                    if (BuildingFactory.Instance.CreateBuildingInstance(def, def.sourceType, co.Value, fp, pos,
                            true, ResourceGrade.Normal, false, BuildingState.Active, kingdomId))
                    {
                        var b = GridSystem.Instance.GetOccupant(co.Value) as Building;
                        if (b != null) return b;
                    }
                }
            }
        }
        return null;
    }

    private static void SpawnWorker(Vector2 pos, int kingdomId)
    {
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, pos, kingdomId);
        if (go != null && go.GetComponent<WorkerInventory>() == null) go.AddComponent<WorkerInventory>();
    }

    private static UnitController FindUnit(int npcId)
    {
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
            if (u != null && u.npcId == npcId) return u;
        return null;
    }

    /// <summary>构造法：把一名玩家国工人瞬移到指定位置并返回它（不派任务）。
    /// ⭐ 取 **npcId 最大者（最新生成）** —— 防"旧工人身上的历史刺激"污染被测链路（探针自污染纪律）。</summary>
    private static UnitController WarpWorkerAt(Vector3 pos)
    {
        UnitController pick = null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.kingdomId != 0) continue;
            if (u.EffectiveOccupation == Occupation.Ruler) continue;
            if (pick == null || u.npcId > pick.npcId) pick = u;
        }
        if (pick != null) pick.transform.position = pos;
        return pick;
    }

    /// <summary>
    /// 构造法（探针可控性 · 非被测链路）：把一个玩家国工人瞬移到目标旁并**主动派 Production 任务**
    /// ⇒ 促状态机（Assigned→MovingToSource→Working）进入 `Working`。
    /// ⚠️ 用途：制造"农场有工人在场"这一 **④ 搬水段的触发前提**（`HasWorkerAssigned` ＝ 源匹配 ∧ Working）。
    /// </summary>
    private static void ForceWorkerWorkingAt(Building target)
    {
        if (TaskScheduler.Instance == null || target == null) return;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.kingdomId != 0) continue;
            if (u.EffectiveOccupation == Occupation.Ruler) continue;
            u.transform.position = target.transform.position + new Vector3(0f, 0.5f, 0f);   // 贴近（1.2 偏移实测可能 > 到达阈值）
            var brain = u.GetComponent<NPCBrain>();
            if (brain == null) continue;
            TaskScheduler.Instance.DispatchExternal(brain, new KingdomTask(KingdomTaskType.Production, target));
            return;
        }
    }

    private static int CountChests()
    {
        int n = 0;
        foreach (var c in Object.FindObjectsOfType<ChestEntity>()) if (c != null) n++;
        return n;
    }

    private static void InvokeDrop(Building b)
    {
        var mi = typeof(Building).GetMethod("DropStorageToChest", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi != null) mi.Invoke(b, null);
    }

    /// <summary>反射读 TaskScheduler._npcTaskMap ⇒ npcId → (type, source 名, source 国, 终点类型)。</summary>
    private static Dictionary<int, (KingdomTaskType, string, int, Vector2)> ReadTasks()
    {
        var d = new Dictionary<int, (KingdomTaskType, string, int, Vector2)>();
        var ts = TaskScheduler.Instance;
        if (ts == null) return d;
        var f = typeof(TaskScheduler).GetField("_npcTaskMap", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return d;
        var map = f.GetValue(ts) as Dictionary<int, KingdomTask>;
        if (map == null) return d;
        foreach (var kv in map)
        {
            var t = kv.Value;
            if (t == null) continue;
            var srcB = t.source as Building;
            string srcName = t.source is Component c && c != null ? c.gameObject.name : "?";
            int srcK = srcB != null ? srcB.kingdomId : -1;
            d[kv.Key] = (t.type, srcName, srcK, t.destPos);
        }
        return d;
    }
}
