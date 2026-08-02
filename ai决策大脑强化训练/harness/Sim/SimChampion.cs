// ============================================================================
//  M4 Headless 模拟器 - SimChampion 冠军配置管理（champion/candidate 机制）
//  06 §M4 / 02 §2.1：
//    - 冠军配置单独维护：champion/tuning.champion.json——只有打败现任冠军才替换
//    - Unity 回灌只从 champion 出——训练师中间产物永远不进 Unity
//  05 §四 CLI 契约：benchmark --config champion/... [--patch proposals/...]
//    - champion 文件 = 调参基线全量快照（tuning 全字段 + 职业库全量）
//    - patch 文件 = 候选差异（部分覆盖，SimPatchLoader 反射打点）
//    - 深合并语义：champion（全量基线）→ 场景 Load（考试卷固定）→ patch（候选差异）
//  序列化：
//    - 全量导出：手拼 JSON（字段按 TuningSnapshot/ProfessionSnapshot 声明序 = 确定性）
//    - 全量加载：JsonElement 反射按字段名赋值（对齐 SimPatchLoader.ConvertValue 数值转换）
//  AI.Core 零改动：champion 只写快照数据。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 冠军配置加载/导出（tuning 全量 + 职业库全量）。
/// 与 SimPatchLoader 的区别：champion 是"全量替换"（含清空职业库），patch 是"部分覆盖"。
/// </summary>
public static class SimChampion
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// 全量导出当前配置到 champion JSON（tuning 全字段 + 职业库全量）。
    /// 字段序 = TuningSnapshot/ProfessionSnapshot 声明序（确定性）；职业 key 按 Ordinal 排序。
    /// </summary>
    public static string Export(SimConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"name\": \"champion\",");
        sb.AppendLine("  \"$comment\": \"M4 调参基线全量快照（champion 机制唯一真身）。改参流程：改此处或写 patch 部分覆盖 -> benchmark 跑分 -> verdict 裁决留/弃。Unity 回灌只从本文件出。\",");
        sb.AppendLine("  \"tuning\": {");
        AppendStructFields(sb, typeof(TuningSnapshot), config.tuning, "    ");
        sb.AppendLine("  },");
        sb.AppendLine("  \"professions\": {");

        // 职业库按 key 排序（确定性）
        var names = new List<string>(config.Professions.Keys);
        names.Sort(StringComparer.Ordinal);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            sb.Append("    \"").Append(name).Append("\": {");
            AppendStructFields(sb, typeof(ProfessionSnapshot), config.GetProfession(name), "      ");
            sb.AppendLine(i < names.Count - 1 ? "    }," : "    }");
        }
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// 从 champion JSON 全量加载到 config（tuning 全量替换 + 职业库清空后全量注册）。
    /// 场景 Load 应在之后调用（场景可覆盖 cellSize/自定义职业）；patch 应在场景 Load 之后（部分覆盖）。
    /// </summary>
    public static void Load(string path, SimConfig config)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            throw new InvalidOperationException("[SimChampion] 冠军配置不存在: " + path);

        var doc = JsonSerializer.Deserialize<ChampionDoc>(File.ReadAllText(path), CreateOptions());
        if (doc == null)
            throw new InvalidOperationException("[SimChampion] JSON 反序列化失败: " + path);

        // 1. tuning 全量替换（反射按字段名赋值，字段缺省 = 保持默认？不：champion 是全量快照，缺字段=0/null 须报错防静默）
        if (doc.tuning != null)
        {
            object boxed = config.tuning;
            ApplyStructAll(typeof(TuningSnapshot), boxed, doc.tuning, path);
            config.tuning = (TuningSnapshot)boxed;
        }

        // 2. 职业库清空后全量注册（champion 是真身：旧默认职业被替换）
        //    ProfessionSnapshot 是 struct：装箱后反射改字段，再 unbox 写回（SimPatchLoader 同款模式）
        if (doc.professions != null && doc.professions.Count > 0)
        {
            foreach (var kv in doc.professions)
            {
                object boxed = new ProfessionSnapshot();
                ApplyStructAll(typeof(ProfessionSnapshot), boxed, kv.Value, path);
                config.RegisterProfession(kv.Key, (ProfessionSnapshot)boxed);
            }
        }
    }

    /// <summary>champion 配置已存在？</summary>
    public static bool Exists(string path) => File.Exists(path);

    // ===== 反射应用：全量（缺字段报错，防静默丢参）=====

    private static void ApplyStructAll(Type type, object instance, Dictionary<string, JsonElement> fields, string path)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var fi in fieldInfos)
        {
            if (!fields.TryGetValue(fi.Name, out var el))
                throw new InvalidOperationException($"[SimChampion] 冠军配置缺少字段 '{fi.Name}'（{type.Name}，{path}）——champion 是全量快照，不许缺字段");
            fi.SetValue(instance, ConvertValue(fi.FieldType, el));
        }
    }

    // ===== 手拼 JSON：struct 全字段（声明序）=====

    /// <summary>struct 全字段手拼（字段序 = 声明序，确定性；值类型含 float/int/bool/Faction/数组）。</summary>
    private static void AppendStructFields(StringBuilder sb, Type type, object instance, string indent)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fi = fieldInfos[i];
            if (i > 0) sb.AppendLine(",");
            sb.Append(indent).Append('"').Append(fi.Name).Append("\": ");
            AppendValue(sb, fi.GetValue(instance));
        }
        sb.AppendLine();
    }

    private static void AppendValue(StringBuilder sb, object value)
    {
        if (value is float f) { sb.Append(F(f)); return; }
        if (value is int i) { sb.Append(i); return; }
        if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
        if (value is float[] fa) { sb.Append("["); for (int k = 0; k < fa.Length; k++) { if (k > 0) sb.Append(", "); sb.Append(F(fa[k])); } sb.Append("]"); return; }
        if (value is int[] ia) { sb.Append("["); for (int k = 0; k < ia.Length; k++) { if (k > 0) sb.Append(", "); sb.Append(ia[k]); } sb.Append("]"); return; }
        if (value is Faction fac) { sb.Append('"').Append(fac).Append('"'); return; }
        throw new InvalidOperationException("[SimChampion] 不支持导出的字段类型: " + value.GetType().FullName);
    }

    /// <summary>按目标字段类型转换 JSON 值（float/int/bool/float[]/int[]/Faction）。</summary>
    private static object ConvertValue(Type fieldType, JsonElement el)
    {
        if (fieldType == typeof(float)) return el.GetSingle();
        if (fieldType == typeof(int)) return el.GetInt32();
        if (fieldType == typeof(bool)) return el.GetBoolean();
        if (fieldType == typeof(Faction)) return ParseFaction(el.GetString());

        if (fieldType == typeof(float[]))
        {
            var list = new List<float>();
            foreach (var item in el.EnumerateArray()) list.Add(item.GetSingle());
            return list.ToArray();
        }
        if (fieldType == typeof(int[]))
        {
            var list = new List<int>();
            foreach (var item in el.EnumerateArray()) list.Add(item.GetInt32());
            return list.ToArray();
        }
        throw new InvalidOperationException("[SimChampion] 不支持的字段类型: " + fieldType.FullName);
    }

    private static Faction ParseFaction(string s)
    {
        switch (s)
        {
            case "None": return Faction.None;
            case "Human_Player": return Faction.Human_Player;
            case "Undead": return Faction.Undead;
            default:
                throw new InvalidOperationException("[SimChampion] 未知阵营 '" + s + "'");
        }
    }

    private static string F(float v)
    {
        // 定长 6 位小数（JSON 全量快照精确到 1e-6，足够 2.26/cellSize 等常量），InvariantCulture 确定性
        return v.ToString("0.000000", Inv);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    // ===== JSON DTO =====
#pragma warning disable CS0649
    private sealed class ChampionDoc
    {
        public string name;
        public Dictionary<string, JsonElement> tuning;
        public Dictionary<string, Dictionary<string, JsonElement>> professions;
    }
#pragma warning restore CS0649
}
