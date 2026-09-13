using System.Collections.Generic;
using UnityEngine;
using ValleyRampart.Rendering;

/// <summary>
/// 轻量序列帧动画播放器（HH.239 T13 ＋ HH.264 A 段补完 · 规格《美术资源接入_SpriteAnimator播放器设计规格》）。
///
/// 状态源 = UnitController/NPCBrain/TaskScheduler（唯一）；帧源 = <see cref="SpriteRefTable"/>。
/// **缺图回退链（F-09）**：状态缺 → `stateFallback` 回退态 → 静态立绘（`portrait_{race}_{occ}`）→ 占位（prefab 原图）。
/// **O(1) 生死／不可见注销／脏写跳过／零 GC／零逐帧字符串比较**（见 Driver）。
///
/// 本类＝**意图层**（状态/朝向/变速/回调订阅）；播放层＝<see cref="SpriteAnimatorDriver"/>（单管理器集中推进）。
/// 硬编码值全部迁入 <see cref="SpriteAnimatorConfig"/>（so-data-driven）。
/// </summary>
[DisallowMultipleComponent]
public class SpriteAnimator : MonoBehaviour
{
    // ===== AnimState（规格 §三；int 枚举，禁逐帧字符串比较；扩展＝尾插）=====
    public const int StIdle = 0, StWalk = 1, StAttack = 2, StRun = 3, StLoot = 4, StDeath = 5;
    public const int StateCount = 6;
    // ===== PlayMode（规格 §三）=====
    public const int ModeLoop = 0, ModeOnceReturn = 1, ModeOnceHold = 2;

    static readonly string[] StateNames = { "idle", "walk", "attack", "run", "loot", "death" };

    /// <summary>共享帧注册表：key = race*1000 + (int)occ → 6 状态帧数组（帧数组只建一份，F-01）。</summary>
    static readonly Dictionary<long, Sprite[][]> _setCache = new Dictionary<long, Sprite[][]>();
    /// <summary>负查询缓存（F-09 静默化：不重复查、不逐帧刷日志）。</summary>
    static readonly HashSet<long> _missingSets = new HashSet<long>();
    /// <summary>缺图一次性告警登记（防刷屏，M2）。</summary>
    static readonly HashSet<long> _warned = new HashSet<long>();

    /// <summary>Driver 侧平铺推进槽（结构体数组内联·连续内存）。</summary>
    internal struct AnimSlot
    {
        public SpriteRenderer sr;
        public Sprite[] frames;
        public float timer;
        public int frame;         // 当前帧索引
        public int lastFrame;     // 脏写跳过
        public int state;
        public int mode;          // 本帧生效播放模式（含工人工作循环覆写）
        public int token;         // 打断重播令牌（F-05）
        public bool facingLeft;
        public bool completed;    // once 已触发回调（防重复）
        public int lodTier;       // 0=Active 1=SemiActive 2=Dormant
        public float lodTimer;
    }

    SpriteRenderer _sr;
    UnitController _uc;
    Sprite[][] _set;
    int _raceId = int.MinValue;
    Occupation _occ;
    long _setKey = long.MinValue;

    int _desiredMoveState = StIdle;
    int _forcedState = -1;        // Death/Loot 等显式态（优先级最高）
    bool _attackIntend;           // 战斗攻击意图：**sticky 直到 once 播完**（F-02/F-05：CD 1s < 动画 1.33s，禁被 hold 截断）
    float _speedScale = 1f;       // 局部变速（F-06 ②）
    float _moveHold;
    float _workHold;              // 工人 Working 期（F-02 工作循环）
    int _restartToken;
    bool _facingLeft;
    bool _registered;

    System.Action _onOnceComplete;                       // F-04 完成回调（P0）
    System.Action<UnitController, int> _onFrame;         // F-15 帧事件钩子（只预留·不接逻辑）

    internal AnimSlot Slot;
    // ===== 验收/诊断读数面（HH.264 §7.2 探针 P1~P8 取证；只读）=====
    /// <summary>当前状态 id（AnimState int）。</summary>
    public int CurrentStateId => Slot.state;
    /// <summary>当前帧索引。</summary>
    public int CurrentFrameIndex => Slot.frame;
    /// <summary>当前状态有效帧数（0＝无帧）。</summary>
    public int CurrentFrameCount => (Slot.frames != null) ? Slot.frames.Length : 0;
    /// <summary>是否已解析到真图帧集（false＝回退末端＝保持 prefab 原图）。</summary>
    public bool HasRealFrames => _set != null;
    /// <summary>当前 LOD 档位（0=Active 1=SemiActive 2=Dormant）。</summary>
    public int LodTier => Slot.lodTier;
    /// <summary>是否在 Driver 活跃表中（F-12 不可见注销可观测面）。</summary>
    public bool IsRegistered => _registered;
    /// <summary>当前生效播放模式（0=Loop 1=OnceReturn 2=OnceHold）。</summary>
    public int CurrentMode => Slot.mode;
    internal Sprite[][] FrameSet => _set;
    internal bool FacingLeft => _facingLeft;
    internal float SpeedScale => _speedScale;
    /// <summary>打断重播令牌（F-05 可观测面：每次重触发 +1）。</summary>
    public int RestartToken => _restartToken;
    internal System.Action OnceCallback => _onOnceComplete;
    internal System.Action<UnitController, int> FrameCallback => _onFrame;
    internal UnitController Controller => _uc;

    /// <summary>本帧期望状态（意图层）：显式态 &gt; 工作循环 &gt; 战斗攻击 &gt; 移动 &gt; idle。</summary>
    internal int DesiredState
    {
        get
        {
            if (_forcedState >= 0) return _forcedState;
            if (_workHold > 0f) return StAttack;
            if (_attackIntend) return StAttack;
            if (_moveHold > 0f) return _desiredMoveState;
            return StIdle;
        }
    }

    /// <summary>本帧期望播放模式（F-02 三态分离：工人工作 attack＝Loop；战斗 attack＝OnceReturn）。</summary>
    internal int DesiredMode
    {
        get
        {
            var cfg = SpriteAnimatorConfig.Instance;
            int st = DesiredState;
            if (st == StAttack && _workHold > 0f && cfg.workAttackLoop) return ModeLoop;
            return cfg.ModeOf(st);
        }
    }

    internal void DecayIntent(float dt)
    {
        if (_moveHold > 0f) _moveHold -= dt;
        if (_workHold > 0f) _workHold -= dt;
    }

    /// <summary>随机起始相位（F-08：**仅 Loop 态**进场随机帧，防 400 单位齐步走；one-shot 不随机）。</summary>
    internal static int RandomPhase(int mode, int len)
        => (len > 1 && mode == ModeLoop) ? UnityEngine.Random.Range(0, len) : 0;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _uc = GetComponent<UnitController>();
        Slot.sr = _sr;
        Slot.state = -1;
        Slot.lastFrame = -1;
    }

    void OnEnable()
    {
        EnsureSet();
        if (isActiveAndEnabled) Register();   // 常驻登记：帧集未就绪由 Driver 每 tick 复核（防「出池洗涤后不再解析」死锁）
    }

    void OnDisable() => Unregister();
    void OnDestroy() => Unregister();
    /// <summary>F-12 不可见注销（H5）。</summary>
    void OnBecameInvisible() => Unregister();
    void OnBecameVisible()
    {
        if (isActiveAndEnabled) Register();
        Slot.lodTimer = 0f;   // 回表立即刷新 LOD 档（防"用旧档"；M1）
    }

    // ===================== 对外 API（规格 §三）=====================

    /// <summary>SetState（F-05：同状态重入＝打断重播，one-shot 从第 0 帧重开）。</summary>
    public void SetState(int state, bool restart = false)
    {
        if (state < 0 || state >= StateCount) return;
        if (state == StDeath || state == StLoot) _forcedState = state;
        else _forcedState = -1;
        if (state == StAttack) _attackIntend = true;
        if (restart) _restartToken++;
    }

    /// <summary>SetFlip：只画朝右，左向＝flipX（F-07）。</summary>
    public void SetFlip(bool facingLeft) => _facingLeft = facingLeft;

    /// <summary>SetSpeed：局部变速（F-06 ②；LOD 缩放在 Driver 侧另乘）。</summary>
    public void SetSpeedScale(float scale) => _speedScale = Mathf.Max(0f, scale);

    /// <summary>SubscribeComplete（F-04 P0）：OnceReturn/OnceHold 播完触发；death 播完 ⇒ 衔接既有死亡流程。F-14 池复位清空。</summary>
    public void SubscribeComplete(System.Action callback) => _onOnceComplete = callback;

    /// <summary>OnFrame 帧事件钩子（F-15 **只预留**·本批不接游戏逻辑·表现禁驱动逻辑）。</summary>
    public void SubscribeFrame(System.Action<UnitController, int> callback) => _onFrame = callback;

    /// <summary>移动通知（UnitController.UpdateFacing 调用）：驱动 walk/run ＋ 两向 flipX。</summary>
    public void NotifyMove(Vector2 direction)
    {
        _moveHold = 0.15f;
        int st = Mathf.Abs(direction.x) > 0.01f || Mathf.Abs(direction.y) > 0.01f ? StWalk : StIdle;
        if (_uc != null && _uc.IsCharging) st = StRun;   // 坐骑 run（D602）；非坐骑无 run 帧 ⇒ 回落 walk 并加速
        if (st != StIdle) _desiredMoveState = st;
        if (direction.x < -0.01f) _facingLeft = true;
        else if (direction.x > 0.01f) _facingLeft = false;
    }

    /// <summary>战斗攻击通知（DamageSystem.ExecuteAttack → NotifyAttackVisual）：attack＝OnceReturn（播完回落）。</summary>
    public void NotifyAttack()
    {
        if (_set == null) { EnsureSet(); if (_set == null) return; }
        if (_forcedState >= 0) return;                  // 死亡/拾取演出中不打断
        if (_set[StAttack] == null || _set[StAttack].Length == 0) return;
        _attackIntend = true;                            // sticky：由 once 播完（OnOnceFinished）收口
        _restartToken++;                                 // F-05 打断重播
    }

    /// <summary>工人工作通知（TaskScheduler Working 期每 tick 调用）：attack＝Loop（不回落，F-02）。</summary>
    public void NotifyWork()
    {
        _workHold = 0.3f;
        _desiredMoveState = StIdle;
    }

    /// <summary>死亡通知（UnitController.Die 调用）：death＝OnceHold（定格末帧），播完回调衔接既有死亡流程（F-04）。</summary>
    public void NotifyDeath()
    {
        _forcedState = StDeath;
        _moveHold = 0f;
        _attackIntend = false;
        _workHold = 0f;
        _restartToken++;
        EnsureSet();
    }

    /// <summary>拾取演出（HH.233 monster loot；触发方归 2_14 域，未接线则不会调用）。</summary>
    public void NotifyLoot()
    {
        if (_set == null) EnsureSet();
        _forcedState = StLoot;
        _restartToken++;
    }

    /// <summary>该状态是否有真图帧（供 F-04：死亡演出在场性判定 —— 无 death 帧则不延后既有回收流程）。</summary>
    public bool HasFramesFor(int state)
    {
        if (_set == null) EnsureSet();
        return _set != null && state >= 0 && state < StateCount
               && _set[state] != null && _set[state].Length > 0;
    }

    /// <summary>once 播完内部收口：按**播放模式**收口（OnceReturn 清态回落／OnceHold 定格保留），并触发订阅者回调（F-04）。</summary>
    internal void OnOnceFinished(int mode)
    {
        var cb = _onOnceComplete;
        _onOnceComplete = null;              // F-04：一次性（防重复触发）
        if (mode != ModeOnceHold)
        {
            _forcedState = -1;
            _attackIntend = false;
        }
        _workHold = 0f;
        cb?.Invoke();
    }

    /// <summary>F-14 池复位：清回调 ＋ frame=随机 ＋ state=Idle（对齐规格 F-14 原文）。</summary>
    public void ResetForReuse()
    {
        _desiredMoveState = StIdle;
        _forcedState = -1;
        _moveHold = 0f;
        _attackIntend = false;
        _workHold = 0f;
        _facingLeft = false;
        _restartToken++;
        _onOnceComplete = null;              // F-14 回调查空
        _raceId = int.MinValue;
        _occ = default;
        _setKey = long.MinValue;
        _set = null;
        Slot.state = -1;
        Slot.frame = 0;                      // 下一帧进入 idle 时由 Driver 赋随机相位（F-08/F-14）
        Slot.lastFrame = -1;
        Slot.timer = 0f;
        Slot.token = -1;
        Slot.completed = false;
        Slot.lodTier = 0;
        Slot.lodTimer = 0f;
        if (isActiveAndEnabled && !_registered) Register();
    }

    // ===================== 注册（O(1) swap-remove）=====================

    void Register()
    {
        if (_registered) return;
        _registered = true;
        SpriteAnimatorDriver.Instance.Add(this);
    }

    void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        SpriteAnimatorDriver.Instance.Remove(this);
    }

    // ===================== 帧集解析（F-01 注册表 ＋ F-09 回退链）=====================

    /// <summary>解析帧集（race/occ 变化时才重建；池复位后/探针显式调用）。</summary>
    public void EnsureSet()
    {
        int race = _uc != null ? _uc.raceId : RaceIds.Human;
        var occ = _uc != null ? _uc.EffectiveOccupation : Occupation.Civilian;
        if (_set != null && race == _raceId && occ == _occ) return;
        _raceId = race;
        _occ = occ;
        _setKey = (long)race * 1000 + (int)occ;
        _set = ResolveSet(_setKey, race, occ);
        if (_set != null && Slot.frames == null) Slot.state = -1;   // 强制重置到首个状态相位
    }

    /// <summary>per (race,occ) 解析 6 状态帧数组；三级回退链末端仍无 ⇒ null（保持 prefab 原图＝占位）。</summary>
    Sprite[][] ResolveSet(long key, int race, Occupation occ)
    {
        if (_setCache.TryGetValue(key, out var cached)) return cached;
        if (_missingSets.Contains(key)) return null;

        var table = SpriteRefTable.Instance;
        if (table == null) { _missingSets.Add(key); return null; }

        string raceName = BuildingVisual.RaceName(race);
        var cfg = SpriteAnimatorConfig.Instance;
        var set = new Sprite[StateCount][];
        bool any = false;

        // ---- 机器线（T15）：machine_{race}_{type} 单图 / machine_{race}_{type}_strip 16 帧 ----
        if (IsMachine(occ, out string machineType))
        {
            string mkey = $"machine_{raceName}_{machineType}";
            if (table.TryGetFrames($"{mkey}_strip", out var strip) && strip.Length > 0)
            {
                for (int i = 0; i < StateCount; i++) { set[i] = strip; any = true; }
            }
            else if (table.TryGet(mkey, out var single) && single != null)
            {
                set[StIdle] = new[] { single };
                any = true;
            }
            if (!any) { WarnMissing(key, race, occ); return null; }
            _setCache[key] = set;
            return set;
        }

        string token = ArtToken(occ);
        if (token == null) { WarnMissing(key, race, occ); return null; }

        // ---- ① 逐状态取帧（怪物键不含族段：unit_monster_{type}_{state}）----
        bool monster = occ == Occupation.Monster;
        string monsterType = null;
        if (monster)
        {
            var mc = _uc as MonsterController;
            monsterType = mc != null ? mc.Type.ToString().ToLowerInvariant() : "raider";
        }
        for (int i = 0; i < StateCount; i++)
        {
            string frameKey = monster
                ? $"unit_{token}_{monsterType}_{StateNames[i]}"
                : $"unit_{raceName}_{token}_{StateNames[i]}";
            if (table.TryGetFrames(frameKey, out var fr) && fr.Length > 0) { set[i] = fr; any = true; }
        }

        // ---- ② 状态缺 ⇒ stateFallback 回退态（F-09 第二级；最多两跳防环）----
        if (any)
        {
            for (int i = 0; i < StateCount; i++)
            {
                if (set[i] != null) continue;
                int fb = cfg.FallbackOf(i);
                for (int hop = 0; hop < 2 && fb >= 0 && set[fb] == null; hop++) fb = cfg.FallbackOf(fb);
                if (fb >= 0 && fb < StateCount && set[fb] != null) set[i] = set[fb];
            }
            _setCache[key] = set;
            return set;
        }

        // ---- ③ 整套缺 ⇒ 静态立绘（F-09 第三级：portrait_{race}_{occ}）----
        string portraitKey = monster ? $"portrait_monster_{monsterType}" : $"portrait_{raceName}_{token}";
        if (table.TryGet(portraitKey, out var portrait) && portrait != null)
        {
            var one = new[] { portrait };
            for (int i = 0; i < StateCount; i++) set[i] = one;
            _setCache[key] = set;
            return set;
        }

        // ---- ④ 占位（F-09 末端：保持 prefab 原 sprite；负查询缓存＋一次性告警）----
        WarnMissing(key, race, occ);
        return null;
    }

    /// <summary>缺图一次性告警（负查询缓存 + 同类只告警一次·禁逐帧刷屏；M2）。</summary>
    static void WarnMissing(long key, int race, Occupation occ)
    {
        _missingSets.Add(key);
        if (_warned.Add(key))
            Debug.Log($"[SpriteAnimator] 整套无真图 (race={BuildingVisual.RaceName(race)}, occ={occ})"
                      + " ⇒ 保持 prefab 原 sprite（缺图回退链末端；同类只告警一次）");
    }

    static bool IsMachine(Occupation occ, out string type)
    {
        switch (occ)
        {
            case Occupation.Ballista:     type = "ballista";     return true;
            case Occupation.Mortar:       type = "mortar";       return true;
            case Occupation.VineCatapult: type = "vinecatapult"; return true;
            case Occupation.Ram:          type = "ram";          return true;
            default:                      type = null;           return false;
        }
    }

    /// <summary>Occupation → 美术 token（素材实盘名；无对应素材返回 null ⇒ 回退）。</summary>
    static string ArtToken(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Archer:       return "archer";
            case Occupation.Warrior:      return "warrior";
            case Occupation.Mage:         return "mage";
            case Occupation.Healer:       return "healer";
            case Occupation.Crossbowman:  return "crossbowman";
            case Occupation.ShieldGuard:  return "shieldguard";
            case Occupation.General:      return "general";
            case Occupation.Cavalry:      return "knight";       // 骑兵素材名=knight（人+马一体烘焙）
            case Occupation.Resident:     return "resident";
            case Occupation.Worker:       return "worker";
            case Occupation.Vagrant:      return "vagrant";
            case Occupation.Child:        return "child";
            // 无独立素材者就近复用（缺图面）
            case Occupation.Civilian:     return "worker";
            case Occupation.Porter:       return "worker";
            case Occupation.HeavyWarrior: return "warrior";
            case Occupation.Bishop:       return "healer";
            case Occupation.Archmage:     return "mage";
            case Occupation.Ruler:        return "general";
            // 四族专属兵（2_20 M7）
            case Occupation.Berserker:    return "berserker";
            case Occupation.WolfRider:    return "wolfrider";
            case Occupation.Musqueteer:   return "musqueteer";
            case Occupation.Bedrock:      return "bedrock";
            case Occupation.Ranger:       return "ranger";
            case Occupation.Windwalker:   return "windwalker";
            case Occupation.DeerRider:    return "deerrider";
            // 敌怪（2_14）：素材在 Units/monster/{type}/
            case Occupation.Monster:      return "monster";
            default:                      return null;
        }
    }
}
