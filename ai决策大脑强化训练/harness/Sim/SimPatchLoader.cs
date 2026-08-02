// ============================================================================
//  M3 Headless 模拟器 - SimPatchLoader 调参补丁加载（区分度注入的唯一入口）
//  06 §M3 区分度验收 / M4 手动调参闭环基础：
//    - tuning_rfdist20.patch.json  （rfDistWeight 0.35 -> 0.20，验收 2）
//    - prof_undead_fast.patch.json （Undead_Warrior 移速 3 -> 6，验收 3 / D7）
//    - prof_no_retreat.patch.json  （Human courage -> 99 + retreatThresholdOffset 上调，D6）
//  设计要点（M3 约束"区分度注入只走 patch 配置，不改 Sim 代码"）：
//    - tuning：反射**部分覆盖** TuningSnapshot（只改 patch 中出现的字段，其余不动）
//    - professions：反射**部分覆盖** ProfessionSnapshot（拷贝 struct 后改 patch 字段再注册回写）
//    - 职业覆盖后同步场景单位：scenario.Units 里 ProfessionName 命中 patch 的 spec
//      重新从 config 取职业快照（patch 在 SimScenario.Load 之后应用，防"单位已拷贝旧快照"失效）
//    - 数值转换按目标字段类型（float/int/bool/float[]/int[]），Int32 显式转换（JSON number 无类型）
//  AI.Core 零改动：patch 只写快照数据，核内公式消费快照。
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 调参补丁加载器（patch JSON -> 部分覆盖 SimConfig 的 tuning + 职业库）。
/// 与 SimScenario.Load 的语义区别：场景 JSON 的 professions/tuning 是"整体替换/注册"，
/// patch 是"部分覆盖"（反射按字段名打点）。
/// </summary>
public static class SimPatchLoader
{
    /// <summary>
    /// 应用补丁。patch 必须在 SimScenario.Load 之后调用（场景职业/世界常量先落位，patch 覆盖其上）。
    /// scenario 用于职业覆盖后同步已解析单位（防单位快照失效）。
    /// </summary>
    public static void Apply(string path, SimConfig config, SimScenarioData scenario)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            throw new InvalidOperationException("[SimPatchLoader] 补丁文件不存在: " + path);

        var doc = JsonSerializer.Deserialize<PatchDoc>(File.ReadAllText(path), CreateOptions());
        if (doc == null)
            throw new InvalidOperationException("[SimPatchLoader] JSON 反序列化失败: " + path);

        // 1. tuning 部分覆盖（反射，boxing 改后写回——TuningSnapshot 是 struct）
        if (doc.tuning != null)
        {
            object boxed = config.tuning;   // box
            foreach (var kv in doc.tuning)
            {
                var field = typeof(TuningSnapshot).GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    throw new InvalidOperationException($"[SimPatchLoader] 未知 tuning 字段 '{kv.Key}'（{path}）");
                field.SetValue(boxed, ConvertValue(field.FieldType, kv.Value));
            }
            config.tuning = (TuningSnapshot)boxed;
        }

        // 2. 职业部分覆盖（拷贝 struct -> 反射改字段 -> 注册回写）
        if (doc.professions != null && doc.professions.Count > 0)
        {
            var overridden = new HashSet<string>();
            foreach (var kv in doc.professions)
            {
                string name = kv.Key;
                ProfessionSnapshot prof = config.GetProfession(name);
                // patch 只覆盖已有职业（存在 = faction 非 None；Default 的 faction=None）
                if (prof.faction == Faction.None)
                    throw new InvalidOperationException($"[SimPatchLoader] 未知职业 '{name}'（{path}）——patch 只覆盖已有职业");

                foreach (var fkv in kv.Value)
                {
                    var field = typeof(ProfessionSnapshot).GetField(fkv.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                        throw new InvalidOperationException($"[SimPatchLoader] 未知职业字段 '{fkv.Key}'（{name}，{path}）");
                    // ProfessionSnapshot 是 struct：ref 拷贝后反射改字段
                    object pbox = prof;
                    field.SetValue(pbox, ConvertValue(field.FieldType, fkv.Value));
                    prof = (ProfessionSnapshot)pbox;
                }
                config.RegisterProfession(name, prof);
                overridden.Add(name);
            }

            // 3. 同步场景单位：patch 在 Load 之后应用，spec 里是旧快照，重新取。
            //    必须保留 spec 已覆盖的 faction（S1 双阵营共用同一职业快照，u.faction 是单位级覆盖，
            //    config 职业库的 faction 是默认值——直接重取会把 Human 侧单位误绑成 Undead 阵营）。
            if (scenario != null && overridden.Count > 0)
            {
                for (int i = 0; i < scenario.Units.Count; i++)
                {
                    var spec = scenario.Units[i];
                    if (!overridden.Contains(spec.ProfessionName)) continue;
                    ProfessionSnapshot merged = config.GetProfession(spec.ProfessionName);
                    merged.faction = spec.Profession.faction;   // 场景 faction 覆盖优先
                    spec.Profession = merged;
                }
            }
        }
    }

    /// <summary>按目标字段类型转换 JSON 值（float/int/bool/float[]/int[]）。</summary>
    private static object ConvertValue(Type fieldType, JsonElement el)
    {
        if (fieldType == typeof(float)) return el.GetSingle();
        if (fieldType == typeof(int)) return el.GetInt32();
        if (fieldType == typeof(bool)) return el.GetBoolean();

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
        throw new InvalidOperationException("[SimPatchLoader] 不支持的字段类型: " + fieldType.FullName);
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
    private sealed class PatchDoc
    {
        public string name;
        public Dictionary<string, JsonElement> tuning;
        public Dictionary<string, Dictionary<string, JsonElement>> professions;
    }
#pragma warning restore CS0649
}
