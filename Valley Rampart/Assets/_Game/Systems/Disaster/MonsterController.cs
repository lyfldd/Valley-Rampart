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
    public MonsterMode mode = MonsterMode.Raiding;   // 当前行为模式（守门/出击/撤退/掠夺）

    /// <summary>本怪所属传送门召唤锚点（出生记录，回援/撤退的 home）。由 MonsterSpawner 在生成时写入。</summary>
    public Vector2 HomePortalPos;

    public MonsterType Type => def != null ? def.type : MonsterType.Raider;
    public bool IsElite { get; private set; }
    public float VisionRadiusCells { get; private set; } = 8f;
    /// <summary>⚠️ `M1-D` 件8 后**无消费**（原唯一消费 ＝ `DropLoot` 掉落量 · 已退役）；保留承载 `MonsterDef.carryResource`（供未来换代）。</summary>
    public int CarryResource { get; private set; } = 5;
    public float RetreatHpRatio { get; private set; } = 0.2f;

    // 存活计数（MonsterSpawner/Portal 召唤上限 maxConcurrentMonsters 判据；InitMonster +1 / Die -1）。
    private static int s_activeCount;
    private bool _counted;   // 防重复计数（池化/未 InitMonster 兜底）
    public static int ActiveCount => s_activeCount;

    /// <summary>由 MonsterSpawner 在 SpawnUnit.Initialize(base) 之后调用，把 MonsterDef 注入驱动怪物行为字段。</summary>
    public void InitMonster(MonsterDef monsterDef)
    {
        if (monsterDef == null) return;
        def = monsterDef;
        IsElite = monsterDef.isElite;
        VisionRadiusCells = monsterDef.visionRadiusCells;
        CarryResource = monsterDef.carryResource;
        RetreatHpRatio = monsterDef.retreatHpRatio;
        if (!_counted) { _counted = true; s_activeCount++; }
    }

    public void SetMode(MonsterMode newMode) => mode = newMode;

    /// <summary>
    /// 2_14 怪物死亡：走基类注销/回池。
    /// ⛔ `M1-D` 件8（`D802`/`D803` `Q8` 裁）：原 `DropLoot()`（`D213`：`CarryResource` × `def.lootResource` **凭空落箱**）**退役** ——
    ///   怪物**无仓** ⇒ 按「死者仓＋标签」统一口径**不产生掉落箱**（`09` §9.7 清单无怪物项）。
    ///   ⚠️ **行为变化已声明**：怪物被击杀不再掉落资源（`D803` 裁「不得静默退役」· 判据 9）；
    ///   ⚠️ `MonsterDef.carryResource`/`lootResource` 字段**保留**（标注「M1-D 后无消费」）；⛔ 不落箱 ＝ 本批预期。
    /// </summary>
    protected override void Die()
    {
        if (_counted) { _counted = false; s_activeCount--; }
        base.Die();
    }

    // ===== `DropLoot()` / `BuildLootPack()` —— ⛔ `M1-D` 件8 **已退役**（`D213` 怪物掉落）=====
    //   改前：`CarryResource`（＝掉落数量数值参数 · ⛔ 非仓内容）＋ `def.lootResource`（模板类型）⇒ 死亡点凭空落箱。
    //   改后（统一口径）：怪物无仓 ⇒ 不落箱。⚠️ 行为变化：击杀怪物不再产出资源箱（`D803` 裁 · 已显式声明）。
    //   grep 证据（改后）：`DropLoot|BuildLootPack` 全库 0 命中（本题注除外）。

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

    /// <summary>
    /// 感知半径内最近玩家单位（D485 单遍过滤法，镜像 UnitController.FindNearestEnemy）。
    /// HH.243（DZ-149/D693）：旧实现 `for(y=0;y<=1;y++)` 把 `GridCoord.y`（2.5D 已是地图行号）
    /// 当"地面+飞行两层"⇒ 只扫最南两行，其余行玩家单位对怪不可见。
    /// 修法照抄 D485（复用 PerceptionSystem.QueryNearby），**kingdomId==0 守卫原样保留**。
    /// </summary>
    public IDamageable FindNearestHuman(float rangeWorld)
    {
        if (UnitRegistry.Instance == null) return null;
        // D695 返工：此处须传 GetFaction()（＝Faction.Monster），不可传 Faction.PlayerCamp。
        // QueryNearby 的 findEnemies=true 语义＝收「f != myFaction && f != None」（PerceptionSystem.cs:38-43），
        // 传 PlayerCamp 会把玩家整个排除、返回非玩家阵营 ⇒ 本方法永远返回不了"人"（功能回归）。
        PerceptionSystem.QueryNearby(_rb.position, rangeWorld, GetFaction(), true, _queryResults);
        IDamageable nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < _queryResults.Count; i++)
        {
            var uc = _queryResults[i] as UnitController;
            if (uc == null || uc == this) continue;
            // 2_17 步骤4 patch C（③）：单位级盲区守卫——步骤3 实体化的 AI 工人是 PlayerCamp+kingdomId>0 冒充态，
            // 会被怪感知为玩家目标；这里补 kingdomId==0（与建筑 L192 同模式同语义），P0 只袭玩家。
            // 步骤10 Faction 收编时随迁移退役（与既有守卫同生命周期）。
            if (uc.kingdomId != 0) continue;
            float d = Vector2.Distance(_rb.position, uc.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = uc; }
        }
        return nearest;
    }
}


/// <summary>怪物生成器：把 MonsterDef 桥接成运行时 UnitData，走 UnitFactory.SpawnUnit（复用对象池/注册/事件/IDamageable）。</summary>
public static class MonsterSpawner
{
    public static MonsterController Spawn(MonsterDef def, Vector2 position)
    {
        // T11 考跑守卫（HH.92）：全怪物源漏斗（传送门/波次/冬怪统一经 MonsterSpawner）考跑期静默。
        var tmMon = TimeManager.Instance;
        if (tmMon != null && tmMon.TestHarnessMode) return null;

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
        if (mc != null)
        {
            mc.HomePortalPos = position;   // 召唤锚点：回援/撤退 home
            mc.InitMonster(def);

            // 段② Q1-B（D252）：仅精英怪挂 NPCBrain + MonsterMode 注入（桥接卡使 UnitFactory brain.Init 生效）。
            // 普通怪即便 prefab 带 NPCBrain，也因 data 非 NpcProfessionDef 不 Init → 零影响。
            if (def.isElite)
            {
                var brain = go.GetComponent<NPCBrain>();
                if (brain != null) brain.ConfigureMonster(mc.mode, position);
            }
        }
        return mc;
    }

    private static UnitData GetUnitData(MonsterDef def)
    {
        float cell = CellSize();
        // 步骤9 强度曲线：怪物属性 × 难度系数(D236) × 天数增长(1 + day×growthRate)。
        // 属性随 day/difficulty 变 → 不缓存，每只按需生成（UnitFactory 走对象池复用）。
        float scale = CurrentScale();

        UnitData u;
        if (def.isElite)
        {
            // 段② Q2-A（D252）：精英（Brute）挂 NPCBrain，data 须为 NpcProfessionDef，
            // UnitFactory.SpawnUnit 走 `data is NpcProfessionDef` 分支自动 brain.Init。
            // MonsterDef.Brute 属性桥接成 NpcProfessionDef（决策核吃 profession 快照，怪物字段仍读 MonsterDef）。
            var npc = ScriptableObject.CreateInstance<NpcProfessionDef>();
            npc.name = "Monster_" + def.type;
            npc.faction = Faction.Monster;
            npc.occupation = Occupation.Monster;
            npc.prefab = def.prefab;
            npc.maxHp = Mathf.RoundToInt(def.hp * scale);
            npc.attack = Mathf.RoundToInt(def.attack * scale);
            npc.defense = 0;
            // 格/秒 -> 世界单位/秒
            npc.walkSpeed = def.speedCellsPerSec * cell;
            npc.runSpeed = npc.walkSpeed * 2f;
            // 战斗/感知从 MonsterDef 桥接（决策核消费）
            npc.attackRange = def.attackRangeCells;
            npc.attackCD = def.attackInterval;
            npc.isRanged = def.isRangedAttack;
            npc.projectileSpeed = def.projectileSpeed;
            npc.perceptionRadius = def.visionRadiusCells;
            u = npc;
        }
        else
        {
            var bu = ScriptableObject.CreateInstance<UnitData>();
            bu.name = "Monster_" + def.type;
            bu.faction = Faction.Monster;
            bu.occupation = Occupation.Monster;
            bu.prefab = def.prefab;
            bu.maxHp = Mathf.RoundToInt(def.hp * scale);
            bu.attack = Mathf.RoundToInt(def.attack * scale);
            bu.defense = 0;
            // 格/秒 -> 世界单位/秒
            bu.walkSpeed = def.speedCellsPerSec * cell;
            bu.runSpeed = bu.walkSpeed * 2f;
            u = bu;
        }

        return u;
    }

    /// <summary>步骤9 强度缩放因子 = 难度系数(D236) × (1 + day×growthRate)。缺配置回退 1（原始 MonsterDef 值）。</summary>
    private static float CurrentScale()
    {
        var cfg = Resources.Load<PortalDisasterConfig>("Config/Disaster/PortalDisasterConfig");
        if (cfg == null) return 1f;
        int difficulty = DifficultyManager.Instance != null
            ? Mathf.Max(1, DifficultyManager.Instance.CurrentDifficulty) : 2;
        int day = TimeManager.Instance != null ? Mathf.Max(1, TimeManager.Instance.CurrentDay) : 1;
        float difficultyScale = cfg.GetWaveCoefficient(difficulty);
        float dayScale = 1f + day * cfg.growthRate;
        return Mathf.Max(0.1f, difficultyScale * dayScale);
    }

    private static float CellSize()
    {
        return (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            ? GridSystem.Instance.Config.cellSize.x : 1.28f;
    }
}