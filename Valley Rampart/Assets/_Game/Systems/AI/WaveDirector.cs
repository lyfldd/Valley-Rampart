using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌袭 → 偶发传送门灾害调度（2_8 实施计划 步骤7 / 2_14 步骤8/10 / D261 / D97 / D177~D189）。
/// 2_14 单轨收拢后，本类只负责【生成传送门 + 按 6:3:1 波次构成出怪】：
///   - 判定权归 PortalDisasterTrigger（唯一概率源，R4 确定性达标，发布 PortalDisasterTriggeredEvent）
///   - 本类订阅该事件做事件桥接，D261 定案：波次并入本类内部方法 SpawnPortalDisasterWaves()，本篇不新建调度类
///   - 正常夜晚无波次（步骤10）：判定不走到此即无任何出怪。
/// 确定性纪律（R4）：出怪随机源用 System.Random(seed)（worldSeed 派生），禁用 UnityEngine.Random，供 sim 对拍。
/// </summary>
public class WaveDirector : Singleton<WaveDirector>, ISaveableSpawner
{
    /// <summary>ISaveableSpawner：负责重建运行时生成的传送门（前缀 "Portal_"）。</summary>
    public string SaveIdPrefix => "Portal_";
    private WaveConfig _config;
    private PortalDisasterConfig _disasterConfig;
    private System.Random _rng = new System.Random();

    /// <summary>当前活跃传送门（无则不生成怪物，由触发序保证先建门再波次）。</summary>
    public Portal ActivePortal { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        // Data/WaveConfig.asset 不存在时回退到类内默认值（字段初始值即 §三 占位表）。
        _config = Resources.Load<WaveConfig>("Data/WaveConfig") ?? ScriptableObject.CreateInstance<WaveConfig>();
        _disasterConfig = Resources.Load<PortalDisasterConfig>("Config/Disaster/PortalDisasterConfig");
        EventBus.Subscribe<PortalDisasterTriggeredEvent>(OnPortalDisasterTriggered);
        EventBus.Subscribe<TimePhaseChangedEvent>(OnTimePhaseChanged);
        SaveManager.Instance?.RegisterSpawner(this);
    }

    public WaveConfig Config => _config;

    protected override void OnDestroy()
    {
        EventBus.Unsubscribe<PortalDisasterTriggeredEvent>(OnPortalDisasterTriggered);
        EventBus.Unsubscribe<TimePhaseChangedEvent>(OnTimePhaseChanged);
        base.OnDestroy();
    }

    /// <summary>世界种子派生确定性随机源（R4）。worldSeed 变化 → 同晚出怪节奏随档位复现；缺 WorldManager 回退固定种子。</summary>
    private void SeedRandom()
    {
        int seed = WorldManager.Instance != null ? WorldManager.Instance.MapSeed : 1337;
        _rng = new System.Random(seed);
    }

    // ========================================================================
    //  事件桥接（判定权归 Trigger）→ 生成传送门 → 波次
    // ========================================================================

    private void OnPortalDisasterTriggered(PortalDisasterTriggeredEvent evt)
    {
        if (_config == null || !gameObject.activeInHierarchy) return;
        SeedRandom();
        StartCoroutine(SpawnNewDisaster(evt.Day, evt.PortalWorldPos));
    }

    /// <summary>
    /// 步骤9/11：昼夜驱动。入夜（Night）时，若仍有存活传送门（未摧毁）→ 每夜续战（烈度递减，§4.4）。
    /// 正常夜无波次的例外：门在场 = 持续灾害，不依赖再次触发灾害。
    /// </summary>
    private void OnTimePhaseChanged(TimePhaseChangedEvent evt)
    {
        if (evt.NewPhase != TimePhase.Night) return;          // 只在入夜触发
        if (ActivePortal == null || ActivePortal.state == PortalState.Destroying) return;
        if (_config == null || !gameObject.activeInHierarchy) return;
        SeedRandom();
        int day = TimeManager.Instance != null ? Mathf.Max(1, TimeManager.Instance.CurrentDay) : 1;

        // 存活夜 +1 → 强度 ×aftermathDecayRate 递减
        ActivePortal.OnSurviveNight();
        Debug.Log($"[WaveDirector] 传送门存活夜#{ActivePortal.survivedNights}，续战（烈度递减），第{day}天。");
        StartCoroutine(SpawnPortalWaves(ActivePortal, day));
    }

    /// <summary>首夜灾害（8/10 主链，D261）：判定已由 Trigger 完成，此处新建传送门 + 出首波。</summary>
    private IEnumerator SpawnNewDisaster(int day, Vector2 portalWorldVec)
    {
        Vector2 portalPos = portalWorldVec;
        // 事件未携带合法坐标（Trigger 占位发 Vector2.zero）→ 由本类按规则选放置位
        if (portalPos == Vector2.zero)
            portalPos = PickPortalPlacement();

        Portal portal = SpawnPortalEntity(portalPos);
        if (portal == null)
        {
            Debug.LogWarning("[WaveDirector] 传送门放置失败，本夜灾害流止（占位：脚手架场景可能缺网格）。");
            yield break;
        }
        ActivePortal = portal;
        Debug.Log($"[WaveDirector] 灾害发生，新建传送门@{portalPos}，第{day}天。");
        yield return SpawnPortalWaves(portal, day);
    }

    /// <summary>对一个传送门出 6:3:1 波次（首波或续战共用；强度随门存活夜递减）。</summary>
    private IEnumerator SpawnPortalWaves(Portal portal, int day)
    {
        if (portal == null) yield break;

        // 波次构成：baseWaves + 难度×wavePerDifficulty → Easy3/Normal4/Hard5（D97）
        int difficulty = DifficultyManager.Instance != null
            ? Mathf.Max(1, DifficultyManager.Instance.CurrentDifficulty) : 1;
        int waveCount = _config.baseWaves + difficulty * _config.wavePerDifficulty;

        float winterMult = TimeManager.Instance != null
            ? TimeManager.Instance.MonsterStrengthMultiplierForCurrentSeason : 1f;

        Debug.Log($"[WaveDirector] 传送门@{portal.name} 出 {waveCount} 波（存活夜#{portal.survivedNights}）。");
        for (int w = 0; w < waveCount; w++)
        {
            int strength = ComputeWaveStrength(winterMult, portal);
            Debug.Log($"[WaveDirector] 第{w + 1}/{waveCount}波来袭: 强度={strength} 第{day}天");
            yield return SpawnWavePortion(w, strength);
            yield return new WaitForSeconds(NextIntervalSeconds());
        }
    }

    /// <summary>单波构成：按 6:3:1 配比拆 strength 分配 Raider/Slinger/Brute，组内逐只错峰出生。</summary>
    private IEnumerator SpawnWavePortion(int waveIndex, int strength)
    {
        if (ActivePortal == null) yield break;

        Vector3 ratio = _config.waveCompositionRatio;
        float sum = Mathf.Max(0.001f, ratio.x + ratio.y + ratio.z);

        // 近战/远程/精英 数量 = strength × 占比（四舍五入，精英保底 1 若允许）
        int melee = Mathf.RoundToInt(strength * ratio.x / sum);
        int ranged = Mathf.RoundToInt(strength * ratio.y / sum);
        int elite = Mathf.RoundToInt(strength * ratio.z / sum);

        // 用确定性随机打散填充顺序（同强度 → 同序列，sim 可对拍）
        var paced = new List<MonsterDef>(melee + ranged + elite);
        paced.AddRange(PickDefs(MonsterType.Raider, melee));
        paced.AddRange(PickDefs(MonsterType.Slinger, ranged));
        paced.AddRange(PickDefs(MonsterType.Brute, elite));
        Shuffle(paced);

        int cap = ActivePortal.Def != null ? ActivePortal.Def.maxConcurrentMonsters : 30;
        foreach (var md in paced)
        {
            // 并发上限（maxConcurrentMonsters）：超限暂停直到有空位
            while (MonsterController.ActiveCount >= cap) yield return null;
            Vector2 pos = SpawnOffsetNearPortal();
            if (MonsterSpawner.Spawn(md, pos) != null)
                yield return new WaitForSeconds(NextIntervalSeconds());
        }
    }

    // ========================================================================
    //  波次强度（复用 2_8 强度曲线，冬季/非线性D266 共享）
    // ========================================================================

    /// <summary>
    /// 单波规模（步骤9 强度曲线）：基础成长 + 难度系数(D236) + 冬季 + 烈度递减。
    /// raw = strengthBase + 天数增长；× 难度waveCoeff；× 冬季倍率；× 烈度递减 aftermathDecayRate^存活夜；封顶 strengthCap。
    /// </summary>
    private int ComputeWaveStrength(float winterMult, Portal portal)
    {
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
        float raw = _config.strengthBase + _config.strengthGrowthPerDay * day;

        // 难度系数（D236：Easy 0.7 / Normal 1.0 / Hard 1.3）
        if (_disasterConfig != null)
        {
            int difficulty = DifficultyManager.Instance != null
                ? Mathf.Max(1, DifficultyManager.Instance.CurrentDifficulty) : 1;
            raw *= _disasterConfig.GetWaveCoefficient(difficulty);
        }

        raw *= winterMult;

        // 非线性加压（D266）
        if (_config.enableNonLinearDifficulty)
            raw *= 1f + day * _config.disasterStrengthGrowPerDay;

        // 烈度递减：未摧毁传送门持续袭击 → 每存活夜 ×aftermathDecayRate（§4.4，默认0.8=-20%）
        if (portal != null && portal.Def != null && portal.survivedNights > 0)
            raw *= Mathf.Pow(portal.Def.aftermathDecayRate, portal.survivedNights);

        int cap = _disasterConfig != null ? _disasterConfig.strengthCap : _config.strengthCap;
        return Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(raw)), cap);
    }

    // ========================================================================
    //  传送门实体生成（2_14 步骤4 注释约定"步骤5 传送门生成订阅"，本类为此订阅落地方）
    // ========================================================================

    /// <summary>
    /// 步骤13 放置检测（§2.2）：随机选点 + 半径 recheckRadius 内无王国建筑(Active+Human)才合法；
    /// 不合法则重试，最多 maxPlacementRetries 次，全失败则返回 zero（调用侧决定当晚放弃）。
    /// </summary>
    private Vector2 PickPortalPlacement()
    {
        int recheckRadius = _disasterConfig != null ? _disasterConfig.recheckRadius : 10;
        int maxRetries = _disasterConfig != null ? _disasterConfig.maxPlacementRetries : 5;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            Vector2 cand = RandomPortalPoint();
            if (IsPlacementLegal(cand, recheckRadius)) return cand;
        }
        Debug.LogWarning("[WaveDirector] 传送门放置重试用尽，本夜不生成（极低概率）。");
        return Vector2.zero;
    }

    /// <summary>生成候选点：主城锚点沿随机方向外推一段（王国区外，远离出生地），缺锚点用单位圆随机。</summary>
    private Vector2 RandomPortalPoint()
    {
        if (WorldManager.Instance != null)
        {
            Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
            if (anchor != Vector2.zero)
            {
                float ang = NextFloat() * 360f;
                Vector2 dir = Quaternion.Euler(0, 0, ang) * Vector2.right;
                // 外推 40~80 格（格→世界），确保落在王国区外；重试由调用方计数
                float dist = (40f + NextFloat() * 40f) * CellSize();
                return anchor + dir * dist;
            }
        }
        return NextInsideUnitCircle() * 40f;
    }

    /// <summary>半径 radius（格）内无任何 Active + Human 阵营王国建筑 → 合法。占位含 False（矿洞/据点是基建，禁邻近）。</summary>
    private bool IsPlacementLegal(Vector2 worldPos, int radius)
    {
        if (BuildingRegistry.Instance == null) return true;   // 无建筑系统时放行（脚手架场景兜底）
        float radiusWorld = radius * CellSize();
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || !b.IsActive) continue;
            if (b.GetFaction() != Faction.Human_Player) continue;      // 只避开玩家王国建筑（AI 王国后置预留）
            float d = Vector2.Distance(b.GetPosition(), worldPos);
            if (d <= radiusWorld) return false;
        }
        return true;
    }

    private Portal SpawnPortalEntity(Vector2 worldPos)
    {
        if (GridSystem.Instance == null) return null;
        GridCoord coord = GridSystem.Instance.WorldToCoord(worldPos) ?? new GridCoord(0, 0);

        var pgo = new GameObject("Portal_Disaster");
        pgo.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
        var portal = pgo.AddComponent<Portal>();
        var portalDef = Resources.Load<PortalDef>("Config/Disaster/PortalDef");
        portal.Initialize(coord, portalDef);
        return portal;
    }

    // ========================================================================
    //  确定性随机辅助（R4：System.Random 派生 worldSeed，禁 UnityEngine.Random）
    // ========================================================================

    private float NextFloat()
    {
        return (float)_rng.NextDouble();
    }

    private Vector2 NextInsideUnitCircle()
    {
        return new Vector2(NextFloat() * 2f - 1f, NextFloat() * 2f - 1f);
    }

    private float NextRange(float min, float max)
    {
        return min + NextFloat() * (max - min);
    }

    private int NextRange(int min, int maxExclusive)
    {
        if (maxExclusive <= min) return min;
        return min + _rng.Next(maxExclusive - min);
    }

    private float NextIntervalSeconds()
    {
        return NextRange(_config.spawnIntervalRange.x, _config.spawnIntervalRange.y);
    }

    /// <summary>打乱列表顺序（确定性）。</summary>
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextRange(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>取某类型怪物 Def（Resources/Config/Disaster/{Raider,Slinger,Brute}.asset）；取不到返回 null（调用侧跳过）。</summary>
    private List<MonsterDef> PickDefs(MonsterType type, int count)
    {
        var list = new List<MonsterDef>();
        if (count <= 0) return list;
        string path = "Config/Disaster/" + type.ToString();
        var def = Resources.Load<MonsterDef>(path);
        if (def == null || def.prefab == null)
        {
            Debug.LogWarning($"[WaveDirector] 缺 {path} 资产/预制，跳过该类型出怪。");
            return list;
        }
        for (int i = 0; i < count; i++) list.Add(def);
        return list;
    }

    private Vector2 SpawnOffsetNearPortal()
    {
        // 召唤出生点=传送门中心附近（出生表现偏移非决策，R4 界内由 _rng 驱动）
        float cell = CellSize();
        return (Vector2)ActivePortal.transform.position + NextInsideUnitCircle() * (cell * 0.8f);
    }

    private static float CellSize()
    {
        return MapRenderService.DefaultCellSize.x;
    }

    // ========================================================================
    //  ISaveableSpawner（2_14 步骤14）：读档重建传送门
    //  SaveManager 阶段 1.5 调此创建实例并注册 → 阶段 2 Scene 分发 Portal.LoadState 恢复
    // ========================================================================

    public void SpawnFromSave(ModuleSaveEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.json)) return;
        PortalSaveData data;
        try { data = JsonUtility.FromJson<PortalSaveData>(entry.json); }
        catch (System.Exception ex) { Debug.LogError($"[WaveDirector] PortalSaveData 反序列化失败: {ex}"); return; }

        if (data == null) return;
        if (ActivePortal != null)
        {
            Debug.LogWarning($"[WaveDirector] 读档重建时已存在传送门，跳过重复重建（saveId={entry.saveId}）。");
            return;
        }
        if (GridSystem.Instance == null) { return; }

        var coord = new GridCoord(data.portalGridX, data.portalGridY);
        Vector2 worldPos = GridSystem.Instance.CoordToWorld(coord);

        var pgo = new GameObject("Portal_Disaster_Reload");
        pgo.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
        var portal = pgo.AddComponent<Portal>();
        var portalDef = Resources.Load<PortalDef>("Config/Disaster/PortalDef");
        portal.Initialize(coord, portalDef);
        // 覆盖 SaveId，使 SaveManager 阶段2 能把 LoadState 分发给正确实例
        portal.OverrideSaveId(entry.saveId);
        ActivePortal = portal;
        Debug.Log($"[WaveDirector] 读档重建传送门 @ ({coord.x},{coord.y}) saveId={entry.saveId}。");
    }
}