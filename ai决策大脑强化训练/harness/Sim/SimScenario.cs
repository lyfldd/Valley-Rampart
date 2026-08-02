// ============================================================================
//  M2 Headless 模拟器 - SimScenario 场景 JSON 加载
//  04_模拟器规格.md §六：每个剧本 JSON：{units:[{profession,faction,x,intent脚本}], seed, maxDuration, winCondition}。
//  解析为强类型 SimScenarioData（职业名查 SimConfig 职业库 + faction 可覆盖，生成单位规格）。
//  场景文件放在 harness/Scenarios/，参考 s1_plains_symmetric.json / s2_archer_harass.json。
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 槽位角色约束（对应壳 FormationEnums.cs 的 SlotRole——该类型未迁入 AI.Core，Sim 侧定义）。
/// 值序与壳一致：Any=0 / MeleeOnly=1 / RangedOnly=2 / GeneralOnly=3（对齐 DefenseFormation.asset 的 role 字段）。
/// </summary>
public enum SlotRole
{
    Any,
    MeleeOnly,
    RangedOnly,
    GeneralOnly,
}

/// <summary>场景数据（解析产物，SimWorld 消费）。</summary>
public sealed class SimScenarioData
{
    public string Name;
    public int Seed;
    public float MaxDuration;
    public List<SimUnitSpec> Units = new List<SimUnitSpec>();
    public List<SimFormationData> Formations = new List<SimFormationData>();
}

/// <summary>单位规格（职业快照已解析，faction 覆盖已应用）。</summary>
public sealed class SimUnitSpec
{
    public int Id;
    public string ProfessionName;        // 日志用（prof 字段）
    public ProfessionSnapshot Profession; // faction 已按场景覆盖
    public float X;                       // 世界坐标（开战布阵后 SimWorld 再叠加抖动）
    public float HomeX;                   // 归巢点 x（未填 = X）
    public int FormationGid = -1;         // 所属编队 gid，-1=无编队
    public bool IsGeneral;                // 是否编队将军（锚点）
}

/// <summary>编队数据（槽位/意图脚本已强类型化）。</summary>
public sealed class SimFormationData
{
    public int Gid;
    public Faction Faction;
    public int GeneralUnitId = -1;
    public int Direction = 1;                       // 阵型朝向：1=右/-1=左（FormationController._formationDirection）
    public TacticIntent DefaultIntent = TacticIntent.Defense;   // 初始意图（不经过 SetIntent 防抖）
    public SimSlotData[] Slots = new SimSlotData[0];
    public SimIntentEventData[] IntentScript = new SimIntentEventData[0];
}

/// <summary>编队槽位（角色约束 + cell 偏移，对应 FormationDef.SlotDef）。</summary>
public sealed class SimSlotData
{
    public int X;
    public int Y;
    public SlotRole Role;
}

/// <summary>意图脚本事件（t=时刻，SetIntent(intent)）。</summary>
public sealed class SimIntentEventData
{
    public float T;
    public TacticIntent Intent;
}

/// <summary>
/// 场景加载器：读 JSON -> 应用 cellSize/职业库/tuning 覆盖到 SimConfig -> 解析单位/编队。
/// </summary>
public static class SimScenario
{
    /// <summary>加载场景并应用到 config（覆盖 cellSize/职业/tuning），返回强类型数据。</summary>
    public static SimScenarioData Load(string path, SimConfig config)
    {
        var options = CreateOptions();
        string json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<SimScenarioDoc>(json, options);
        if (doc == null)
            throw new InvalidOperationException("[SimScenario] JSON 反序列化失败: " + path);

        // 1. 世界常量覆盖
        if (doc.cellSize > 0d) config.cellSize = (float)doc.cellSize;

        // 2. 职业库覆盖/增补
        if (doc.professions != null)
        {
            foreach (var kv in doc.professions)
                config.RegisterProfession(kv.Key, kv.Value);
        }

        // 3. 调参覆盖（可选）
        if (doc.tuning.HasValue)
            config.tuning = doc.tuning.Value;

        // 4. 解析单位规格
        var data = new SimScenarioData
        {
            Name = doc.name ?? Path.GetFileNameWithoutExtension(path),
            Seed = doc.seed,
            MaxDuration = doc.maxDuration > 0d ? (float)doc.maxDuration : 60f,
        };

        if (doc.units != null)
        {
            foreach (var u in doc.units)
            {
                var prof = config.GetProfession(u.profession);
                if (prof.faction == Faction.None && !string.IsNullOrEmpty(u.profession))
                    throw new InvalidOperationException($"[SimScenario] 未知职业 '{u.profession}'（场景 {data.Name}）");

                // faction 覆盖（决策点 1：S1 双阵营同一职业快照——真镜像只测 sim 阵营 bug）
                if (!string.IsNullOrEmpty(u.faction))
                {
                    var overrideFaction = ParseFaction(u.faction);
                    // ProfessionSnapshot 是 struct，拷贝后覆写 faction
                    prof.faction = overrideFaction;
                }

                data.Units.Add(new SimUnitSpec
                {
                    Id = u.id,
                    ProfessionName = u.profession,
                    Profession = prof,
                    X = (float)u.x,
                    HomeX = u.homeX.HasValue ? (float)u.homeX.Value : (float)u.x,
                    FormationGid = u.formationGid,
                    IsGeneral = u.isGeneral,
                });
            }
        }

        // 5. 解析编队
        if (doc.formations != null)
        {
            foreach (var f in doc.formations)
            {
                var formation = new SimFormationData
                {
                    Gid = f.gid,
                    Faction = ParseFaction(f.faction),
                    GeneralUnitId = f.generalUnitId,
                    Direction = f.direction != 0 ? f.direction : 1,
                    DefaultIntent = !string.IsNullOrEmpty(f.defaultIntent) ? ParseIntent(f.defaultIntent) : TacticIntent.Defense,
                };
                var slots = new List<SimSlotData>();
                if (f.slots != null)
                {
                    foreach (var s in f.slots)
                        slots.Add(new SimSlotData { X = s.x, Y = s.y, Role = ParseSlotRole(s.role) });
                }
                formation.Slots = slots.ToArray();

                var script = new List<SimIntentEventData>();
                if (f.intentScript != null)
                {
                    foreach (var evt in f.intentScript)
                        script.Add(new SimIntentEventData { T = (float)evt.t, Intent = ParseIntent(evt.intent) });
                }
                formation.IntentScript = script.ToArray();

                data.Formations.Add(formation);
            }
        }

        return data;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,   // ProfessionSnapshot/TuningSnapshot 是 public 字段结构
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static Faction ParseFaction(string s)
    {
        switch (s)
        {
            case "None": return Faction.None;
            case "Human_Player": return Faction.Human_Player;
            case "Undead": return Faction.Undead;
            default:
                throw new InvalidOperationException("[SimScenario] 未知阵营 '" + s + "'");
        }
    }

    private static SlotRole ParseSlotRole(string s)
    {
        switch (s)
        {
            case "Any": return SlotRole.Any;
            case "MeleeOnly": return SlotRole.MeleeOnly;
            case "RangedOnly": return SlotRole.RangedOnly;
            case "GeneralOnly": return SlotRole.GeneralOnly;
            default:
                throw new InvalidOperationException("[SimScenario] 未知槽位角色 '" + s + "'");
        }
    }

    private static TacticIntent ParseIntent(string s)
    {
        switch (s)
        {
            case "Defense": return TacticIntent.Defense;
            case "Charge": return TacticIntent.Charge;
            case "Retreat": return TacticIntent.Retreat;
            default:
                throw new InvalidOperationException("[SimScenario] 未知战术意图 '" + s + "'");
        }
    }

    // ===== JSON DTO（与 04 §六 剧本结构对应）=====
    // 字段由 System.Text.Json 反射赋值（IncludeFields），编译器 CS0649 警告为误报。

#pragma warning disable CS0649
    private sealed class SimScenarioDoc
    {
        public string name;
        public int seed;
        public double maxDuration;
        public double cellSize;
        public List<SimUnitDoc> units;
        public List<SimFormationDoc> formations;
        public Dictionary<string, ProfessionSnapshot> professions;
        public TuningSnapshot? tuning;
    }

    private sealed class SimUnitDoc
    {
        public int id;
        public string profession;
        public string faction;
        public double x;
        public double? homeX;
        public int formationGid = -1;
        public bool isGeneral;
    }

    private sealed class SimFormationDoc
    {
        public int gid;
        public string faction;
        public int generalUnitId = -1;
        public int direction = 1;
        public string defaultIntent;
        public List<SimSlotDoc> slots;
        public List<SimIntentDoc> intentScript;
    }

    private sealed class SimSlotDoc
    {
        public int x;
        public int y;
        public string role;
    }

    private sealed class SimIntentDoc
    {
        public double t;
        public string intent;
    }
#pragma warning restore CS0649
}
