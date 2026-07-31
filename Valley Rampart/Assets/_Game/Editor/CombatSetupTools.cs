using UnityEditor;
using UnityEngine;

/// <summary>
/// 3.4 伤害管线验证资产一键创建工具。
/// 菜单：ValleyRampart/3.4 伤害管线/一键创建验证资产
///
/// 创建：
///   - DamageConfig.asset（全局伤害规则）
///   - 5 个 NpcProfessionDef.asset（工人/近战士兵/远程士兵/近战敌人/远程敌人）
///   - 5 个 NPC Prefab（复制 Ruler 改色 + 加 StubAttacker/DamageFeedback）
/// </summary>
public static class CombatSetupTools
{
    private const string CONFIG_DIR = "Assets/Resources/Config";
    private const string UNITDATA_DIR = "Assets/Resources/UnitData";
    private const string PREFAB_DIR = "Assets/Resources/UnitPrefabs";
    private const string RULER_PREFAB = "Assets/Resources/UnitPrefabs/Human_Player_Ruler.prefab";

    [MenuItem("ValleyRampart/3.4 伤害管线/一键创建验证资产")]
    public static void CreateAll()
    {
        EnsureDirectories();
        CreateDamageConfig();
        CreateNpcProfessionDefs();
        CreateNpcPrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[3.4] 验证资产创建完成！请将 DamageSystem/ProjectileManager 挂到场景中。");
    }

    private static void EnsureDirectories()
    {
        EnsureDir(CONFIG_DIR);
        EnsureDir(UNITDATA_DIR);
        EnsureDir(PREFAB_DIR);
    }

    private static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    // ===== DamageConfig =====

    private static void CreateDamageConfig()
    {
        string path = $"{CONFIG_DIR}/DamageConfig.asset";
        var existing = AssetDatabase.LoadAssetAtPath<DamageConfig>(path);
        if (existing != null) return; // 已存在不覆盖

        var config = ScriptableObject.CreateInstance<DamageConfig>();
        AssetDatabase.CreateAsset(config, path);
        EditorUtility.SetDirty(config);
        Debug.Log($"[3.4] DamageConfig 创建: {path}");
    }

    // ===== NpcProfessionDef（5 个职业配置）=====

    private static void CreateNpcProfessionDefs()
    {
        // 工人（Human_Player, Civilian，无攻击）
        CreateProfession("Human_Player_Civilian", Faction.Human_Player, Occupation.Civilian,
            attack: 5, defense: 0, maxHp: 80, walkSpeed: 3f, runSpeed: 6f,
            attackRange: 0f, attackCD: 0f, isRanged: false, projectileSpeed: 0f);

        // 近战士兵（Human_Player, Warrior）
        CreateProfession("Human_Player_Warrior", Faction.Human_Player, Occupation.Warrior,
            attack: 10, defense: 20, maxHp: 100, walkSpeed: 3f, runSpeed: 6f,
            attackRange: 1f, attackCD: 1f, isRanged: false, projectileSpeed: 0f);

        // 远程士兵（Human_Player, Archer）
        CreateProfession("Human_Player_Archer", Faction.Human_Player, Occupation.Archer,
            attack: 8, defense: 10, maxHp: 80, walkSpeed: 3f, runSpeed: 6f,
            attackRange: 5f, attackCD: 1.5f, isRanged: true, projectileSpeed: 25f);

        // 近战敌人（Undead, Warrior）
        CreateProfession("Undead_Warrior", Faction.Undead, Occupation.Warrior,
            attack: 10, defense: 20, maxHp: 100, walkSpeed: 3f, runSpeed: 6f,
            attackRange: 1f, attackCD: 1f, isRanged: false, projectileSpeed: 0f);

        // 远程敌人（Undead, Archer）
        CreateProfession("Undead_Archer", Faction.Undead, Occupation.Archer,
            attack: 8, defense: 10, maxHp: 80, walkSpeed: 3f, runSpeed: 6f,
            attackRange: 5f, attackCD: 1.5f, isRanged: true, projectileSpeed: 25f);
    }

    private static void CreateProfession(
        string name, Faction faction, Occupation occupation,
        int attack, int defense, int maxHp, float walkSpeed, float runSpeed,
        float attackRange, float attackCD, bool isRanged, float projectileSpeed)
    {
        string path = $"{UNITDATA_DIR}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<NpcProfessionDef>(path) != null) return;

        var def = ScriptableObject.CreateInstance<NpcProfessionDef>();
        def.faction = faction;
        def.occupation = occupation;
        def.walkSpeed = walkSpeed;
        def.runSpeed = runSpeed;
        def.maxHp = maxHp;
        def.attack = attack;
        def.defense = defense;
        def.attackRange = attackRange;
        def.attackCD = attackCD;
        def.isRanged = isRanged;
        def.projectileSpeed = projectileSpeed;

        AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        Debug.Log($"[3.4] NpcProfessionDef 创建: {path}");
    }

    // ===== NPC Prefab（复制 Ruler 改色 + 加组件）=====

    private static void CreateNpcPrefabs()
    {
        GameObject rulerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RULER_PREFAB);
        if (rulerPrefab == null)
        {
            Debug.LogError($"[3.4] 找不到 Ruler Prefab: {RULER_PREFAB}");
            return;
        }

        CreateNpcPrefab("Human_Player_Warrior", rulerPrefab, new Color(0.3f, 0.7f, 1f));   // 蓝色
        CreateNpcPrefab("Human_Player_Archer", rulerPrefab, new Color(0.3f, 1f, 0.7f));    // 青绿色
        CreateNpcPrefab("Human_Player_Civilian", rulerPrefab, new Color(0.7f, 0.7f, 0.7f)); // 灰色
        CreateNpcPrefab("Undead_Warrior", rulerPrefab, new Color(1f, 0.3f, 0.3f));          // 红色
        CreateNpcPrefab("Undead_Archer", rulerPrefab, new Color(1f, 0.6f, 0.2f));           // 橙红色
    }

    private static void CreateNpcPrefab(string name, GameObject sourcePrefab, Color tint)
    {
        string path = $"{PREFAB_DIR}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        // 实例化 Ruler Prefab
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        go.name = name;

        // 改色
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;

        // 移除 PlayerInputHandler（NPC 不需要玩家输入）
        var input = go.GetComponent<PlayerInputHandler>();
        if (input != null) Object.DestroyImmediate(input);

        // 添加 StubAttacker
        if (go.GetComponent<StubAttacker>() == null)
            go.AddComponent<StubAttacker>();

        // 添加 DamageFeedback
        if (go.GetComponent<DamageFeedback>() == null)
            go.AddComponent<DamageFeedback>();

        // 保存为新 Prefab
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log($"[3.4] NPC Prefab 创建: {path} (颜色: {tint})");
    }
}
