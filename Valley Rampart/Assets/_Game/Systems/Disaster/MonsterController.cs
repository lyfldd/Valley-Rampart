using System.Collections.Generic;
using UnityEngine;

// 2_14 敌人怪物实体（实施计划步骤6 / 设计稿 §3.1/§3.2）
// 职责：读 MonsterDef 应用属性 + Faction=Undead + 可被玩家攻击 + 从传送门召唤锚点。
//
// 出怪路线（用户裁决「造 MonsterAI」，2026-08-24）：
//   普通怪（Raider/Slinger）挂 MonsterAI（规则模式切换器，不进训练）——本片交付；
//   精英怪（Brute）挂 NPCBrain + MonsterMode 注入归步骤7（MonsterMode 打架/训练接线），
//   本片精英暂复用 MonsterController + MonsterAI 实体（IsElite 标记就位），可生成/可攻击。
//
// 复用 UnitController 全套：伤害管线（IDamageable/TakeDamage）/ 寻路（MoveTowards）/ 空间分区 / 对象池。
// 确定性 R4：属性来自 MonsterDef（SO），无 UnityEngine.Random 进决策。
public class MonsterController : UnitController
{
    public MonsterDef def;
    public MonsterMode mode = MonsterMode.Raiding;   // 当前行为模式（守门/出击/撤退/掠夺；完整态机步骤7）

    public MonsterType Type => def != null ? def.type : MonsterType.Raider;
    public bool IsElite { get; private set; }
    public float VisionRadiusCells { get; private set; } = 8f;
    public int CarryResource { get; private set; } = 5;
    public float RetreatHpRatio { get; private set; } = 0.2f;

    /// <summary>由 MonsterSpawner 在 SpawnUnit.Initialize(base) 之后调用，把 MonsterDef 注入驱动怪物行为字段。</summary>
    public void InitMonster(MonsterDef monsterDef)
    {
        if (monsterDef == null) return;
        def = monsterDef;
        IsElite = monsterDef.isElite;
        VisionRadiusCells = monsterDef.visionRadiusCells;
        CarryResource = monsterDef.carryResource;
        RetreatHpRatio = monsterDef.retreatHpRatio;
    }

    public void SetMode(MonsterMode newMode) => mode = newMode;

    /// <summary>攻击配置（从 MonsterDef 构造；Slinger 远程射程圆=6 格 D258，近战肉搏）。</summary>
    public AttackProfile BuildAttackProfile()
    {
        return new AttackProfile
        {
            attack = Attack,
            range = Mathf.Max(0.5f, def != null ? def.attackRangeCells : 1f),
            cd = Mathf.Max(0.1f, def != null ? def.attackInterval : 2f),
            isRanged = def != null && def.isRangedAttack,
            projectileSpeed = def != null ? def.projectileSpeed : 0f,
        };
    }

    /// <summary>感知半径内最近玩家单位（GridSystem 邻近格扫描，y 地面+飞行两层；镜像 UnitController.FindNearestEnemy）。</summary>
    public IDamageable FindNearestHuman(float rangeWorld)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return null;
        float cellSize = GridSystem.Instance.Config.cellSize.x;
        var centerOpt = GridSystem.Instance.WorldToCoord(_rb.position);
        if (!centerOpt.HasValue) return null;
        GridCoord center = centerOpt.Value;
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(rangeWorld / cellSize));

        IDamageable nearest = null;
        float nearestDist = float.MaxValue;
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || uc == this || !uc.IsAlive || uc.CurrentHp <= 0) continue;
                    if (uc.GetFaction() != Faction.Human_Player) continue;
                    float d = Vector2.Distance(_rb.position, uc.transform.position);
                    if (d < nearestDist) { nearestDist = d; nearest = uc; }
                }
            }
        }
        return nearest;
    }
}


/// <summary>怪物生成器：把 MonsterDef 桥接成运行时 UnitData，走 UnitFactory.SpawnUnit（复用对象池/注册/事件/IDamageable）。</summary>
public static class MonsterSpawner
{
    // 运行时合成的 UnitData 按 MonsterType 缓存（每类型一份，规避每召唤 CreateInstance 泄漏）。
    private static readonly Dictionary<MonsterType, UnitData> s_dataCache = new();

    public static MonsterController Spawn(MonsterDef def, Vector2 position)
    {
        if (def == null || def.prefab == null)
        {
            Debug.LogError("[MonsterSpawner] MonsterDef 或其 prefab 为空，无法生成怪物。");
            return null;
        }
        if (UnitFactory.Instance == null) return null;

        UnitData data = GetUnitData(def);
        GameObject go = UnitFactory.Instance.SpawnUnit(data, position);
        if (go == null) return null;

        MonsterController mc = go.GetComponent<MonsterController>();
        mc?.InitMonster(def);
        return mc;
    }

    private static UnitData GetUnitData(MonsterDef def)
    {
        if (s_dataCache.TryGetValue(def.type, out var cached)) return cached;

        float cell = CellSize();
        var u = ScriptableObject.CreateInstance<UnitData>();
        u.name = "Monster_" + def.type;
        u.faction = Faction.Undead;
        u.occupation = Occupation.Monster;
        u.prefab = def.prefab;
        u.maxHp = def.hp;
        u.attack = def.attack;
        u.defense = 0;
        // 格/秒 -> 世界单位/秒（doc 1 §1.6：一格的横向世界长度≈cellSize.x）
        u.walkSpeed = def.speedCellsPerSec * cell;
        u.runSpeed = u.walkSpeed * 2f;

        s_dataCache[def.type] = u;
        return u;
    }

    private static float CellSize()
    {
        return (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            ? GridSystem.Instance.Config.cellSize.x : 1.28f;
    }
}