using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 序列帧动画**集中推进器**（HH.239 T13 / H5「单管理器集中推进」，ProjectileManager 同款范式）。
///
/// 数据布局：与 <see cref="_objs"/> 平行的**结构体数组** <see cref="_slots"/>（<c>SpriteAnimator.AnimSlot</c> 平铺，
/// 连续内存遍历、零虚调用、零装箱）；**脏写跳过**（帧索引未变不写 sprite 属性）。
/// 注册/注销：O(1) swap-remove（禁 List.Remove 位移）；不可见/销毁由 SpriteAnimator 侧驱动移出。
/// 推进：单 <c>LateUpdate</c>，<c>Time.deltaTime × speedScale</c>（自动随倍速 0.5~3x 与 timeScale 冻结）。
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

        // 倒序遍历：SwapRemove 把末位搬到 i，倒序可保证未访问项不被跳过
        for (int i = count - 1; i >= 0; i--)
        {
            var a = _objs[i];
            if (a == null) { SwapRemove(i); continue; }

            a.EnsureSet();
            var set = a.FrameSet;
            if (set == null) continue;   // 缺图：保持 prefab 原 sprite（仍留在活跃表 ⇒ 出池换族后可再解析）

            a.DecayIntent(dt);
            var s = _slots[i];
            if (s.sr == null) { SwapRemove(i); continue; }

            int want = a.DesiredState;
            var frames = set[want] != null && set[want].Length > 0 ? set[want] : set[SpriteAnimator.StIdle];
            if (frames == null || frames.Length == 0) { SwapRemove(i); continue; }

            // 状态切换（同状态重入 = 打断重播：由 state 比较保证；loop 态随机起始相位防齐步走）
            if (s.state != want || !ReferenceEquals(s.frames, frames))
            {
                s.state = want;
                s.frames = frames;
                s.timer = 0f;
                s.frame = SpriteAnimator.RandomPhase(want, frames.Length);
                s.lastFrame = -1;
            }

            float fps = SpriteAnimator.FpsOf(want);
            float scale = a.SpeedScale;
            if (scale <= 0f)
            {
                s.timer = s.frame / Mathf.Max(1f, fps);   // 冻结静态帧（远档 LOD）
            }
            else
            {
                s.timer += dt * scale;
                int f = (int)(s.timer * fps);
                if (f >= frames.Length)
                {
                    if (SpriteAnimator.ModeOf(want) == 0)   // loop
                    {
                        f %= frames.Length;
                        s.timer -= frames.Length / Mathf.Max(1f, fps);   // 保持相位连续，防 fps 抖动
                        if (s.timer < 0f) s.timer = 0f;
                    }
                    else if (SpriteAnimator.ModeOf(want) == 1)   // once → 回落 idle
                    {
                        a.ForceIdle();
                        s.state = -1;      // 下帧切回 idle
                        f = frames.Length - 1;
                    }
                    else
                    {
                        f = frames.Length - 1;   // once 定格
                    }
                }
                s.frame = f;
            }

            // 脏写跳过：帧索引未变不写 sprite 属性
            if (s.frame != s.lastFrame)
            {
                s.sr.sprite = s.frames[s.frame];
                s.lastFrame = s.frame;
            }
            // 两向朝向（flipX）
            if (s.facingLeft != a.FacingLeft) s.facingLeft = s.sr.flipX = a.FacingLeft;

            _slots[i] = s;
            a.Slot = s;
        }
    }
}
