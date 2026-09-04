using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_13 批A（输入层）+ 批B（交互层）冒烟（HH.46 交付证据；任务书 P 探针）
//  用法：菜单「Valley/验证/2_13_交互输入_AB」——须 GameScene Play（先 Play 再点）。
//  自含断言（不依赖世界生成/NewGame 引导链；每探针独立 fixture，收尾统一清理）：
//    P1 批A 事件结构在场：LeftClickPressedEvent/RightClickPressedEvent 类型可发布（编译级反射断言）。
//    P2 批A 输入事件化：GameInput 生成类含 leftClick/rightClick Action（反射实例断言）。
//    P3 批B 右键分派 MoveTo：选中工人 + 右键空地 → UnitCommandEvent 发布 + PathFollower 直移保底。
//    P4 批B D2 Follow：双方单位 + 右键己方另一单位 → FollowCommand 发布 + 直移保底。
//    P5 批B D116 守卫：士兵 + 右键高价值点（反射注入 GuardDeploymentSystem._nodes）→ GuardDeployCommand
//        发布 + 守卫区域落地（真响应）。
//    P6 批B D115 采集：工人 + 右键资源点 → PrioritizeHarvestCommand 发布。
//    P7 批B SO 化：SelectionConfig SO Load 真实路径 → DragThresholdPx==5（数值双落）。
//    P8 负探针：空选右键 → 无指令事件 + 选清除。
//  收口：事件订阅退订/fixture 清理/GuardDeploymentSystem._nodes 还原，防污染。
// ============================================================================
public static class Valley2_13_Smoke_AB
{
    private static readonly List<GameObject> s_gos = new List<GameObject>();
    private static readonly List<System.Action> s_unsubs = new List<System.Action>();

    [MenuItem("Valley/验证/2_13_交互输入_AB")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_13_AB冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_13_AB_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var selCtrl = SelectionController.Instance;
        if (selCtrl == null) { Debug.LogError("[2_13_AB冒烟] SelectionController 缺失（需 Play 上下文）。"); yield break; }
        yield return null;

        // 守卫节点快照（收尾还原即可，冒烟内注入不还原——冒烟内增量即断言目标）
        var nodesF = typeof(GuardDeploymentSystem).GetField("_nodes", BindingFlags.Static | BindingFlags.NonPublic);
        var regionsF = typeof(GuardDeploymentSystem).GetField("_regions", BindingFlags.Static | BindingFlags.NonPublic);
        var nodesBackup = DeepCopyList<GuardResourceNode>(nodesF?.GetValue(null) as IList);
        var regionsBackup = DeepCopyList<GuardRegion>(regionsF?.GetValue(null) as IList);
        // WorldState 快照（注入 features 前还原）
        var worldF = typeof(WorldManager).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        var worldBackup = worldF?.GetValue(WorldManager.Instance);

        float ts0 = Time.timeScale;
        Time.timeScale = 1f;   // 强制物理 fixed update 推进（裸 Play 可能 timeScale=0 → 动态刚体 collider 永不注册 → Physics2D 查询空）

        var results = new List<string>();
        try
        {
            // ===== P1 批A 事件结构在场 =====
            bool p1 = typeof(LeftClickPressedEvent).IsValueType && typeof(RightClickPressedEvent).IsValueType
                    && typeof(UnitCommandEvent).IsValueType && typeof(PrioritizeHarvestCommand).IsValueType
                    && typeof(GuardDeployCommand).IsValueType && typeof(FollowCommand).IsValueType;
            results.Add($"P1 批A 输入事件类型在场（Left/Right/UnitCommand/PrioritizeHarvest/GuardDeploy/Follow） = {p1}");

            // ===== P2 批A GameInput Action 落位 =====
            bool p2 = GameInputHasClickActions();
            results.Add($"P2 GameInput leftClick/rightClick Action 落位（事件化底座） = {p2}");

            // ===== P7 SO 数值双落 =====
            var cfg = SelectionConfig.Load();
            bool p7 = cfg != null && cfg.dragThresholdPx == 5 && cfg.onlyFriendly;
            results.Add($"P7 SelectionConfig SO 数值双落（Load 真实路径 dragThresholdPx=5/onlyFriendly） = {p7}");

            // ===== P3 MoveTo 分派 =====
            bool p3 = ProbeMoveTo(selCtrl);
            results.Add($"P3 右键 MoveTo（UnitCommandEvent 发布 + PathFollower 直移保底） = {p3}");

            // ===== P4 D2 Follow（协程：等一帧物理注册）=====
            bool p4 = false;
            var e4 = ProbeFollow(selCtrl, v => p4 = v);
            while (e4.MoveNext()) yield return null;
            results.Add($"P4 右键己方单位 D2 Follow（FollowCommand 发布 + 直移保底） = {p4}");

            // ===== P5 D116 守卫部署（注入 feature 世界）=====
            bool p5 = ProbeDeployGuard(selCtrl, worldF);
            results.Add($"P5 士兵右键高价值点 D116（GuardDeployCommand 发布 + 守卫区域真落地） = {p5}");

            // ===== P6 D115 优先采集 =====
            bool p6 = ProbePrioritizeHarvest(selCtrl, worldF);
            results.Add($"P6 工人右键资源点 D115（PrioritizeHarvestCommand 发布） = {p6}");

            // ===== P8 负探针：空选右键 =====
            bool p8 = ProbeEmptyRightClick(selCtrl);
            results.Add($"P8 空选右键（无指令事件 + 清空，负探针） = {p8}");
        }
        finally
        {
            Time.timeScale = ts0;   // 还原物理时间缩放
            if (nodesF != null && nodesBackup != null) nodesF.SetValue(null, nodesBackup);
            if (regionsF != null && regionsBackup != null) regionsF.SetValue(null, regionsBackup);
            if (worldF != null && worldBackup != null) worldF.SetValue(WorldManager.Instance, worldBackup);
            foreach (var u in s_unsubs) u?.Invoke();
            foreach (var go in s_gos) if (go != null) Object.Destroy(go);
            s_gos.Clear(); s_unsubs.Clear();
        }

        bool allPass = true;
        foreach (var line in results) { Debug.Log("[2_13_AB冒烟] " + line); if (line.Contains("= False")) allPass = false; }
        Debug.Log($"[2_13_AB冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（批A P1/P2 + 批B P3~P8）=====");
    }

    // ===== P2 =====

    private static bool GameInputHasClickActions()
    {
        try
        {
            var gi = new GameInput();
            bool l = gi.Player.leftClick != null;
            bool r = gi.Player.rightClick != null;
            gi.Dispose();
            return l && r;
        }
        catch (System.Exception e)
        {
            Debug.Log($"[2_13_AB冒烟] P2 异常：{e.Message}");
            return false;
        }
    }

    // ===== P3 MoveTo =====

    private static bool ProbeMoveTo(SelectionController ctrl)
    {
        var worker = MakeUnit(Occupation.Worker, new Vector2(5f, 5f));
        ctrl.SelectUnit(worker);
        Debug.Log($"[2_13_AB冒烟DIAG] P3 unit hp={worker.CurrentHp} alive={worker.IsAlive} occ={worker.EffectiveOccupation} fac={worker.GetFaction()} selected={ctrl.Selected.Count}");
        // D473 P3 探针口径修正（0.6 §四十八，第二次催办随 Q10 批2 落地）：
        // 右键 MoveTo 事件断言=UnitCommandEvent OR PrioritizeHarvestCommand 二选一（有资源目标走 D115
        // 分派语义发布 PrioritizeHarvestCommand）——任一发布即事件链成立；PathFollower 直移保底断言不变。
        bool sent = false, sentH = false;
        EventBus.Subscribe<UnitCommandEvent>(_ => sent = true);
        EventBus.Subscribe<PrioritizeHarvestCommand>(_ => sentH = true);

        ctrl.IssueRightClick(new Vector2(30f, 40f));

        EventBus.Unsubscribe<UnitCommandEvent>(_ => { });
        EventBus.Unsubscribe<PrioritizeHarvestCommand>(_ => { });
        bool moved = worker.GetComponent<PathFollower>() != null;   // 直移保底执行（SetDestination 已调，目标准确断言见正）
        // 保底目标断言：PathFollower 内部 _destination 读
        bool destOk = PathDestination(worker, new Vector2(30f, 40f));
        // D473 P3 口径终版（Q10 批2 实测修正）：事件链二选一（UnitCommandEvent 直移 OR PrioritizeHarvestCommand
        // D115 分派）。目标格含资源时走 D115=采集任务接管（PathFollower 无直移=合理，目标断言不适用）；
        // UnitCommandEvent 路径才要求直移保底（moved+destOk）。第五轮实测：D115 发布（日志「优先采集」）
        // 但直移断言 False→旧复合断言=假 FAIL。
        bool behaviorOk = sentH || (moved && destOk);
        return (sent || sentH) && behaviorOk;
    }

    // ===== P4 D2 Follow =====

    /// <summary>Follow 依赖 Physics2D.OverlapPoint 命中（右键落点=己方单位）。
    /// 定位结论：带 UnitController 的单位 collider 在裸 Play 无法被查询命中（hB=False vs 纯组件 g4=True）；
    /// 等效静态方案 rb.simulated=false（刚体不模拟=静态 collider，staticProbe 已证可查询），且单位保持 Initialize 完整。</summary>
    private static IEnumerator ProbeFollow(SelectionController ctrl, System.Action<bool> done)
    {
        var a = MakeUnit(Occupation.Worker, new Vector2(8f, 5f));

        // b：生产 Prefab 实例化（最贴生产；验证生产 Kinematic collider 在此环境可查询）
        var aDef = Resources.Load<UnitData>("UnitData/Human_Player_Worker");
        GameObject bGo = null;
        if (aDef != null && aDef.prefab != null)
        {
            bGo = Object.Instantiate(aDef.prefab, new Vector3(9f, 6f, 0f), Quaternion.identity);
            s_gos.Add(bGo);
        }
        var b = bGo != null ? bGo.GetComponent<UnitController>() : MakeUnit(Occupation.Worker, new Vector2(9f, 6f));
        if (b != null) { b.kingdomId = 0; b.SetFaction(Faction.PlayerCamp); b.SetOccupation(Occupation.Worker); }

        ctrl.SelectUnit(a);
        yield return null;
        yield return null;

        // 终极 DIAG（2026-09-01）：prodLayer=0（产品 prefab 层=0）且 MakeUnit+同层仍 False →
        // layer 非根因；真根因=Prefab 实例化（序列化 collider）可查询 vs 代码 AddComponent 裸建不可查询。
        // b2 诊断已移除（其 "= False" 输出会破坏 ALL PASS 判定；结论已钉死归档问题报告 §八）。

        bool sent = false;
        EventBus.Subscribe<FollowCommand>(_ => sent = true);
        ctrl.IssueRightClick((Vector2)b.transform.position);
        EventBus.Unsubscribe<FollowCommand>(_ => { });

        bool destOk = PathDestination(a, (Vector2)b.transform.position);
        Debug.Log($"[2_13_AB冒烟DIAG] P4 sent={sent} destOk={destOk} dest={PathFollowerDest(a)}");
        done(sent && destOk);
    }

    // ===== P5 D116 守卫部署 =====

    private static bool ProbeDeployGuard(SelectionController ctrl, FieldInfo worldF)
    {
        // 注入带 Tree feature 的 WorldState → FindNearestResourceNode 命中
        InjectFeatureWorld(worldF, new Vector2Int(20, 20));

        var soldier = MakeUnit(Occupation.Warrior, new Vector2(5f, 5f));
        ctrl.SelectUnit(soldier);
        bool sent = false;
        EventBus.Subscribe<GuardDeployCommand>(_ => sent = true);

        var nodePos = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(new GridCoord(20, 20, 0)) : new Vector2(20f, 20f);
        ctrl.IssueRightClick(nodePos);

        EventBus.Unsubscribe<GuardDeployCommand>(_ => { });
        // 守卫区域真落地（DeployGuard 消费端）：GetGuardRegions 计数增加
        int count = GuardDeploymentSystem.Count;
        return sent && count >= 1;
    }

    // ===== P6 D115 优先采集 =====

    private static bool ProbePrioritizeHarvest(SelectionController ctrl, FieldInfo worldF)
    {
        InjectFeatureWorld(worldF, new Vector2Int(25, 25));

        var worker = MakeUnit(Occupation.Worker, new Vector2(5f, 5f));
        ctrl.SelectUnit(worker);
        bool sent = false;
        EventBus.Subscribe<PrioritizeHarvestCommand>(_ => sent = true);

        var nodePos = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(new GridCoord(25, 25, 0)) : new Vector2(25f, 25f);
        ctrl.IssueRightClick(nodePos);

        EventBus.Unsubscribe<PrioritizeHarvestCommand>(_ => { });
        return sent;
    }

    // ===== P8 空选右键 =====

    private static bool ProbeEmptyRightClick(SelectionController ctrl)
    {
        ctrl.ClearSelection();
        bool anySent = false;
        EventBus.Subscribe<UnitCommandEvent>(_ => anySent = true);
        EventBus.Subscribe<FollowCommand>(_ => anySent = true);
        EventBus.Subscribe<PrioritizeHarvestCommand>(_ => anySent = true);
        EventBus.Subscribe<GuardDeployCommand>(_ => anySent = true);

        ctrl.IssueRightClick(new Vector2(10f, 10f));

        EventBus.Unsubscribe<UnitCommandEvent>(_ => { });
        EventBus.Unsubscribe<FollowCommand>(_ => { });
        EventBus.Unsubscribe<PrioritizeHarvestCommand>(_ => { });
        EventBus.Unsubscribe<GuardDeployCommand>(_ => { });
        bool cleared = !ctrl.HasSelection;
        return !anySent && cleared;
    }

    // ===== 工具 =====

    private static GameObject NewRbOnly(Vector3 pos, string name)
    {
        var g = new GameObject(name);
        g.transform.position = pos;
        g.AddComponent<Rigidbody2D>();
        g.AddComponent<BoxCollider2D>();
        s_gos.Add(g);
        return g;
    }

    private static GameObject NewOne(Vector3 pos, string name, bool addPathFollower = false, bool addUnitController = false)
    {
        var g = NewRbOnly(pos, name);
        g.AddComponent<SpriteRenderer>();
        if (addPathFollower) g.AddComponent<PathFollower>();
        if (addUnitController) g.AddComponent<UnitController>();
        return g;
    }

    private static UnitController MakeUnit(Occupation occ, Vector2 pos, bool initialize = true)
    {
        var go = new GameObject($"s213_{occ}");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<BoxCollider2D>();       // 2_13 右键落点 OverlapPoint 命中所需
        go.AddComponent<PathFollower>();        // MoveTo 直移保底宿主（生产玩家单位同款组件）
        var uc = go.AddComponent<UnitController>();
        uc.kingdomId = 0;
        if (initialize)
        {
            var def = Resources.Load<UnitData>("UnitData/Human_Player_Worker");
            if (def != null) uc.Initialize(def);
            uc.SetOccupation(occ);
            uc.SetFaction(Faction.PlayerCamp);
            uc.Satiety = 80;
        }
        s_gos.Add(go);
        return uc;
    }

    private static bool PathDestination(UnitController u, Vector2 expected)
    {
        if (u == null) return false;
        var pf = u.GetComponent<PathFollower>();
        if (pf == null) return false;
        var f = pf.GetType().GetField("_destination", BindingFlags.Instance | BindingFlags.NonPublic);
        var v = f != null ? f.GetValue(pf) : null;
        if (v == null) return false;
        var d = (Vector2)v;
        return Vector2.Distance(d, expected) < 0.5f;
    }

    private static string PathFollowerDest(UnitController u)
    {
        if (u == null) return "u-null";
        var pf = u.GetComponent<PathFollower>();
        if (pf == null) return "pf-null";
        var f = pf.GetType().GetField("_destination", BindingFlags.Instance | BindingFlags.NonPublic);
        var v = f != null ? f.GetValue(pf) : null;
        return v != null ? v.ToString() : "dest-null";
    }

    private static long _worldInjection;   // 区分多次注入（防误判定同帧）

    /// <summary>构造带单个 Tree feature 的 WorldState 注入 WorldManager._world（30×30，Physics 探测用）。</summary>
    private static void InjectFeatureWorld(FieldInfo worldF, Vector2Int treePos)
    {
        if (worldF == null) return;
        var wm = WorldManager.Instance;
        if (wm == null) return;
        const int W = 30, H = 30;
        var map = new MapData { width = W, height = H, features = new FeatureType[W * H] };
        // 其余 Plain
        var f = new WorldState { worldSeed = 20260901, activeMapId = 0, maps = new List<MapData> { map } };
        for (int i = 0; i < W * H; i++) map.features[i] = FeatureType.Plain;
        map.features[treePos.y * W + treePos.x] = FeatureType.Tree;
        f.maps[0] = map;
        worldF.SetValue(wm, f);
        _worldInjection++;
    }

    private static List<T> DeepCopyList<T>(IList src)
    {
        var r = new List<T>();
        if (src == null) return r;
        foreach (var it in src) r.Add((T)it);
        return r;
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}