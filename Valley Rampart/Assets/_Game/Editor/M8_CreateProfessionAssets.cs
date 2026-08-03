using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// M8 军事新增：批量创建新职业/机器/工事的 NpcProfessionDef 资产（数值=09 文档 §1 与 sim SimConfig 机械一致）。
/// 菜单：ValleyRampart/M8/Create Profession Assets
/// 已存在资产跳过（不覆盖现有 4 职业/将军/工人）。
/// </summary>
public static class M8CreateProfessionAssets
{
    private sealed class Spec
    {
        public string name; public Faction faction;
        public float walk, run; public int hp, atk, def;
        public float range, cd; public bool ranged; public float proj;
        public float perc, ts; public int courage, obedience; public float retreatOff;
        public bool isStatic;
        public Spec(string n, Faction f, float w, float r, int hp_, int a, int d,
                    float rg, float cd_, bool rr, float pj, float pc, float t,
                    int c, int o, float ro, bool st = false)
        {
            name = n; faction = f; walk = w; run = r; hp = hp_; atk = a; def = d;
            range = rg; cd = cd_; ranged = rr; proj = pj; perc = pc; ts = t;
            courage = c; obedience = o; retreatOff = ro; isStatic = st;
        }
    }

    [MenuItem("ValleyRampart/M8/Create Profession Assets")]
    public static void CreateAll()
    {
        var specs = new List<Spec>
        {
            // 人类新职业
            new Spec("Human_Player_Mage", Faction.Human_Player, 2.8f, 5.6f, 50, 14, 10, 6f, 1.8f, true, 20f, 8f, 1.1f, 40, 60, 0.3f),
            new Spec("Human_Player_Healer", Faction.Human_Player, 3f, 6f, 70, 4, 12, 5f, 1.5f, true, 20f, 8f, 0.8f, 55, 70, 0.2f),
            new Spec("Human_Player_ShieldGuard", Faction.Human_Player, 2.5f, 5f, 150, 7, 40, 1f, 1.2f, false, 0f, 7f, 0.9f, 80, 75, 0.6f),
            new Spec("Human_Player_Archmage", Faction.Human_Player, 2.8f, 5.6f, 55, 10, 10, 7f, 2f, true, 20f, 8f, 1.2f, 40, 60, 0.3f),
            new Spec("Human_Player_Bishop", Faction.Human_Player, 3f, 6f, 80, 3, 15, 6f, 2f, true, 20f, 8f, 0.7f, 60, 75, 0.2f),
            new Spec("Human_Player_HeavyWarrior", Faction.Human_Player, 2.6f, 5.2f, 130, 9, 30, 1f, 1.2f, false, 0f, 7f, 1f, 85, 70, 0.5f),
            new Spec("Human_Player_Crossbowman", Faction.Human_Player, 2.8f, 5.6f, 70, 16, 12, 8f, 2.5f, true, 20f, 9f, 1f, 50, 60, 0.3f),
            // 亡灵对称新职业
            new Spec("Undead_Mage", Faction.Undead, 2.8f, 5.6f, 45, 15, 8, 6f, 1.8f, true, 20f, 8f, 1.2f, 40, 60, 0.3f),
            new Spec("Undead_Healer", Faction.Undead, 3f, 6f, 65, 5, 10, 5f, 1.5f, true, 20f, 8f, 0.9f, 55, 70, 0.2f),
            new Spec("Undead_ShieldGuard", Faction.Undead, 2.5f, 5f, 145, 8, 38, 1f, 1.2f, false, 0f, 7f, 1f, 80, 75, 0.6f),
            new Spec("Undead_Archmage", Faction.Undead, 2.8f, 5.6f, 50, 11, 8, 7f, 2f, true, 20f, 8f, 1.3f, 40, 60, 0.3f),
            new Spec("Undead_Bishop", Faction.Undead, 3f, 6f, 75, 4, 13, 6f, 2f, true, 20f, 8f, 0.8f, 60, 75, 0.2f),
            new Spec("Undead_HeavyWarrior", Faction.Undead, 2.6f, 5.2f, 125, 10, 28, 1f, 1.2f, false, 0f, 7f, 1.1f, 85, 70, 0.5f),
            new Spec("Undead_Crossbowman", Faction.Undead, 2.8f, 5.6f, 65, 17, 10, 8f, 2.5f, true, 20f, 9f, 1.1f, 50, 60, 0.3f),
            // 战争机器（单位化；Ballista isStatic）
            new Spec("SiegeMachine", Faction.Human_Player, 1.2f, 2.4f, 200, 18, 20, 10f, 3f, true, 25f, 10f, 0.5f, 99, 80, 0.5f),
            new Spec("Ballista", Faction.Human_Player, 0f, 0f, 150, 28, 20, 14f, 4f, true, 25f, 12f, 0.5f, 99, 80, 0.5f, true),
            // 防御工事（isStatic）
            new Spec("ArrowTower", Faction.Human_Player, 0f, 0f, 400, 8, 15, 7f, 1.5f, true, 20f, 10f, 0.5f, 99, 99, 0.5f, true),
            new Spec("CrossbowTower", Faction.Human_Player, 0f, 0f, 450, 16, 15, 10f, 2.5f, true, 25f, 12f, 0.5f, 99, 99, 0.5f, true),
            new Spec("MagicTower", Faction.Human_Player, 0f, 0f, 350, 12, 15, 8f, 2f, true, 20f, 10f, 0.5f, 99, 99, 0.5f, true),
            new Spec("Barricade", Faction.Human_Player, 0f, 0f, 100, 0, 20, 0f, 0f, false, 0f, 0f, 0.5f, 99, 99, 0.5f, true),
            new Spec("Wall", Faction.Human_Player, 0f, 0f, 1000, 0, 25, 0f, 0f, false, 0f, 0f, 0.5f, 99, 99, 0.5f, true),
            new Spec("Gate", Faction.Human_Player, 0f, 0f, 800, 0, 25, 0f, 0f, false, 0f, 0f, 0.5f, 99, 99, 0.5f, true),
        };

        int created = 0, skipped = 0;
        foreach (var s in specs)
        {
            string path = "Assets/Resources/UnitData/" + s.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<NpcProfessionDef>(path) != null) { skipped++; continue; }
            var a = ScriptableObject.CreateInstance<NpcProfessionDef>();
            a.name = s.name;
            a.faction = s.faction;
            a.walkSpeed = s.walk; a.runSpeed = s.run;
            a.maxHp = s.hp; a.attack = s.atk; a.defense = s.def;
            a.attackRange = s.range; a.attackCD = s.cd;
            a.isRanged = s.ranged; a.projectileSpeed = s.proj;
            a.perceptionRadius = s.perc; a.threatSensitivity = s.ts;
            a.courage = s.courage; a.obedience = s.obedience;
            a.retreatThresholdOffset = s.retreatOff;
            a.maxHitCount = 99; a.professionPullScale = 0.2f;
            a.equipmentSlotCount = 0; a.wanderRadiusCells = s.isStatic ? 0f : 2f;
            a.isStatic = s.isStatic;
            AssetDatabase.CreateAsset(a, path);
            created++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[M8] created {created} profession assets, skipped {skipped}");
    }
}
