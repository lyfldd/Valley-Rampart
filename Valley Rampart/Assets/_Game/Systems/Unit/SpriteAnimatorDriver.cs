using System.Collections.Generic;
using UnityEngine;
using ValleyRampart.Rendering;

/// <summary>
/// 序列帧动画**集中推进器**（HH.239 T13 ＋ HH.264 A 段 · 规格 §2.1「SpriteAnimatorSystem」· ProjectileManager 同款范式）。
///
/// 数据布局：与 <see cref="_objs"/> 平行的**结构体数组** <see cref="_slots"/>（<c>SpriteAnimator.AnimSlot</c> 平铺，
/// 连续内存遍历、零虚调用、零装箱）；**脏写跳过**（帧索引未变不写 sprite 属性）。
/// 注册/注销：O(1) swap-remove（禁 List.Remove 位移）；不可见/销毁由 <see cref="SpriteAnimator"/> 侧驱动移出。
/// 推进：单 <c>LateUpdate</c>，<c>Time.deltaTime × speedScale × lodFpsScale</c>（自动随倍速 0.5~3x 与 timeScale 冻结）。
/// **LOD 分层（F-13）**：per-slot 时间闸（`SpriteAnimatorConfig.lodRefreshInterval`，默认 0.5s）查 **既有**
/// <c>LODSystem.GetLevelAt</c> ⇒ 近＝全速／半活跃＝半频／休眠＝冻结静态帧；**禁每帧逐单位查**（M1）。
/// </summary>
public class SpriteAnimatorDriver : MonoBehaviour
{
    static SpriteAnimatorDriver _instance;

    /// <summary>单例（首次取用时自建隐藏宿主；跨场景常驻）。</summary>
    public static SpriteAnimatorDriver Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[SpriteAnimatorDriver]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SpriteAnimatorDriver>();
            }
            return _instance;
        }
    }

    readonly List<SpriteAnimator> _objs = new List<SpriteAnimator>(512);
    readonly List<SpriteAnimator.AnimSlot> _slots = new List<SpriteAnimator.AnimSlot>(512);

    public int ActiveCount => _objs.Count;

    /// <summary>诊断/验收读数（P8）：当前活跃表中各 LOD 档位数（0=Active 1=SemiActive 2=Dormant）。</summary>
    public int CountTier(int tier)
    {
        int n = 0;
        for (int i = 0; i < _slots.Count; i++) if (_slots[i].lodTier == tier) n++;
        return n;
    }

    /// <summary>诊断/验收读数：当前活跃表内处于指定状态的数量。</summary>
    public int CountState(int state)
    {
        int n = 0;
        for (int i = 0; i < _slots.Count; i++) if (_slots[i].state == state) n++;
        return n;
    }

    /// <summary>O(1) 登记（重复登记幂等）。</summary>
    public void Add(SpriteAnimator a)
    {
        if (a == null) return;
        for (int i = 0; i < _objs.Count; i++)
            if (ReferenceEquals(_objs[i], a)) return;   // 仅重登记时走 O(n)（每次注册一次，非每帧）
        _objs.Add(a);
        _slots.Add(a.Slot);
    }

    /// <summary>O(1) 注销（swap-remove）。</summary>
    public void Remove(SpriteAnimator a)
    {
        if (a == null) return;
        for (int i = 0; i < _objs.Count; i++)
        {
            if (!ReferenceEquals(_objs[i], a)) continue;
            SwapRemove(i);
            return;
        }
    }

    void SwapRemove(int i)
    {
        int last = _objs.Count - 1;
        _objs[i] = _objs[last];
        _slots[i] = _slots[last];
        _objs.RemoveAt(last);
        _slots.RemoveAt(last);
    }

    void LateUpdate()
    {
        int count = _objs.Count;
        if (count == 0) return;
        float dt = Time.deltaTime;
        var cfg = SpriteAnimatorConfig.Instance;
        var lod = LODSystem.Instance;   // 既有 LOD 系统（禁另建；F-13）

        // 倒序遍历：SwapRemove 把末位搬到 i，倒序可保证未访问项不被跳过
        for (int i = count - 1; i >= 0; i--)
        {
            var a = _objs[i];
            if (a == null) { SwapRemove(i); continue; }

            a.EnsureSet();
            var set = a.FrameSet;
            if (set == null) { a.DecayIntent(dt); continue; }   // 缺图：保持 prefab 原 sprite（仍留表 ⇒ 出池换族后可再解析）

            var s = _slots[i];
            if (s.sr == null) { SwapRemove(i); continue; }

            a.DecayIntent(dt);

            int want = a.DesiredState;
            int mode = a.DesiredMode;
            bool restarted = s.token != a.RestartToken;

            if (s.state != want || s.mode != mode || restarted || s.frames == null)
            {
                s.state = want;
                s.mode = mode;
                s.token = a.RestartToken;
                s.timer = 0f;
                s.lastFrame = -1;
                s.completed = false;
                var fr = set[want];
                if (fr == null || fr.Length == 0)
                    fr = (mode == SpriteAnimator.ModeLoop) ? set[SpriteAnimator.StIdle] : null;   // once 无帧 ⇒ null（走立即完成）
                s.frames = fr;
                s.frame = (fr != null) ? SpriteAnimator.RandomPhase(mode, fr.Length) : 0;
                // 相位与 timer 对齐（否则本 tick 的推进会把随机相位覆写成 0 ⇒ F-08 失效）
                s.timer = (fr != null && s.frame > 0) ? s.frame / Mathf.Max(1f, cfg.FpsOf(want)) : 0f;
            }

            // once 态无帧（例：death 无素材）⇒ 立即完成回调（既有流程零延迟），不动 sprite
            if (s.frames == null || s.frames.Length == 0)
            {
                if (!s.completed)
                {
                    s.completed = true;
                    a.OnOnceFinished(mode);
                }
                _slots[i] = s;
                a.Slot = s;
                continue;
            }

            // ---- LOD 档位（F-13）：per-slot 时间闸，禁每帧逐单位查 Dictionary（M1）----
            s.lodTimer -= dt;
            if (s.lodTimer <= 0f)
            {
                s.lodTimer = cfg.lodRefreshInterval;
                s.lodTier = (lod != null) ? (int)lod.GetLevelAt(a.transform.position) : 0;
            }

            float scale = a.SpeedScale * cfg.LodScaleOf(s.lodTier);
            int len = s.frames.Length;

            if (scale <= 0f)
            {
                // 冻结档（Dormant）：停推进，仅保证当前帧在位（静态帧）
            }
            else
            {
                float fps = cfg.FpsOf(want);
                if (want == SpriteAnimator.StAttack && mode == SpriteAnimator.ModeOnceReturn) fps *= cfg.attackSpeedScale;
                // 非坐骑 run＝walk 加速（D568：run 帧回落 walk 时按 runAsWalkSpeedScale 加速）
                if (want == SpriteAnimator.StRun && set[SpriteAnimator.StWalk] != null
                    && ReferenceEquals(s.frames, set[SpriteAnimator.StWalk]))
                    fps *= cfg.runAsWalkSpeedScale;
                if (fps <= 0f) fps = 1f;

                s.timer += dt * scale;
                int f = (int)(s.timer * fps);
                if (f >= len)
                {
                    if (mode == SpriteAnimator.ModeLoop)
                    {
                        s.timer -= len / fps;            // 相位连续（防 fps 抖动）
                        if (s.timer < 0f) s.timer = 0f;
                        f = (int)(s.timer * fps);
                        if (f >= len) f = len - 1;
                    }
                    else
                    {
                        f = len - 1;                     // OnceReturn / OnceHold 停末帧
                        if (!s.completed)
                        {
                            s.completed = true;
                            s.frame = f;
                            if (s.frame != s.lastFrame)
                            {
                                s.sr.sprite = s.frames[s.frame];
                                s.lastFrame = s.frame;
                            }
                            a.OnOnceFinished(mode);           // F-04 回调（once 播完）
                            if (s.facingLeft != a.FacingLeft) s.facingLeft = s.sr.flipX = a.FacingLeft;
                            _slots[i] = s;
                            a.Slot = s;
                            continue;                        // 回调可能改变意图，下一 tick 再切态
                        }
                    }
                }
                s.frame = f;
            }

            // 脏写跳过：帧索引未变不写 sprite 属性（F-16：同 sheet 同 pivot ⇒ 换帧 bounds 不变 ⇒ 排序稳定）
            if (s.frame != s.lastFrame)
            {
                s.sr.sprite = s.frames[s.frame];
                s.lastFrame = s.frame;
                var fcb = a.FrameCallback;                  // F-15 帧钩子（只预留·不接游戏逻辑）
                if (fcb != null) fcb(a.Controller, s.frame);
            }
            if (s.facingLeft != a.FacingLeft) s.facingLeft = s.sr.flipX = a.FacingLeft;   // F-07 两向翻转

            _slots[i] = s;
            a.Slot = s;
        }
    }
}
