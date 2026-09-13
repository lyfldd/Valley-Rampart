using System.Collections.Generic;
using UnityEngine;
using ValleyRampart.Rendering;

/// <summary>
/// 轻量序列帧动画播放器（HH.239 T13 · 路线A · H5 全配档）。
///
/// 状态源 = 运动/攻击事件（idle / walk / attack / run）；帧源 = <see cref="SpriteRefTable"/>
/// （<c>unit_{race}_{occ}_{state}</c> 切帧后的 Sprite[]；机器走 <c>machine_{race}_{type}_strip</c>）。
/// **缺图回退静默化**：整套缺帧 ⇒ 保持 prefab 原 sprite 不动（负查询缓存 + 一次性告警，禁逐帧刷日志）。
///
/// 架构（H5）：
///   - **单管理器集中推进**（<see cref="SpriteAnimatorDriver"/>，ProjectileManager 同款）；
///   - **结构体数组内联**（Driver 内平铺 <c>AnimSlot</c>：timer/frame/fps/state/frames，连续内存遍历）；
///   - **脏写跳过**（帧索引未变不写 sprite 属性）；
///   - **不可见注销**（OnBecameInvisible 移出活跃表／OnBecameVisible 回表）；
///   - **状态注册表**（per (race,occ) 共享 Sprite[]，400 单位共享 ~46 套 sheet，帧数组只建一份）；
///   - **O(1) 注册/注销**（swap-remove，禁 List.Remove）；OnDestroy 必注销（池复位纪律延伸）；
///   - **零 GC／零逐帧字符串比较**（状态用 int id；帧集解析只在 race/occ 变化时做一次）；
///   - **Time.deltaTime** 驱动（自动随倍速/timeScale 冻结）；per-state fps + 局部 speedScale。
/// </summary>
[DisallowMultipleComponent]
public class SpriteAnimator : MonoBehaviour
{
    public const int StIdle = 0, StWalk = 1, StAttack = 2, StRun = 3;
    public const int StateCount = 4;

    static readonly string[] StateNames = { "idle", "walk", "attack", "run" };
    // 每状态：fps（SO 可配口径，暂常量）/ 播放模式（0=loop 1=once 回落 idle 2=once 定格）
    static readonly float[] StateFps = { 8f, 12f, 12f, 12f };
    static readonly int[] StateMode = { 0, 0, 1, 0 };   // 工人 attack=工作循环语义由 UnitController 侧选择不改模式

    /// <summary>共享帧注册表：key = race*1000 + (int)occ → 4 个状态的帧数组（帧数组只建一份）。</summary>
    static readonly Dictionary<long, Sprite[][]> _setCache = new Dictionary<long, Sprite[][]>();
    /// <summary>负查询缓存（缺图静默化：不重复查、不逐帧刷日志）。</summary>
    static readonly HashSet<long> _missingSets = new HashSet<long>();
    /// <summary>缺图一次性告警登记（防刷屏）。</summary>
    static readonly HashSet<long> _warned = new HashSet<long>();

    /// <summary>Driver 内联推进槽（结构体数组，连续内存；Driver 侧平铺持有）。</summary>
    internal struct AnimSlot
    {
        public SpriteRenderer sr;
        public Sprite[] frames;
        public float timer;
        public int frame;
        public int lastFrame;     // 脏写跳过
        public int state;
        public bool facingLeft;
    }

    SpriteRenderer _sr;
    UnitController _uc;
    Sprite[][] _set;              // null = 无真图（保持 prefab 原 sprite 不动）
    int _raceId = int.MinValue;
    Occupation _occ;
    long _setKey = long.MinValue;
    int _desiredMoveState = StIdle;
    float _speedScale = 1f;
    float _moveHold;
    float _attackHold;
    bool _facingLeft;
    bool _registered;

    internal AnimSlot Slot;
    internal Sprite[][] FrameSet => _set;
    internal bool FacingLeft => _facingLeft;
    internal float SpeedScale => _speedScale;

    /// <summary>本帧期望状态（意图层）：attack 优先 → 移动 → idle。播放层（Driver）只消费本值。</summary>
    internal int DesiredState
    {
        get
        {
            if (_attackHold > 0f) return StAttack;
            if (_moveHold > 0f) return _desiredMoveState;
            return StIdle;
        }
    }

    /// <summary>意图计时衰减（由 Driver 集中调用；零 GC）。</summary>
    internal void DecayIntent(float dt)
    {
        if (_moveHold > 0f) _moveHold -= dt;
        if (_attackHold > 0f) _attackHold -= dt;
    }

    /// <summary>强制回落 idle（once 态播完）。</summary>
    internal void ForceIdle()
    {
        _attackHold = 0f;
        _moveHold = 0f;
        _desiredMoveState = StIdle;
    }

    internal static float FpsOf(int state) => (state >= 0 && state < StateCount) ? StateFps[state] : 12f;

    internal static int ModeOf(int state) => (state >= 0 && state < StateCount) ? StateMode[state] : 0;

    /// <summary>随机起始相位（仅 loop 态进场随机帧，防全场齐步走）。</summary>
    internal static int RandomPhase(int state, int len)
        => (len > 1 && ModeOf(state) == 0) ? Random.Range(0, len) : 0;

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
        if (isActiveAndEnabled) Register();   // 常驻登记：帧集未就绪时由 Driver 每 tick 复核（防「出池洗涤后不再解析」死锁）
    }

    void OnDisable() => Unregister();
    void OnDestroy() => Unregister();
    void OnBecameInvisible() => Unregister();
    void OnBecameVisible() { if (isActiveAndEnabled) Register(); }

    /// <summary>由 Driver 调用：重新解析帧集（race/occ 变化时才做，禁用逐帧解析）。</summary>
    internal void EnsureSet()
    {
        int race = _uc != null ? _uc.raceId : RaceIds.Human;
        var occ = _uc != null ? _uc.EffectiveOccupation : Occupation.Civilian;
        if (_set != null && race == _raceId && occ == _occ) return;
        _raceId = race;
        _occ = occ;
        _setKey = (long)race * 1000 + (int)occ;
        _set = ResolveSet(_setKey, race, occ);
        if (_set != null)
        {
            if (Slot.frames == null || Slot.frames.Length == 0) Slot.state = -1;  // 强制重置到 idle 相位
        }
    }

    /// <summary>移动通知（UnitController.UpdateFacing 调用）：驱动 walk/run + 两向 flipX。</summary>
    public void NotifyMove(Vector2 direction)
    {
        _moveHold = 0.15f;
        int st = Mathf.Abs(direction.x) > 0.01f || Mathf.Abs(direction.y) > 0.01f ? StWalk : StIdle;
        if (_uc != null && _uc.IsCharging) st = StRun;   // 坐骑 run（冲锋语义）；无冲锋的族无 run 帧 → 回落 walk
        if (st != StIdle) _desiredMoveState = st;
        if (direction.x < -0.01f) _facingLeft = true;
        else if (direction.x > 0.01f) _facingLeft = false;
    }

    /// <summary>攻击通知（UnitController 侧攻击注册成功时调用）：播放 attack（once 回落 idle）。</summary>
    public void NotifyAttack()
    {
        if (_set == null) { EnsureSet(); if (_set == null) return; }
        if (_set[StAttack] == null || _set[StAttack].Length == 0) return;
        _attackHold = 0.4f;
    }

    /// <summary>LOD / 倍速局部缩放（对接既有 LODSystem；1=全速，0.5=远档降半频，0=冻结静态帧）。</summary>
    public void SetSpeedScale(float scale)
    {
        _speedScale = Mathf.Max(0f, scale);
    }

    /// <summary>就地重置（对象池复用 / 出池洗涤）。</summary>
    public void ResetForReuse()
    {
        _desiredMoveState = StIdle;
        _moveHold = 0f;
        _attackHold = 0f;
        _facingLeft = false;
        _raceId = int.MinValue;
        _occ = default;
        _setKey = long.MinValue;
        _set = null;
        Slot.state = -1;
        Slot.frame = 0;
        Slot.lastFrame = -1;
        Slot.timer = 0f;
        if (isActiveAndEnabled && !_registered) Register();   // 保持登记 ⇒ Driver 下一 tick 重新解析（防死锁）
    }

    // ===== 注册表（O(1) swap-remove）=====

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

    // ===== 帧集解析（状态注册表）=====

    /// <summary>per (race,occ) 解析 4 状态帧数组；无真图返回 null（负查询缓存 + 一次性告警）。</summary>
    Sprite[][] ResolveSet(long key, int race, Occupation occ)
    {
        if (_setCache.TryGetValue(key, out var cached)) return cached;
        if (_missingSets.Contains(key)) return null;

        var table = SpriteRefTable.Instance;
        if (table == null) { _missingSets.Add(key); return null; }

        string raceName = BuildingVisual.RaceName(race);
        string token = ArtToken(occ);
        var set = new Sprite[StateCount][];
        bool any = false;

        // 机器线（T15）：machine_{race}_{type} 单图 / machine_{race}_{type}_strip 16 帧
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
            if (!any) { _missingSets.Add(key); return null; }
            _setCache[key] = set;
            return set;
        }

        if (token == null) { _missingSets.Add(key); return null; }

        // 敌怪（2_14）：素材路径 Units/monster/{type}/，键 = unit_monster_{type}_{state}（无族段）
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
            if (table.TryGetFrames(frameKey, out var fr) && fr.Length > 0)
            {
                set[i] = fr;
                any = true;
            }
        }
        // 状态缺失回落：run→walk→idle；attack→idle（H5「once 回落」的静默兜底）
        if (set[StRun] == null) set[StRun] = set[StWalk];
        if (set[StAttack] == null) set[StAttack] = set[StIdle];

        if (!any)
        {
            _missingSets.Add(key);
            if (_warned.Add(key))
                Debug.Log($"[SpriteAnimator] 无真图帧集 (race={raceName}, occ={occ}) ⇒ 保持 prefab 原 sprite（缺图回退；同类只告警一次）");
            return null;
        }
        _setCache[key] = set;
        return set;
    }

    static bool IsMachine(Occupation occ, out string type)
    {
        switch (occ)
        {
            case Occupation.Ballista:    type = "ballista";     return true;
            case Occupation.Mortar:      type = "mortar";       return true;
            case Occupation.VineCatapult:type = "vinecatapult"; return true;
            case Occupation.Ram:         type = "ram";          return true;
            default:                     type = null;           return false;
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
            // 无独立素材者就近复用（缺图面·§七 维持回退口径）
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
