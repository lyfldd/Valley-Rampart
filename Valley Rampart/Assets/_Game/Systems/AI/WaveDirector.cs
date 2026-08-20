using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌袭 → 偶发传送门灾害调度（2_8 实施计划 步骤7 / 2_8_AI应用层 §5.3，D177~D189）。
/// 传送门实体/怪物资产归 2_14；本篇落 WaveDirector 调度与波次参数，传送门未落地时按 SpawnDef 直出怪占位
/// （沿方向在靠近高价值目标侧直接用 UnitFactory 生成怪物，见 <see cref="SpawnGroup"/>）。
/// </summary>
public class WaveDirector : Singleton<WaveDirector>
{
    private WaveConfig _config;

    /// <summary>距上次灾害经过的夜晚数（天数保底 / 防长草判定用）。</summary>
    private int _nightsSinceLastDisaster;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        // Data/WaveConfig.asset 不存在时回退到类内默认值（字段初始值即 §三 占位表）。
        _config = Resources.Load<WaveConfig>("Data/WaveConfig") ?? ScriptableObject.CreateInstance<WaveConfig>();
    }

    public WaveConfig Config => _config;

    /// <summary>
    /// 判断今晚是否触发灾害（概率 + 天数保底 + 连续未触发强制），并推进夜晚计数。
    /// 由 DayCycleSettlement 入夜时调用；返回 true 表示应触发，随后调 <see cref="SpawnDisaster"/>。
    /// 非线性加压（D266）：启用时每晚概率随天数递增，不再是固定 0.3。
    /// </summary>
    public bool ShouldTriggerDisasterThisNight()
    {
        if (_config == null) return false;
        _nightsSinceLastDisaster++;
        bool cadence = _nightsSinceLastDisaster >= _config.disasterEveryNDays;          // 天数保底触发
        bool hardCap = _nightsSinceLastDisaster >= _config.disasterGuaranteeNDays;      // 连续未触发强制（防长草）
        float prob = ComputeNightProbability();
        bool rolled = Random.value < prob;                                              // 每晚概率（含非线性递增）
        if (cadence || hardCap || rolled)
            Debug.Log($"[WaveDirector] 灾害判定: 夜#{_nightsSinceLastDisaster} 概率={prob:0.##} "
                + (cadence ? "[天数保底]" : hardCap ? "[防长草强制]" : "[概率触发]"));
        return cadence || hardCap || rolled;
    }

    /// <summary>
    /// 每晚灾害概率：非线性加压启用时 = clamp(base + 天数×递增 + 难度档系数)；
    /// 未启用回退旧固定 <see cref="WaveConfig.disasterProbPerNight"/>。
    /// </summary>
    private float ComputeNightProbability()
    {
        if (!_config.enableNonLinearDifficulty)
            return _config.disasterProbPerNight;
        int day = TimeManager.Instance != null ? Mathf.Max(1, TimeManager.Instance.CurrentDay) : 1;
        float difficulty = DifficultyManager.Instance != null
            ? Mathf.Max(1, DifficultyManager.Instance.CurrentDifficulty) : 1;
        float grow = difficulty > 1 ? _config.disasterProbGrowPerDay * (difficulty - 1) : 0f;
        return Mathf.Clamp01(_config.disasterDifficultyBaseProb + day * grow);
    }

    /// <summary>
    /// 触发灾害：按波次逐波出怪（2_14 的传送门召唤入口预留，2_14 落地后改此生成传送门再分批召唤）。
    /// </summary>
    public void SpawnDisaster()
    {
        _nightsSinceLastDisaster = 0;
        if (_config == null || !gameObject.activeInHierarchy) return;
        StartCoroutine(SpawnDisasterCoroutine());
    }

    // ========================================================================
    //  波次调度
    // ========================================================================

    private IEnumerator SpawnDisasterCoroutine()
    {
        List<SpawnDef> spawns = GetThreatSpawns();

        int difficulty = DifficultyManager.Instance != null
            ? Mathf.Max(1, DifficultyManager.Instance.CurrentDifficulty)
            : 1;
        // 少波次 D97：baseWaves=2 / wavePerDifficulty=1 → Easy 3 / Normal 4 / Hard 5
        int waveCount = _config.baseWaves + difficulty * _config.wavePerDifficulty;

        float winterMult = TimeManager.Instance != null
            ? TimeManager.Instance.MonsterStrengthMultiplierForCurrentSeason
            : 1f;

        for (int w = 0; w < waveCount; w++)
        {
            int strength = ComputeWaveStrength(winterMult);
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
            Debug.Log($"[WaveDirector] 第{w + 1}/{waveCount}波来袭: 强度={strength} 第{day}天");

            List<List<SpawnDef>> groups = BuildDirectionGroups(spawns);
            if (groups.Count == 0) groups = BuildFallbackGroups();
            if (groups.Count == 0) yield break;

            // 每波选 1~2 个方向聚合组（写两个方向敌潮同时来袭冒烟点）
            int groupCount = Random.Range(1, Mathf.Min(2, groups.Count) + 1);
            List<List<SpawnDef>> chosen = PickRandomDistinctGroups(groups, groupCount);

            int perGroup = Mathf.Max(1, Mathf.RoundToInt(strength / (float)chosen.Count));
            foreach (var group in chosen)
                yield return SpawnGroup(group, perGroup);

            // 波间留一小段时间（组内错峰由 spawnIntervalRange 控制）
            yield return new WaitForSeconds(Random.Range(_config.spawnIntervalRange.x, _config.spawnIntervalRange.y));
        }
    }

    /// <summary>单波规模 = strengthBase + 天数增长，封顶 strengthCap，冬季 + 非线性强度系数放大。</summary>
    private int ComputeWaveStrength(float winterMult)
    {
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
        float raw = _config.strengthBase + _config.strengthGrowthPerDay * day;
        raw *= winterMult;
        // 非线性加压强度系数（D266）：预=1 + 天数×强度递增，随天数非线性放大
        if (_config.enableNonLinearDifficulty)
            raw *= 1f + day * _config.disasterStrengthGrowPerDay;
        return Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(raw)), _config.strengthCap);
    }

    // ========================================================================
    //  方向聚合（R1）：direction 夹角 ≤ directionMergeAngle 归一组
    // ========================================================================

    /// <summary>把威胁刷点按来袭方向聚合成组（组内方向夹角 ≤45°，同波次同组）。</summary>
    private List<List<SpawnDef>> BuildDirectionGroups(List<SpawnDef> spawns)
    {
        var groups = new List<List<SpawnDef>>();
        if (spawns == null || spawns.Count == 0) return groups;

        bool[] used = new bool[spawns.Count];
        for (int i = 0; i < spawns.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var group = new List<SpawnDef> { spawns[i] };
            for (int j = i + 1; j < spawns.Count; j++)
            {
                if (used[j]) continue;
                if (DirectionDiffDeg(spawns[i].direction, spawns[j].direction) <= _config.directionMergeAngle)
                {
                    used[j] = true;
                    group.Add(spawns[j]);
                }
            }
            groups.Add(group);
        }
        return groups;
    }

    /// <summary>无威胁刷点时的兜底：沿 kingdom 锚点向外一圈合成单组。</summary>
    private List<List<SpawnDef>> BuildFallbackGroups()
    {
        if (WorldManager.Instance == null) return new List<List<SpawnDef>>();
        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        if (anchor == Vector2.zero) return new List<List<SpawnDef>>();

        var group = new List<SpawnDef>();
        for (int d = 0; d < 4; d++)
        {
            Vector2 dir = Quaternion.Euler(0, 0, d * 90f) * Vector2.right;   // 四正方向
            group.Add(new SpawnDef { coord = Vector2Int.zero, direction = new Vector2(dir.x, dir.y), strength = 1, faction = Faction.Undead });
        }
        return new List<List<SpawnDef>> { group };
    }

    /// <summary>两方向夹角（度）。</summary>
    private static float DirectionDiffDeg(Vector2 a, Vector2 b)
    {
        float d = Mathf.Clamp(Vector2.Dot(a.normalized, b.normalized), -1f, 1f);
        return Mathf.Acos(d) * Mathf.Rad2Deg;
    }

    private List<List<SpawnDef>> PickRandomDistinctGroups(List<List<SpawnDef>> groups, int count)
    {
        var copy = new List<List<SpawnDef>>(groups);
        var picked = new List<List<SpawnDef>>();
        for (int i = 0; i < count && copy.Count > 0; i++)
        {
            int idx = Random.Range(0, copy.Count);
            picked.Add(copy[idx]);
            copy.RemoveAt(idx);
        }
        return picked;
    }

    // ========================================================================
    //  出怪（直出怪占位。传送门实体归 2_14，此处用 UnitFactory 直接生成）
    // ========================================================================

    /// <summary>组内逐个出怪，错峰 spawnIntervalRange（2~5s）。</summary>
    private IEnumerator SpawnGroup(List<SpawnDef> group, int count)
    {
        if (group == null || group.Count == 0) yield break;
        for (int k = 0; k < count; k++)
        {
            SpawnDef sd = group[Random.Range(0, group.Count)];
            SpawnMonster(sd);
            yield return new WaitForSeconds(Random.Range(_config.spawnIntervalRange.x, _config.spawnIntervalRange.y));
        }
    }

    /// <summary>沿方向在靠近高价值目标侧（刷点附近）生成一只怪物。</summary>
    private void SpawnMonster(SpawnDef sd)
    {
        if (UnitFactory.Instance == null)
        {
            Debug.LogWarning("[WaveDirector] UnitFactory 不可用，跳过出怪。");
            return;
        }
        Vector2 pos = ResolveSpawnPos(sd);
        GameObject go = UnitFactory.Instance.SpawnUnit(Faction.Undead, Occupation.Warrior, pos);
        if (go == null)
            Debug.LogWarning("[WaveDirector] 出怪失败（Undead_Warrior 数据/预制缺失，占位待 2_14）。");
    }

    /// <summary>把 SpawnDef.coord 转世界坐标并加轻微抖动；无网格回退到 kingdom 锚点沿来袭方向反向外推。</summary>
    private Vector2 ResolveSpawnPos(SpawnDef sd)
    {
        if (GridSystem.Instance != null)
        {
            Vector2 cell = GridSystem.Instance.CoordToWorld(new GridCoord(sd.coord.x, sd.coord.y));
            return cell + (Vector2)Random.insideUnitCircle * 0.5f;
        }
        if (WorldManager.Instance != null)
        {
            Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
            if (anchor != Vector2.zero)
                return anchor - new Vector2(sd.direction.x, sd.direction.y) * 20f; // 反方向外推作为刷点
        }
        return Random.insideUnitCircle * 10f;
    }

    /// <summary>从当前活跃地图取威胁刷点（2_1 生成、2_8 消费）。</summary>
    private List<SpawnDef> GetThreatSpawns()
    {
        if (WorldManager.Instance != null && WorldManager.Instance.ActiveMap != null
            && WorldManager.Instance.ActiveMap.threatSpawns != null)
            return WorldManager.Instance.ActiveMap.threatSpawns;
        return new List<SpawnDef>();
    }
}