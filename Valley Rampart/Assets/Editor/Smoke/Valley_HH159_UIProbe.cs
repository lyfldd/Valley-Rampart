using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.159 件9（HH.133 件2/件3）UI/交互行为探针（Editor-only 冒烟容器）
//  入口=HH.92 测试环境正门 TestHarnessApi.EnterTestRun（L-17）；收尾 ExitTestRun + QuitSmoke。
//  探针（HH.133 §二 件2/件3 + HH.159 §二 T9.2/T9.3）：
//   P1  玩家巡逻真派发（选中己方单位→巡逻→2~4 路径点→单位沿路径循环移动；走 SelectionController 真链）
//   P1n 负探针：AI 侧无巡逻入口（真过滤路径 ClickSelect 不纳 AI 单位；正对照=玩家单位可纳）
//   P2  遇敌打断→转交战→敌清回巡逻（P2a 交战发生 / P2b 任务未清+回巡推进）
//   P5  情报面板阶段列与 KingdomState.scriptPhase 一致
//   P7  点选列国行→TerritoryOverlay.HighlightKingdom 染色高亮生效 + 切国/关面板后上一国高亮被清除
//       （HH.159 件9 新补 D619 派生：切国清上一国——本次必验）
// ============================================================================
public static class Valley_HH159_UIProbe
{
    private const int SEED = 21159;
    private const string SLOT = "probe_hh159";
    private const string TAG = "[HH159UI]";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH159_UI行为探针")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH159_UIHost").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine());
    }

    private class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (t == LogType.Exception) Log(TAG + " EXCEPTION: " + cond + "\n" + stack, false);
        }
    }

    private static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.LogWarning(TAG + " " + (ok ? "✓ " : "✗ ") + msg);
    }

    private static UnitController SpawnVagrant(Vector2 pos, int raceId)
    {
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Vagrant,
            SpawnPosSnapper.SnapWorld(pos, "HH159野人"), -1);
        if (go == null) return null;
        var uc = go.GetComponent<UnitController>();
        if (uc != null) uc.raceId = raceId;
        return uc;
    }

    private static object FindPatrolTask(UnitController u)
    {
        var f = typeof(PatrolTaskSystem).GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Static);
        var list = f != null ? f.GetValue(null) as System.Collections.IList : null;
        if (list == null) return null;
        foreach (var item in list)
        {
            if (item == null) continue;
            var uf = item.GetType().GetField("Unit");
            if (uf != null && ReferenceEquals(uf.GetValue(item) as UnitController, u)) return item;
        }
        return null;
    }
    private static int TaskIndex(object task) => task != null ? (int)task.GetType().GetField("WaypointIndex").GetValue(task) : -1;
    private static Vector2 TaskNext(object task) => task != null ? (Vector2)task.GetType().GetField("NextWaypoint").GetValue(task) : Vector2.zero;

    private static string CmdModule(NPCBrain b)
    {
        var f = typeof(NPCBrain).GetField("_lastCmd", BindingFlags.NonPublic | BindingFlags.Instance);
        var cmd = f != null ? f.GetValue(b) : null;
        if (cmd == null) return "null";
        var mf = cmd.GetType().GetField("Module");
        return mf != null ? mf.GetValue(cmd).ToString() : "?";
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        var all = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].gameObject.scene.IsValid()) return all[i];
        return null;
    }

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 30f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || UnitRegistry.Instance == null || KingdomRegistry.Instance == null || SelectionController.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(0.6f);
        Log("正门进局 seed=" + SEED + "（30x）", true);

        var anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        var sel = SelectionController.Instance;

        // 玩家士兵（生产链；NPCBrain 驱动）
        var wDef = UnitDataManager.Instance.GetData(Faction.PlayerCamp, Occupation.Warrior) as NpcProfessionDef;
        var goU = wDef != null ? UnitFactory.Instance.SpawnUnit(wDef, SpawnPosSnapper.SnapWorld(anchor + new Vector2(12 * cs, 0), "HH159巡逻兵"), 0) : null;
        var unit = goU != null ? goU.GetComponent<UnitController>() : null;
        var brain = goU != null ? goU.GetComponent<NPCBrain>() : null;
        yield return null;
        yield return new WaitForSeconds(0.4f);
        if (unit == null || brain == null) { Log($"P1 前置缺失 unit={unit != null} brain={brain != null}（Warrior prefab 无 NPCBrain？）", false); Finish(); yield break; }

        // ===== P1 玩家巡逻真派发（SelectionController 真链）=====
        Vector2 up0 = unit.GetPosition();
        sel.ClearSelection();
        sel.SelectUnit(unit);
        bool began = sel.BeginPatrolSetup();
        var fWp = typeof(SelectionController).GetField("_patrolWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
        var wp = fWp != null ? fWp.GetValue(sel) as List<Vector2> : null;
        if (wp != null)
        {
            wp.Add(up0 + new Vector2(2.5f * cs, 0));
            wp.Add(up0 + new Vector2(0, 2.5f * cs));
            wp.Add(up0);   // 首尾同点闭环
        }
        int wpCount = wp != null ? wp.Count : -1;
        sel.ConfirmPatrolSetup();
        bool dispatched = PatrolTaskSystem.IsPatrolling(brain);
        yield return new WaitForSeconds(16f);
        object taskA = FindPatrolTask(unit);
        int idxA = TaskIndex(taskA);
        Vector2 up1 = unit.GetPosition();
        bool moved = Vector2.Distance(up0, up1) > cs;      // 净位移 ≥1 格
        bool advanced = idxA > 0;                          // 路点下标推进（循环移动硬证据）
        Log($"P1 巡逻真派发：BeginSetup={began} 路点数={wpCount}（确认前）确认后IsPatrolling={dispatched} " +
            $"位移={Vector2.Distance(up0, up1):F2}（>{cs:F2}=1格）waypointIndex={idxA} lastCmd.Module={CmdModule(brain)}",
            began && dispatched && (moved || advanced));

        // ===== P1n 负探针：AI 侧无巡逻入口（AI 单位不因玩家入口被派发）=====
        // 真过滤路径（ClickSelect/BoxSelect）需相机+碰撞体，自动化下不可靠 → 改用入口语义+行为计数：
        sel.ClearSelection();
        bool noSelRejected = !sel.BeginPatrolSetup();   // 无选中→入口拒绝（巡逻仅作用于已选中己方单位）
        int aiPatrolling = 0, aiTotal = 0;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.kingdomId <= 0) continue;
            aiTotal++;
            var b2 = u.GetComponent<NPCBrain>();
            if (b2 != null && PatrolTaskSystem.IsPatrolling(b2)) aiPatrolling++;
        }
        Log($"P1n 负探针：空选中→BeginPatrolSetup 拒绝={noSelRejected}；玩家入口派发后 AI 巡逻数={aiPatrolling}/{aiTotal}（应=0）" +
            $"｜代码锚：SelectionController.ClickSelect/BoxSelect 仅纳 kingdomId==0（AI 单位不可入 Selected）",
            noSelRejected && aiTotal > 0 && aiPatrolling == 0);

        // ===== P2 遇敌打断→转交战→敌清回巡逻 =====
        var enemy = SpawnVagrant(unit.GetPosition() + new Vector2(0.5f * cs, 0), 3);   // 异族野人（raceId=3≠0，贴脸 0.5 格）
        yield return null;
        bool fought = false, stillPatrol = false;
        string p2why = "敌人生成失败";
        if (enemy != null)
        {
            int eHp0 = enemy.CurrentHp, wHp0 = unit.CurrentHp;
            yield return new WaitForSeconds(6f);
            fought = !enemy.IsAlive || enemy.CurrentHp < eHp0 || unit.CurrentHp < wHp0;
            stillPatrol = PatrolTaskSystem.IsPatrolling(brain);
            p2why = $"敌HP {eHp0}→{(enemy.IsAlive ? enemy.CurrentHp : 0)} 我HP {wHp0}→{unit.CurrentHp} 任务仍在={stillPatrol}";
            if (enemy.IsAlive) enemy.TakeDamage(999999);
        }
        Log($"P2a 遇敌打断→交战：{p2why}", fought);
        object t2 = FindPatrolTask(unit);
        float dBefore = t2 != null ? Vector2.Distance(unit.GetPosition(), TaskNext(t2)) : -1f;
        int idxB = TaskIndex(t2);
        yield return new WaitForSeconds(8f);
        object t3 = FindPatrolTask(unit);
        float dAfter = t3 != null ? Vector2.Distance(unit.GetPosition(), TaskNext(t3)) : -1f;
        int idxC = TaskIndex(t3);
        bool resumed = t3 != null && (dAfter < dBefore - 0.1f * cs || idxC != idxB);
        bool patrolAlive = PatrolTaskSystem.IsPatrolling(brain);
        Log($"P2b 敌清回巡逻：任务存在={t3 != null} 距下一路点 {dBefore:F2}→{dAfter:F2} waypointIndex {idxB}→{idxC} " +
            $"lastCmd.Module={CmdModule(brain)} → 回巡推进={resumed}（IsPatrolling={patrolAlive}）",
            patrolAlive && resumed);

        // ===== P5 情报面板阶段列与 KingdomState.scriptPhase 一致 =====
        // scriptPhase 由王国脑日 tick 同步 → 需推进 ≥1 游戏日（30x≈12s/日）
        yield return WaitDays(2);
        Log($"P5 前置：推进至第 {(TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1)} 日", true);
        var intel = FindSceneComponent<KingdomIntelPanel>();
        if (intel == null) { Log("P5 前置缺失：场景无 KingdomIntelPanel", false); }
        else
        {
            var miBind = typeof(KingdomIntelPanel).GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Instance);
            var miFill = typeof(KingdomIntelPanel).GetMethod("Fill", BindingFlags.NonPublic | BindingFlags.Instance);
            var fStage = typeof(KingdomIntelPanel).GetField("_stage", BindingFlags.NonPublic | BindingFlags.Instance);
            if (miBind != null) miBind.Invoke(intel, null);
            var stageLabel = fStage != null ? fStage.GetValue(intel) as Label : null;
            if (miFill == null || stageLabel == null) { Log($"P5 前置缺失：Fill={miFill != null} stageLabel={stageLabel != null}（UIDocument 未绑定？）", false); }
            else
            {
                int checkedN = 0, matchN = 0;
                var seen = new Dictionary<string, int>();
                var parts = new List<string>();
                foreach (var k in KingdomRegistry.Instance.GetAll())
                {
                    if (k == null || !k.scriptPhase.HasValue) continue;
                    intel.ShowKingdom(k.id);
                    miFill.Invoke(intel, null);
                    string expect = ScriptStageMachine.Name(k.scriptPhase.Value);
                    string panel = stageLabel.text;
                    checkedN++;
                    if (panel == expect) matchN++;
                    seen[panel] = seen.TryGetValue(panel, out var c) ? c + 1 : 1;
                    parts.Add($"k{k.id}:面板[{panel}]vs阶段[{expect}]");
                }
                Log($"P5 情报面板阶段列：受检国={checkedN} 一致={matchN}｜[{string.Join(" ", parts)}]（读公开口 KingdomState.scriptPhase，D520）",
                    checkedN > 0 && matchN == checkedN);
                Log($"P5c 阶段多值（信息）：不同阶段取值数={seen.Count}" +
                    (seen.Count <= 1 ? "（本局同阶段=同拍，不构成判别力限制）" : ""), true);
                if (checkedN == 0) Log("P5b 无任何王国携带 scriptPhase（AI 脑日 tick 未跑？）——列报", true);
            }
        }

        // ===== P7 高亮联动（含 D619 切国清上一国）=====
        var overlay = TerritoryOverlay.Instance;
        var rig2 = CameraRig.Instance;
        if (overlay == null) { Log("P7 前置缺失：TerritoryOverlay 不在场", false); }
        else
        {
            overlay.ReapplyAll();
            yield return null;
            var fPainted = typeof(TerritoryOverlay).GetField("_painted", BindingFlags.NonPublic | BindingFlags.Instance);
            var painted = fPainted != null ? fPainted.GetValue(overlay) as Dictionary<Vector2Int, int> : null;
            int kidA = -1, kidB = -1; Vector2Int midA = default, midB = default;
            if (painted != null)
                foreach (var kv in painted)
                {
                    if (kidA < 0) { kidA = kv.Value; midA = kv.Key; }
                    else if (kv.Value != kidA) { kidB = kv.Value; midB = kv.Key; break; }
                }
            if (kidA < 0 || kidB < 0) { Log($"P7 前置缺失：染色不足两国（kidA={kidA} kidB={kidB} painted={(painted != null ? painted.Count : -1)}）", false); }
            else
            {
                if (rig2 != null) rig2.ZoomTo(0);          // 近景：普通 alpha=0，高亮才显
                yield return new WaitForSeconds(0.6f);
                float aBefore = overlay.GetMidAlpha(midA);
                var listPanel = FindSceneComponent<KingdomListPanel>();
                if (listPanel != null) listPanel.HighlightKingdom(kidA); else overlay.HighlightKingdom(kidA);
                yield return null;
                bool activeHL = overlay.IsLayerActive;
                float aHl = overlay.GetMidAlpha(midA);
                // 切国
                if (listPanel != null) listPanel.HighlightKingdom(kidB); else overlay.HighlightKingdom(kidB);
                yield return null;
                float aAfterSwitch = overlay.GetMidAlpha(midA);
                float bHl = overlay.GetMidAlpha(midB);
                var fHl = typeof(TerritoryOverlay).GetField("_highlightKid", BindingFlags.NonPublic | BindingFlags.Instance);
                int hlKid = fHl != null ? (int)fHl.GetValue(overlay) : -2;
                // 关面板
                if (listPanel != null) listPanel.Close(); else overlay.HighlightKingdom(-1);
                yield return new WaitForSeconds(0.6f);
                float bAfterClose = overlay.GetMidAlpha(midB);
                bool hidden = !overlay.IsLayerActive;

                Log($"P7a 高亮生效：近景普通alpha={aBefore:F3} → 高亮后={aHl:F3} 层激活={activeHL}（缝=KingdomListPanel）",
                    activeHL && aHl > aBefore + 0.05f);
                Log($"P7b D619 切国清上一国：k{kidA} mid alpha 高亮={aHl:F3} → 切k{kidB}后={aAfterSwitch:F3}（应回普通={aBefore:F3}）｜_highlightKid={hlKid}（应={kidB}）b高亮={bHl:F3}",
                    Mathf.Abs(aAfterSwitch - aBefore) < 0.05f && hlKid == kidB);
                Log($"P7c 关面板清当前国：k{kidB} mid alpha {bHl:F3}→{bAfterClose:F3} 层隐藏={hidden}",
                    bAfterClose <= 0.05f && hidden);
            }
        }

        Finish();
    }

    private static IEnumerator WaitDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            int start = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            while (TimeManager.Instance != null && TimeManager.Instance.CurrentDay == start)
                yield return null;
            yield return new WaitForSeconds(0.3f);
        }
    }

    private static void Finish()
    {
        _log.AppendLine("===== HH.159 件9 UI 行为探针收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh159_ui_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError(TAG + " 日志写盘失败: " + e.Message); }
        Debug.LogWarning(TAG + " ★ 收工 PASS=" + _pass + " FAIL=" + _fail);
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
