#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.264 B 段：`SpriteAnimator` **§7.2 行为级探针 P1~P8**（Editor-only · 正门 `TestHarnessApi.EnterTestRun`）。
///
/// 口径：每条探针给**读数**（帧索引/状态/回调计数/告警计数/LOD 档位），禁"已实现"充数。
/// 用法（MCP）：Play 模式内 `Valley_HH264_AnimProbe.Run()`；读 `[HH264]` 日志得证据；探针自收尾（ExitTestRun+QuitSmoke+退 Play）。
/// </summary>
public static class Valley_HH264_AnimProbe
{
    const int Seed = 21107;
    static readonly StringBuilder _log = new StringBuilder();
    static int _pass, _fail;
    static int _warnCount;      // [SpriteAnimator] 告警条数（P7 禁刷屏实证）
    static int _cbLoot, _cbDeath;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH264] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0; _warnCount = 0; _cbLoot = 0; _cbDeath = 0;
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 1x）", true);
        var host = new GameObject("HH264_AnimProbeHost").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine(host));
    }

    class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (cond.Contains("[SpriteAnimator]")) _warnCount++;
            else if (t == LogType.Exception) Debug.LogError("[HH264][PROBE-EXC] " + cond + "\n" + stack);
        }
    }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.Log("[HH264] " + (ok ? "✓ " : "✗ ") + msg);
        Flush();   // 逐条落盘（域重载/退出 Play 会清控制台，证据以文件为准）
    }

    /// <summary>证据落盘（Logs/hh264_anim_probe.log；控制台可能被域重载清空）。</summary>
    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh264_anim_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { /* 取证失败不影响探针 */ }
    }

    static string _logPath;

    static SpriteAnimator Anim(UnitController u) => u.GetComponent<SpriteAnimator>();
    static int FrameOf(UnitController u) => Anim(u).CurrentFrameIndex;
    static int StateOf(UnitController u) => Anim(u).CurrentStateId;
    static bool FlipOf(UnitController u) => u.GetComponent<SpriteRenderer>().flipX;

    static IEnumerator Coroutine(ProbeHost host)
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "smoke_hh264", kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);   // 1x：动画推进按真实时序

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(0.8f);

        // ---- 探针单位（玩家阵营·主城旁·禁脑防 AI 移动干扰）----
        Vector2 basePos = PlayerCastlePos();
        var warriors = new List<UnitController>();
        for (int i = 0; i < 3; i++)
        {
            var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Warrior, basePos + new Vector2(0.35f * i, 0f), 0);
            if (go != null) warriors.Add(go.GetComponent<UnitController>());
        }
        var wgo = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, basePos + new Vector2(1.4f, 0f), 0);
        UnitController worker = wgo != null ? wgo.GetComponent<UnitController>() : null;
        var all = new List<UnitController>(warriors);
        if (worker != null) all.Add(worker);
        foreach (var u in all)
        {
            var b = u.GetComponent<NPCBrain>();
            if (b != null) b.enabled = false;                 // 探针期间不受 AI 移动驱动
            Anim(u).ResetForReuse();                           // 清态后再解析（等同出池）
            Anim(u).EnsureSet();
        }
        // F-13 前置：把**既有 LODSystem** 焦点按到探针区（否则探针区落 Dormant ⇒ 动画冻结，P1~P6 不可判）
        if (LODSystem.Instance != null) LODSystem.Instance.SetFocalCenter(basePos);
        yield return new WaitForSeconds(1.5f);
        Log($"P0 前置：探针单位在场 warriors={warriors.Count} worker={(worker != null ? 1 : 0)} "
            + $"| 帧集={Anim(warriors[0]).HasRealFrames} 帧数(idle)={Anim(warriors[0]).CurrentFrameCount} "
            + $"| LOD档位(探针区)={Anim(warriors[0]).LodTier}(0=Active) "
            + $"| 档位分布 Active={SpriteAnimatorDriver.Instance.CountTier(0)} Semi={SpriteAnimatorDriver.Instance.CountTier(1)} Dormant={SpriteAnimatorDriver.Instance.CountTier(2)}",
            warriors.Count >= 3 && worker != null && Anim(warriors[0]).LodTier == 0);
        if (warriors.Count < 3 || worker == null) { Finish(host); yield break; }

        var w1 = warriors[0]; var w2 = warriors[1]; var w3 = warriors[2];
        var a1 = Anim(w1);

        // ================= P1 idle 循环：8 帧回绕 ＋ 随机相位 =================
        {
            int f0a = FrameOf(w1), f0b = FrameOf(w2), f0c = FrameOf(w3);
            int distinctPhases = (f0a != f0b ? 1 : 0) + (f0b != f0c ? 1 : 0) + (f0a != f0c ? 1 : 0);
            int maxSeen = -1, minSeen = 999, advances = 0, last = -1;
            float t = 0f;
            while (t < 1.6f)
            {
                int f = FrameOf(w1);
                if (f > maxSeen) maxSeen = f;
                if (f < minSeen) minSeen = f;
                if (last >= 0 && f != last) advances++;
                last = f; t += Time.deltaTime; yield return null;
            }
            int len = a1.CurrentFrameCount;
            bool ok = len == 8 && maxSeen == 7 && minSeen == 0 && advances >= 5 && distinctPhases >= 1;
            Log($"P1 idle 循环：有效帧数={len}（切9播8） 观察窗最大帧={maxSeen}（≤7＝末格空不播） 最小帧={minSeen} "
                + $"回绕推进次数={advances}/1.6s 起始相位 w1={f0a} w2={f0b} w3={f0c}（3 单位相位对差值={distinctPhases}≥1 ⇒ 随机相位生效） state={StateOf(w1)}", ok);
        }

        // ================= P2 walk ＋ flipX（无抖动/错帧）=================
        {
            int flipsRight = 0, framesSeen = 0, stateDuringRight = -1; bool prev = FlipOf(w1); int lastF = -1;
            float t = 0f;
            while (t < 0.6f)
            {
                a1.NotifyMove(Vector2.right * 1.0f);            // 持续右移
                bool f = FlipOf(w1);
                if (f != prev) flipsRight++;
                prev = f;
                int fr = FrameOf(w1);
                if (fr != lastF) { framesSeen++; lastF = fr; }
                stateDuringRight = a1.CurrentStateId;
                t += Time.deltaTime; yield return null;
            }
            bool rightOk = flipsRight == 0 && FlipOf(w1) == false && framesSeen >= 3 && stateDuringRight == SpriteAnimator.StWalk;

            int flipsLeft = 0; prev = FlipOf(w1); t = 0f;
            while (t < 0.6f)
            {
                a1.NotifyMove(Vector2.left * 1.0f);             // 持续左移
                bool f = FlipOf(w1);
                if (f != prev) flipsLeft++;
                prev = f;
                t += Time.deltaTime; yield return null;
            }
            bool leftOk = flipsLeft == 1 && FlipOf(w1) == true;
            Log($"P2 walk+flipX：右移 反向翻转次数={flipsRight}（0＝无抖动） 换帧次数={framesSeen} 期间state={stateDuringRight}(1=walk) "
                + $"左移 反向翻转次数={flipsLeft}（1＝只翻一次） 现flipX={FlipOf(w1)}（True＝左向）", rightOk && leftOk);
        }

        // ================= P3 战斗 attack 单次 → 回落 =================
        {
            a1.NotifyMove(Vector2.zero);                        // 先回落 idle
            yield return new WaitForSeconds(0.35f);
            int before = StateOf(w1);
            a1.NotifyAttack();
            yield return null;
            int during = StateOf(w1);
            float t = 0f; int maxF = -1;
            while (t < 2.0f) { if (StateOf(w1) == SpriteAnimator.StAttack) { int f = FrameOf(w1); if (f > maxF) maxF = f; } t += Time.deltaTime; yield return null; }
            int after = StateOf(w1);
            int atkLen = 0;
            Log($"P3 战斗 attack 单次：前置state={before}(0=idle) 触发后state={during}(2=attack) 观察窗最大攻击帧={maxF} "
                + $"播完回落state={after}(0=idle／1=walk) ⇒ {(after == SpriteAnimator.StIdle ? "OnceReturn 回落成立" : "未回落")}",
                before == SpriteAnimator.StIdle && during == SpriteAnimator.StAttack && after == SpriteAnimator.StIdle);
        }

        // ================= P4 工人工作 attack 循环（不回落）=================
        {
            var wk = Anim(worker);
            wk.NotifyMove(Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            int stayAttack = 0, samples = 0; float t = 0f;
            while (t < 1.5f)
            {
                worker.NotifyWorkVisual();                      // 模拟 TaskScheduler Working 期每 tick 调用
                if (wk.CurrentStateId == SpriteAnimator.StAttack) stayAttack++;
                samples++;
                t += Time.deltaTime; yield return null;
            }
            bool loopOk = samples > 0 && stayAttack >= samples - 2 && wk.CurrentMode == SpriteAnimator.ModeLoop;
            // 停止工作通知 → 应回落
            float t2 = 0f;
            while (t2 < 0.6f) { t2 += Time.deltaTime; yield return null; }
            bool fellBack = wk.CurrentStateId == SpriteAnimator.StIdle;
            Log($"P4 工人工作循环：Working 期 state==attack 占比={stayAttack}/{samples} mode={wk.CurrentMode}(0=Loop) "
                + $"| 停发工作通知 0.6s 后 state={wk.CurrentStateId}（回落）", loopOk && fellBack);
        }

        // ================= P5 OnceHold 定格 ＋ 完成回调（F-04）=================
        {
            var wc = Anim(w3);
            _cbLoot = 0;
            wc.SubscribeComplete(() => { _cbLoot++; });
            wc.NotifyLoot();                                    // loot＝OnceHold（人无 loot 素材 ⇒ 走 fallback→Idle 帧）
            float t = 0f;
            while (t < 2.0f) { t += Time.deltaTime; yield return null; }
            int holdFrame = wc.CurrentFrameIndex, holdLen = wc.CurrentFrameCount;
            int holdState = wc.CurrentStateId;
            yield return new WaitForSeconds(0.5f);
            bool heldStill = wc.CurrentFrameIndex == holdFrame && wc.CurrentFrameIndex == holdLen - 1;
            Log($"P5a OnceHold 定格＋回调：回调触发次数={_cbLoot} 末帧={holdFrame}/{holdLen - 1} 0.5s 后仍={wc.CurrentFrameIndex}"
                + $"（定格不动） state={holdState}(4=loot)", _cbLoot == 1 && heldStill);

            // P5b death 路径（本批无 death 素材 ⇒ 即时回调，不延后既有回收流程）
            _cbDeath = 0;
            var wd = Anim(w1);
            wd.SubscribeComplete(() => { _cbDeath++; });
            string spriteBefore = w1.GetComponent<SpriteRenderer>().sprite != null ? w1.GetComponent<SpriteRenderer>().sprite.name : "null";
            wd.NotifyDeath();
            yield return new WaitForSeconds(0.3f);
            string spriteAfter = w1.GetComponent<SpriteRenderer>().sprite != null ? w1.GetComponent<SpriteRenderer>().sprite.name : "null";
            Log($"P5b death 路径：death 真帧在场={wd.HasFramesFor(SpriteAnimator.StDeath)}（素材缺） "
                + $"onOnceComplete 触发次数={_cbDeath}（即时＝既有回收零延迟） sprite 前={spriteBefore} 后={spriteAfter}（保持不崩）",
                _cbDeath == 1 && spriteBefore == spriteAfter);
        }

        // ================= P6 打断重播（帧回 0）=================
        {
            a1 = Anim(w1);
            a1.SetState(SpriteAnimator.StIdle, true);
            yield return new WaitForSeconds(0.3f);
            a1.NotifyAttack();
            float t = 0f; int midFrame = -1;
            while (t < 0.35f) { midFrame = FrameOf(w1); t += Time.deltaTime; yield return null; }
            int tokenBefore = a1.RestartToken;
            a1.NotifyAttack();                                   // 攻击中再触发 ⇒ 打断重播
            int tokenAfter = a1.RestartToken;
            yield return null;
            int fAfter = FrameOf(w1);
            Log($"P6 打断重播：触发前帧={midFrame} 重触发后帧={fAfter}（0/1＝回到起点） token={tokenBefore}→{tokenAfter}（+1＝F-05 令牌生效）",
                tokenAfter == tokenBefore + 1 && fAfter <= 1);
        }

        // ================= P7 缺图回退（不崩 ＋ 一次性告警禁刷屏）=================
        {
            int warnBefore = _warnCount;
            var u = w2;
            var anim = Anim(u);
            var sr = u.GetComponent<SpriteRenderer>();
            string spriteBefore = sr.sprite != null ? sr.sprite.name : "null";
            u.SetOccupation(Occupation.SiegeMachine);            // 该 occupation 无对应 artId（token=null）⇒ 整套缺图
            yield return null;
            anim = Anim(u);                                      // SetOccupation 后同组件
            anim.EnsureSet();
            anim.NotifyMove(Vector2.right);
            float t = 0f;
            while (t < 1.0f) { t += Time.deltaTime; yield return null; }   // 连续 60 帧观察告警条数
            int warnDelta = _warnCount - warnBefore;
            string spriteAfter = sr.sprite != null ? sr.sprite.name : "null";
            Log($"P7 缺图回退：HasRealFrames={anim.HasRealFrames}（false＝回退末端） 连续 1.0s 告警条数={warnDelta}（1＝禁刷屏） "
                + $"sprite 前={spriteBefore} 后={spriteAfter}（不崩·保持原图）", warnDelta == 1 && !anim.HasRealFrames);
            u.SetOccupation(Occupation.Warrior);                  // 复位
            yield return null;
        }

        // ================= P8 LOD 分层 ＋ 不可见移出活跃表 =================
        {
            var drv = SpriteAnimatorDriver.Instance;
            var cfgA = ValleyRampart.Rendering.SpriteAnimatorConfig.Instance;

            // 8a LOD 分层：取"**已登记且档位不同**"的两单位做同窗对照（Active vs Dormant/Semi）
            UnitController near = w1, far = null;
            foreach (var cu in UnityEngine.Object.FindObjectsOfType<UnitController>())
            {
                var ca = Anim(cu);
                if (ca == null || !ca.IsRegistered) continue;
                if (ca.LodTier >= 1) { far = cu; break; }
            }
            near.transform.position = basePos;                  // 近档＝相机旁
            yield return new WaitForSeconds(1.2f);
            int nearTier = Anim(near).LodTier;
            yield return SampleAdvance(near, 1.0f);
            int nearAdv = _adv;
            int farTier = -1, farAdv = -1;
            if (far != null)
            {
                farTier = Anim(far).LodTier;
                yield return SampleAdvance(far, 1.0f);
                farAdv = _adv;
            }

            // 8b 不可见注销（F-12）：先试位移触发 Unity 剔除，再驱动回调直证接线
            var w2a = Anim(w2);
            bool regBefore = w2a.IsRegistered;
            w2.transform.position = basePos + new Vector2(3000f, 3000f);
            yield return new WaitForSeconds(0.8f);
            bool regAfterMove = w2a.IsRegistered;                    // 多相机场景下位移未必触发剔除（如实记录）
            var mInv = typeof(SpriteAnimator).GetMethod("OnBecameInvisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mVis = typeof(SpriteAnimator).GetMethod("OnBecameVisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mInv.Invoke(w2a, null);
            bool regInvisible = w2a.IsRegistered;                    // 期望 false（移出活跃表）
            mVis.Invoke(w2a, null);
            bool regVisible = w2a.IsRegistered;                      // 期望 true（回表）
            int camCount = UnityEngine.Camera.allCamerasCount;

            float nearScale = cfgA.LodScaleOf(nearTier), farScale = cfgA.LodScaleOf(farTier < 0 ? 2 : farTier);
            Log($"P8 LOD：档位对照 近 tier={nearTier}(scale={nearScale}) 帧推进/1s={nearAdv} "
                + $"vs 远 tier={farTier}(scale={farScale}) 帧推进/1s={farAdv}（远 < 近 ⇒ 降半频/冻结生效） "
                + $"| 档位分布 Active={drv.CountTier(0)} Semi={drv.CountTier(1)} Dormant={drv.CountTier(2)} "
                + $"| 不可见注销：位移后 IsRegistered={regAfterMove}（相机数={camCount}·多相机下剔除不必然）"
                + $" 回调直证 {regBefore}→OnBecameInvisible∈活跃表={regInvisible}→OnBecameVisible∈活跃表={regVisible}",
                far != null && farScale < nearScale && farAdv < nearAdv
                && regBefore && !regInvisible && regVisible);
        }

        Log($"==== HH264 P1~P8 汇总：PASS={_pass} FAIL={_fail} ====", _fail == 0);
        Debug.Log("[HH264][SUMMARY]\n" + _log);
        Finish(host);
    }

    /// <summary>1 秒窗内帧推进次数采样（结果入 <see cref="_adv"/>；迭代器禁 out 参数）。</summary>
    static int _adv;
    static IEnumerator SampleAdvance(UnitController u, float seconds)
    {
        _adv = 0;
        int last = FrameOf(u);
        float t = 0f;
        while (t < seconds)
        {
            int f = FrameOf(u);
            if (f != last) { _adv++; last = f; }
            t += Time.deltaTime; yield return null;
        }
    }

    static Vector2 PlayerCastlePos()
    {
        var reg = BuildingRegistry.Instance;
        if (reg != null)
            foreach (var b in reg.All)
                if (b != null && b.sourceType == BuildingType.CastleCore && b.kingdomId == 0)
                    return (Vector2)b.transform.position + new Vector2(2f, 0f);
        return Vector2.zero;
    }

    static void Finish(ProbeHost host)
    {
        _log.AppendLine($"==== HH264 收尾：PASS={_pass} FAIL={_fail} ====");
        Flush();
        Debug.Log("[HH264] 探针收尾：ExitTestRun + QuitSmoke（含清场/退 Play）");
        Application.logMessageReceived -= host.Catch;
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }
}
#endif
