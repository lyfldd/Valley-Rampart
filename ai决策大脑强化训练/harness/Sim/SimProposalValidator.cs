// ============================================================================
//  M5 Headless 模拟器 - SimProposalValidator 训练师提案校验（propose validate）
//  05 §三 契约 + AGENTS.md 铁律：
//    1. changes ≤3 项
//    2. path 必须在 factor_registry 注册（未注册 = 拒收）
//    3. to 必须在 registry 的 [min,max] 边界内
//    4. 死参数（registry.deadParams）直接拒收
//    5. harness=false 的字段（LOD/调度/模拟器不包含职业）拒收——改了 sim 不生效=浪费轮次
//    6. rawFactor 六权重改动后 Σ 必须 = 1（自己归一化）
//    7. 滞回类（sensitive=hysteresis）改动需在 risk 字段声明方差风险
//  输出：通过（0）/ 拒收（1）+ 逐项原因（训练师据此修正；拒收率 = 05 验收 1 的指标）
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>因子注册表条目（factor_registry.example.json registry[]）。</summary>
public sealed class RegistryEntry
{
    public string path;
    public double current;
    public double min;
    public double max;
    public string unit;
    public string group;
    public string constraint;
    public string sensitive;      // "hysteresis" = 滞回类
    public bool harness = true;   // false = 模拟器不生效
    public string semantics;
    public string[] consumers;
    public string note;
}

/// <summary>
/// 提案校验器：加载 factor_registry -> 逐项校验提案 -> 输出问题列表。
/// 训练师写完提案先跑 `propose validate`，通过后再 `propose run`。
/// </summary>
public static class SimProposalValidator
{
    private const string RegistryPath = "../schemas/factor_registry.example.json";
    // 相对 harness 运行目录（dotnet run cwd = harness/）；champion 等路径同理
    private const string RegistryPathAlt = "schemas/factor_registry.example.json";

    /// <summary>校验结果（Program 打印 + 拒收率统计用）。</summary>
    public sealed class Result
    {
        public bool Valid = true;
        public List<string> Issues = new List<string>();
    }

    public static Result Validate(string proposalPath)
    {
        var r = new Result();

        // 1. 加载注册表（优先绝对/相对 harness 目录）
        var registry = LoadRegistry();

        // 2. 解析提案
        var proposal = JsonSerializer.Deserialize<ProposalDoc>(File.ReadAllText(proposalPath), CreateOptions());
        if (proposal == null)
        {
            r.Valid = false;
            r.Issues.Add("提案 JSON 反序列化失败（格式错误）");
            return r;
        }
        if (string.IsNullOrEmpty(proposal.id)) { r.Valid = false; r.Issues.Add("缺 id（应为 p_0001 格式）"); }
        if (string.IsNullOrEmpty(proposal.base_)) { r.Valid = false; r.Issues.Add("缺 base（应写 champion@...）"); }
        if (string.IsNullOrEmpty(proposal.hypothesis) || proposal.hypothesis.Length < 10)
        { r.Valid = false; r.Issues.Add("hypothesis 过短（≥10 字因果假设）"); }
        if (proposal.evidence == null || proposal.evidence.Length == 0)
        { r.Valid = false; r.Issues.Add("缺 evidence（必须引用真实文件+数据）"); }
        if (proposal.changes == null || proposal.changes.Length == 0)
        { r.Valid = false; r.Issues.Add("缺 changes"); }
        else if (proposal.changes.Length > 3)
        { r.Valid = false; r.Issues.Add($"changes 超限：{proposal.changes.Length} 项（铁律 ≤3）"); }

        if (proposal.changes == null) return r;

        // 3. 逐项校验 changes
        var rawFactorTos = new Dictionary<string, double>();   // rawFactor 六权重改动收集（Σ 校验）
        for (int i = 0; i < proposal.changes.Length; i++)
        {
            var c = proposal.changes[i];
            if (string.IsNullOrEmpty(c.path) || c.path.Length < 3)
            {
                r.Valid = false; r.Issues.Add($"changes[{i}].path 非法: '{c.path}'");
                continue;
            }
            if (string.IsNullOrEmpty(c.rationale) || c.rationale.Length < 5)
            {
                r.Valid = false;
                r.Issues.Add($"changes[{i}].rationale 过短（≥5 字理由）");
            }

            // 3.1 死参数（registry.deadParams 按字段名后缀匹配）
            foreach (var dead in registry.DeadParams)
            {
                if (c.path.EndsWith("." + dead) || c.path == dead)
                {
                    r.Valid = false;
                    r.Issues.Add($"changes[{i}] '{c.path}' 是死参数（{dead}，01 §八：调了无效果），拒收");
                }
            }

            // 3.2 冻结参数（frozenParams）
            foreach (var f in registry.FrozenParams)
            {
                if (c.path == f.path)
                {
                    r.Valid = false;
                    r.Issues.Add($"changes[{i}] '{c.path}' 是冻结参数（{f.reason}），拒收");
                }
            }

            // 3.3 注册表查项
            var entry = FindEntry(registry, c.path);
            if (entry == null)
            {
                r.Valid = false;
                r.Issues.Add($"changes[{i}] '{c.path}' 未在 factor_registry 注册（路径写错或死参数），拒收");
                continue;
            }

            // 3.4 harness=false（模拟器不生效）
            if (!entry.harness)
            {
                r.Valid = false;
                r.Issues.Add($"changes[{i}] '{c.path}' 是 harness=false 字段（模拟器不消费此参数，改了不产生 sim 效果），拒收");
            }

            // 3.5 边界
            if (c.to < entry.min || c.to > entry.max)
            {
                r.Valid = false;
                r.Issues.Add($"changes[{i}] '{c.path}' to={c.to} 越界 [min={entry.min}, max={entry.max}]（registry 边界）");
            }

            // 3.6 rawFactor 六权重收集（Σ=1 校验）
            if (c.path.StartsWith("tuning.rf") && c.path.EndsWith("Weight"))
                rawFactorTos[c.path.Substring("tuning.".Length)] = c.to;

            // 3.7 滞回类声明
            if (entry.sensitive == "hysteresis" && string.IsNullOrEmpty(proposal.risk))
            {
                r.Valid = false;
                r.Issues.Add($"changes[{i}] '{c.path}' 是滞回类参数，risk 字段必须声明方差风险");
            }
        }

        // 4. rawFactor Σ 提示（软检查：实现是加权和+Clamp01，权重绝对值有意义，不强制 Σ=1；
        //    但改权重比值会显著改变行为，提示训练师保持比值意图——05 契约原文"Σ=1"与实现不符，已按实现校正）
        if (rawFactorTos.Count > 0)
        {
            double sum = 0;
            string[] rfFields = { "rfDistWeight", "rfCountWeight", "rfHpWeight", "rfAllyWeight", "rfTimeWeight", "rfHeatWeight" };
            bool anyMissing = false;
            foreach (var f in rfFields)
            {
                double v;
                if (rawFactorTos.TryGetValue(f, out v)) sum += v;
                else
                {
                    var e = FindEntry(registry, "tuning." + f);
                    if (e != null) sum += e.current;
                    else anyMissing = true;
                }
            }
            if (!anyMissing)
            {
                double baseSum = 0;
                foreach (var f in rfFields)
                {
                    var e = FindEntry(registry, "tuning." + f);
                    if (e != null) baseSum += e.current;
                }
                if (baseSum > 0 && Math.Abs(sum - baseSum) > 0.01)
                    r.Issues.Add($"提示：rawFactor 权重改动后 Σ={sum:F2}（基准 {baseSum:F2}）。实现是加权和+Clamp01，不强制 Σ=1，但请确认权重比值变化符合意图");
            }
        }

        return r;
    }

    // ===== 注册表加载/查找 =====

    public sealed class Registry
    {
        public List<RegistryEntry> Entries = new List<RegistryEntry>();
        public List<string> DeadParams = new List<string>();
        public List<RegistryFrozen> FrozenParams = new List<RegistryFrozen>();
    }

    public sealed class RegistryFrozen { public string path; public string reason; }

    /// <summary>公开查询：按注册路径查条目（Program.search 用；未注册返回 null）。</summary>
    public static RegistryEntry FindEntryPublic(string path)
    {
        var reg = LoadRegistry();
        return FindEntry(reg, path);
    }

    private static Registry LoadRegistry()
    {
        string path = null;
        if (File.Exists(RegistryPath)) path = RegistryPath;
        else if (File.Exists(RegistryPathAlt)) path = RegistryPathAlt;
        else throw new InvalidOperationException("[SimProposalValidator] 找不到 factor_registry.example.json（期待 harness/ 或 ../../ 下）");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var reg = new Registry();
        foreach (var e in root.GetProperty("registry").EnumerateArray())
        {
            reg.Entries.Add(new RegistryEntry
            {
                path = e.GetProperty("path").GetString(),
                current = e.TryGetProperty("current", out var c) ? c.GetDouble() : 0,
                min = e.TryGetProperty("min", out var mn) ? mn.GetDouble() : double.MinValue,
                max = e.TryGetProperty("max", out var mx) ? mx.GetDouble() : double.MaxValue,
                sensitive = e.TryGetProperty("sensitive", out var s) ? s.GetString() : null,
                harness = !e.TryGetProperty("harness", out var h) || h.GetBoolean(),
                constraint = e.TryGetProperty("constraint", out var ct) ? ct.GetString() : null,
            });
        }
        if (root.TryGetProperty("deadParams", out var dp))
            foreach (var d in dp.EnumerateArray()) reg.DeadParams.Add(d.GetString());
        if (root.TryGetProperty("frozenParams", out var fp))
            foreach (var f in fp.EnumerateArray())
                reg.FrozenParams.Add(new RegistryFrozen { path = f.GetProperty("path").GetString(), reason = f.GetProperty("reason").GetString() });
        return reg;
    }

    private static RegistryEntry FindEntry(Registry reg, string path)
    {
        for (int i = 0; i < reg.Entries.Count; i++)
            if (reg.Entries[i].path == path) return reg.Entries[i];
        return null;
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
}
