// ============================================================================
//  3.6 军事统一管理 - 资产批量创建器（菜单：ValleyRampart/3.6 CombatCore 资产）
//  一键生成：弹药 / 地面效果 / 装备 / 骑兵 / 工事 / 模块树 全部新资产。
//  依赖前置：AmmoDef/GroundEffectDef/EquipmentDef/FortificationDef/ModuleDef 类已编译。
// ============================================================================

using UnityEditor;
using UnityEngine;

public static class CreateCombatAssets
{
    private const string AmmoDir = "Assets/Resources/Ammo";
    private const string EquipDir = "Assets/Resources/Equipment";
    private const string FortDir = "Assets/Resources/Fortifications";
    private const string ModuleDir = "Assets/Resources/Modules";
    private const string UnitDir = "Assets/Resources/UnitData";

    [MenuItem("ValleyRampart/3.6 CombatCore 资产/一键创建全部（弹药/装备/骑兵/工事/模块树）")]
    public static void CreateAll()
    {
        EnsureFolder(AmmoDir);
        EnsureFolder(EquipDir);
        EnsureFolder(FortDir);
        EnsureFolder(ModuleDir);

        CreateGroundEffects();
        CreateAmmo();
        CreateEquipment();
        CreateCavalry();
        CreateFortifications();
        CreateModules();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateCombatAssets] 全部资产创建完成（Resources/Ammo, Equipment, Fortifications, Modules, UnitData）");
    }

    private static void EnsureFolder(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = dir.Substring(0, dir.LastIndexOf('/'));
        string name = dir.Substring(dir.LastIndexOf('/') + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    // ===== 地面效果 =====

    private static void CreateGroundEffects()
    {
        CreateAsset(CreateBurnEffect(), AmmoDir + "/GroundEffect_Burn.asset");
        CreateAsset(CreateSlowEffect(), AmmoDir + "/GroundEffect_Slow.asset");
    }

    private static GroundEffectDef CreateBurnEffect()
    {
        var e = ScriptableObject.CreateInstance<GroundEffectDef>();
        e.type = GroundEffectType.Burn;
        e.radiusCells = 3f; e.duration = 5f; e.tickInterval = 1f; e.power = 3f; e.maxTargets = 0;
        return e;
    }

    private static GroundEffectDef CreateSlowEffect()
    {
        var e = ScriptableObject.CreateInstance<GroundEffectDef>();
        e.type = GroundEffectType.Slow;
        e.radiusCells = 2f; e.duration = 5f; e.tickInterval = 0.5f; e.power = 0.5f; e.maxTargets = 0;
        return e;
    }

    // ===== 弹药（3.6 §3.2 弹药表）=====

    private static void CreateAmmo()
    {
        // Arrow 弓手箭：Lv1 穿透，无 AOE，低抛被墙挡
        var arrow = ScriptableObject.CreateInstance<AmmoDef>();
        arrow.ammoType = ProjectileType.Arrow; arrow.pierceLevel = 1;
        arrow.aoeRadiusCells = 0f; arrow.aoeFalloff = 0f;
        arrow.ballisticType = BallisticType.Lob; arrow.arcHeightCells = 0.8f;
        CreateAsset(arrow, AmmoDir + "/Ammo_Arrow.asset");

        // Bolt 手持弩箭：Lv1 穿透，无 AOE，低抛
        var bolt = ScriptableObject.CreateInstance<AmmoDef>();
        bolt.ammoType = ProjectileType.Bolt; bolt.pierceLevel = 1;
        bolt.aoeRadiusCells = 0f; bolt.aoeFalloff = 0f;
        bolt.ballisticType = BallisticType.Lob; bolt.arcHeightCells = 0.8f;
        CreateAsset(bolt, AmmoDir + "/Ammo_Bolt.asset");

        // HeavyBolt 贯穿弩箭（弩炮）：Lv3 穿透，单体高伤，高抛越墙
        var hb = ScriptableObject.CreateInstance<AmmoDef>();
        hb.ammoType = ProjectileType.HeavyBolt; hb.pierceLevel = 3;
        hb.aoeRadiusCells = 0f; hb.aoeFalloff = 0f;
        hb.ballisticType = BallisticType.HighArc; hb.arcHeightCells = 4f;
        CreateAsset(hb, AmmoDir + "/Ammo_HeavyBolt.asset");

        // Stone 投石（投掷机）：Lv3 穿透，单段 AOE 2 格，高抛越墙
        var stone = ScriptableObject.CreateInstance<AmmoDef>();
        stone.ammoType = ProjectileType.Stone; stone.pierceLevel = 3;
        stone.aoeRadiusCells = 2f; stone.aoeFalloff = 0.5f;
        stone.ballisticType = BallisticType.HighArc; stone.arcHeightCells = 4f;
        CreateAsset(stone, AmmoDir + "/Ammo_Stone.asset");

        // Fireball 火弹（投掷机/法师阉割）：Lv1 穿透，大 AOE 灼烧场
        var fire = ScriptableObject.CreateInstance<AmmoDef>();
        fire.ammoType = ProjectileType.Fireball; fire.pierceLevel = 1;
        fire.aoeRadiusCells = 3f; fire.aoeFalloff = 0.5f;
        fire.ballisticType = BallisticType.HighArc; fire.arcHeightCells = 4f;
        fire.effect = CreateBurnEffect();
        CreateAsset(fire, AmmoDir + "/Ammo_Fireball.asset");

        // Magic 魔弹（弩炮/法师阉割）：Lv1 穿透，中 AOE 减速场
        var magic = ScriptableObject.CreateInstance<AmmoDef>();
        magic.ammoType = ProjectileType.Magic; magic.pierceLevel = 1;
        magic.aoeRadiusCells = 2f; magic.aoeFalloff = 0.5f;
        magic.ballisticType = BallisticType.HighArc; magic.arcHeightCells = 4f;
        magic.effect = CreateSlowEffect();
        CreateAsset(magic, AmmoDir + "/Ammo_Magic.asset");
    }

    // ===== 装备（3.6 §4.3 加减修正模型）=====

    private static void CreateEquipment()
    {
        // 重甲（Warrior）：+20血 +10防 -2攻
        var heavy = ScriptableObject.CreateInstance<EquipmentDef>();
        heavy.id = "HeavyArmor"; heavy.compatibleWith = Occupation.Warrior;
        heavy.modifiers.maxHp = 20; heavy.modifiers.defense = 10; heavy.modifiers.attack = -2;
        CreateAsset(heavy, EquipDir + "/Equip_HeavyArmor.asset");

        // 盾（Warrior）：+40血 +25防 -4攻
        var shield = ScriptableObject.CreateInstance<EquipmentDef>();
        shield.id = "Shield"; shield.compatibleWith = Occupation.Warrior;
        shield.modifiers.maxHp = 40; shield.modifiers.defense = 25; shield.modifiers.attack = -4;
        shield.isShield = true;
        CreateAsset(shield, EquipDir + "/Equip_Shield.asset");

        // 弩（Archer）：+6攻 +2防 +3程 +0.9CD
        var crossbow = ScriptableObject.CreateInstance<EquipmentDef>();
        crossbow.id = "Crossbow"; crossbow.compatibleWith = Occupation.Archer;
        crossbow.modifiers.attack = 6; crossbow.modifiers.defense = 2;
        crossbow.modifiers.attackRange = 3f; crossbow.modifiers.attackCD = 0.9f;
        CreateAsset(crossbow, EquipDir + "/Equip_Crossbow.asset");

        // 骑枪（Cavalry 重装骑兵变体）：+30血 +10防（冲锋是技能，不叠攻）
        var lance = ScriptableObject.CreateInstance<EquipmentDef>();
        lance.id = "Lance"; lance.compatibleWith = Occupation.Cavalry;
        lance.modifiers.maxHp = 30; lance.modifiers.defense = 10;
        CreateAsset(lance, EquipDir + "/Equip_Lance.asset");
    }

    // ===== 骑兵（3.6 §4.5 champion 基础 + 韧性 50 + 冲锋）=====

    private static void CreateCavalry()
    {
        var human = ScriptableObject.CreateInstance<NpcProfessionDef>();
        human.name = "Human_Player_Cavalry";
        human.faction = Faction.Human_Player;
        human.occupation = Occupation.Cavalry;
        // 移速（3.6 §六）：平常 walkSpeed 与正常一致，最大 runSpeed 全场最快（追击/冲锋提速）
        human.walkSpeed = 3f; human.runSpeed = 10f;
        human.maxHp = 160; human.attack = 12; human.defense = 15;
        human.attackRange = 1f; human.attackCD = 1f; human.isRanged = false;
        human.perceptionRadius = 8f; human.threatSensitivity = 1f;
        human.courage = 80; human.obedience = 70; human.retreatThresholdOffset = 0.5f;
        human.maxHitCount = 99; human.professionPullScale = 0.2f;
        human.wanderRadiusCells = 2f;
        human.baseToughness = 50f; human.toughnessDefenseScale = 0.2f;
        human.isCavalry = true;
        human.chargeDamage = 80f; human.chargeRangeCells = 4f; human.chargeSpeed = 25f;
        human.chargePairGap = 0.3f; human.chargeGroupCooldown = 20f; human.chargeDamageReduce = 0.7f;
        CreateAsset(human, UnitDir + "/Human_Player_Cavalry.asset");

        var undead = ScriptableObject.CreateInstance<NpcProfessionDef>();
        undead.name = "Undead_Cavalry";
        undead.faction = Faction.Undead;
        undead.occupation = Occupation.Cavalry;
        // 移速（3.6 §六）：平常 walkSpeed 与正常一致，最大 runSpeed 全场最快
        undead.walkSpeed = 3f; undead.runSpeed = 10f;
        undead.maxHp = 150; undead.attack = 13; undead.defense = 14;
        undead.attackRange = 1f; undead.attackCD = 1f; undead.isRanged = false;
        undead.perceptionRadius = 8f; undead.threatSensitivity = 1.2f;
        undead.courage = 80; undead.obedience = 70; undead.retreatThresholdOffset = 0.5f;
        undead.maxHitCount = 99; undead.professionPullScale = 0.2f;
        undead.wanderRadiusCells = 2f;
        undead.baseToughness = 50f; undead.toughnessDefenseScale = 0.2f;
        undead.isCavalry = true;
        undead.chargeDamage = 80f; undead.chargeRangeCells = 4f; undead.chargeSpeed = 25f;
        undead.chargePairGap = 0.3f; undead.chargeGroupCooldown = 20f; undead.chargeDamageReduce = 0.7f;
        CreateAsset(undead, UnitDir + "/Undead_Cavalry.asset");
    }

    // ===== 工事（3.6 §4.6 档位）=====

    private static void CreateFortifications()
    {
        // 城墙 Lv2：减免 40%，挡移动，高 2
        var wall = ScriptableObject.CreateInstance<FortificationDef>();
        wall.name = "Wall"; wall.defenseLevel = 2; wall.meleeDamageReduce = 0.4f;
        wall.blocksMovement = true; wall.heightCells = 2f; wall.maxHp = 1000;
        CreateAsset(wall, FortDir + "/Wall.asset");

        // 城门 Lv1：减免 20%，可通行
        var gate = ScriptableObject.CreateInstance<FortificationDef>();
        gate.name = "Gate"; gate.defenseLevel = 1; gate.meleeDamageReduce = 0.2f;
        gate.blocksMovement = true; gate.passable = true; gate.heightCells = 2f; gate.maxHp = 800;
        CreateAsset(gate, FortDir + "/Gate.asset");

        // 拒马 Lv1：减免 20%，不挡移动（C5 裁决 2026-08-04：拒马=减速带，不硬挡；blocksMovement=false），矮 0.5
        var barricade = ScriptableObject.CreateInstance<FortificationDef>();
        barricade.name = "Barricade"; barricade.defenseLevel = 1; barricade.meleeDamageReduce = 0.2f;
        barricade.blocksMovement = false; barricade.heightCells = 0.5f; barricade.maxHp = 100;
        CreateAsset(barricade, FortDir + "/Barricade.asset");

        // 箭塔 Lv1：弹药 Arrow
        var arrowTower = ScriptableObject.CreateInstance<FortificationDef>();
        arrowTower.name = "ArrowTower"; arrowTower.defenseLevel = 1; arrowTower.meleeDamageReduce = 0.2f;
        arrowTower.heightCells = 3f; arrowTower.maxHp = 400;
        CreateAsset(arrowTower, FortDir + "/ArrowTower.asset");

        // 弩塔 Lv1：弹药 Bolt
        var crossTower = ScriptableObject.CreateInstance<FortificationDef>();
        crossTower.name = "CrossbowTower"; crossTower.defenseLevel = 1; crossTower.meleeDamageReduce = 0.2f;
        crossTower.heightCells = 3f; crossTower.maxHp = 450;
        CreateAsset(crossTower, FortDir + "/CrossbowTower.asset");

        // 法塔 Lv1：弹药 Magic
        var magicTower = ScriptableObject.CreateInstance<FortificationDef>();
        magicTower.name = "MagicTower"; magicTower.defenseLevel = 1; magicTower.meleeDamageReduce = 0.2f;
        magicTower.heightCells = 3f; magicTower.maxHp = 350;
        CreateAsset(magicTower, FortDir + "/MagicTower.asset");
    }

    // ===== 模块树（3.5 §2.1 初始 3 节点）=====

    private static void CreateModules()
    {
        CreateModule("Civil", "土木",
            new string[] { "Wall", "Gate", "ArrowTower", "Castle" }, new string[0],
            new string[] { "Wall", "Gate", "ArrowTower" }, new string[] { "Barricade", "CrossbowTower" },
            new string[] { "Wall", "Gate", "ArrowTower" }, new string[] { "MagicTower" });

        CreateModule("Production", "生产",
            new string[] { "Lumbermill", "Quarry", "Mine", "Farm", "Warehouse", "TrainingGround" }, new string[0],
            new string[] { "Lumbermill", "Quarry", "Mine", "Farm", "Warehouse", "TrainingGround" }, new string[] { "FoodWorkshop", "Ranch" },
            new string[] { "Lumbermill", "Quarry", "Mine", "Farm", "Warehouse" }, new string[] { "AdvancedStorage" });

        CreateModule("Livelihood", "民生",
            new string[] { "Granary", "House" }, new string[0],
            new string[] { "Granary", "House" }, new string[] { "Well" },
            new string[] { "Granary", "House" }, new string[] { "Hospital", "Church" });

        CreateModule("Military", "军事",
            new string[] { "TrainingCamp", "Armory" }, new string[0],
            new string[] { "TrainingCamp", "Armory" }, new string[] { "SiegeWorkshop" },
            new string[] { "TrainingCamp", "Armory" }, new string[] { "Barracks" });

        CreateModule("Commerce", "商业",
            new string[] { "Market" }, new string[0],
            new string[] { "Market" }, new string[] { "Shop" },
            new string[] { "Market" }, new string[] { "GoldMine" });

        CreateModule("Science", "科技",
            new string[] { "Academy", "Workshop" }, new string[0],
            new string[] { "Academy", "Workshop" }, new string[0],
            new string[] { "Academy", "Workshop" }, new string[0]);
    }

    private static void CreateModule(string id, string display,
        string[] t1Upgrade, string[] t1Unlock,
        string[] t2Upgrade, string[] t2Unlock,
        string[] t3Upgrade, string[] t3Unlock)
    {
        var module = ScriptableObject.CreateInstance<ModuleDef>();
        module.name = "Module_" + id;
        module.moduleId = id;
        module.tiers = new[]
        {
            new ModuleTierDef { tier = 1, requiredCastleLevel = 1, upgradeBuildings = t1Upgrade, unlockBuildings = t1Unlock },
            new ModuleTierDef { tier = 2, requiredCastleLevel = 2, upgradeBuildings = t2Upgrade, unlockBuildings = t2Unlock },
            new ModuleTierDef { tier = 3, requiredCastleLevel = 3, upgradeBuildings = t3Upgrade, unlockBuildings = t3Unlock },
        };
        CreateAsset(module, ModuleDir + "/" + module.name + ".asset");
    }

    // ===== 工具 =====

    private static void CreateAsset(ScriptableObject obj, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(obj, path);
        AssetDatabase.SaveAssets();
    }
}
