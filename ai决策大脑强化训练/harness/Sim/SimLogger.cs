// ============================================================================
//  M2 Headless 模拟器 - SimLogger JSONL 事件落盘
//  04_模拟器规格.md §八：JSONL 事件（分析态，训练师读这个）。
//  JSONL 每行一个 JSON 对象，字段与示例格式对齐：
//    {"t":12.3,"ev":"unit_died","id":37,"prof":"Undead_Warrior","x":51.2,"killer":"Human_Archer"}
//    {"t":8.1,"ev":"spectrum","id":12,"from":"FullPower","to":"Cautious","threat":0.62,"safety":0.31}
//    {"t":8.1,"ev":"retreat","id":12,"kind":"tactical","reason":"hitCount>=max"}
//    {"t":5.0,"ev":"formation_intent","gid":2,"intent":"Charge","heat":0.7,"value":0.8}
//    {"t":1.0,"ev":"tick","slotDev":0.42,"alive_h":6,"alive_u":6}   ← 每 tick 采样
//    {"t":0.0,"ev":"attack","id":5,"target":21,"dmg":7,"overkill":false}
//  确定性（04 §七）：InvariantCulture 定长浮点（t F1 / x F1 / slotDev·threat·safety·heat·value F2 /
//  整数直写），保证同 seed 跑两次 JSONL 逐字节一致。
// ============================================================================

using System;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// JSONL 日志器（每局一个文件）。事件由 SimWorld / SimDamage / SimFormation 调用。
/// 手动拼 JSON 字符串（不走 System.Text.Json 序列化，避免 float 位宽非确定输出）。
/// </summary>
public sealed class SimLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public SimLogger(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
    }

    /// <summary>每 tick 采样（槽位偏差均值 + 双方存活数）。</summary>
    public void Tick(float t, float slotDev, int aliveHuman, int aliveUndead)
        => Line($"\"t\":{F1(t)},\"ev\":\"tick\",\"slotDev\":{F2(slotDev)},\"alive_h\":{aliveHuman},\"alive_u\":{aliveUndead}");

    /// <summary>单位死亡。</summary>
    public void UnitDied(float t, SimUnit unit, SimUnit killer)
        => Line($"\"t\":{F1(t)},\"ev\":\"unit_died\",\"id\":{unit.Id},\"prof\":\"{unit.ProfessionName}\",\"x\":{F1(unit.Position.x)},\"killer\":\"{(killer != null ? killer.ProfessionName : "unknown")}\"");

    /// <summary>攻击命中（每次 ApplyDamage 记录；overkill=目标被近战锁定数>1）。</summary>
    public void Attack(float t, SimUnit attacker, SimUnit target, int dmg, bool overkill)
        => Line($"\"t\":{F1(t)},\"ev\":\"attack\",\"id\":{attacker.Id},\"target\":{target.Id},\"dmg\":{dmg},\"overkill\":{(overkill ? "true" : "false")}");

    /// <summary>谱系切换（threat=ThreatFactor 上一帧 raw，safety=SafetyFactor）。</summary>
    public void Spectrum(float t, SimUnit unit, string from, string to, float threat, float safety)
        => Line($"\"t\":{F1(t)},\"ev\":\"spectrum\",\"id\":{unit.Id},\"from\":\"{from}\",\"to\":\"{to}\",\"threat\":{F2(threat)},\"safety\":{F2(safety)}");

    /// <summary>撤退（kind=tactical/strategic，reason=hitCount>=max / threat）。</summary>
    public void Retreat(float t, SimUnit unit, string kind, string reason)
        => Line($"\"t\":{F1(t)},\"ev\":\"retreat\",\"id\":{unit.Id},\"kind\":\"{kind}\",\"reason\":\"{reason}\"");

    /// <summary>编队意图（v0 剧本 SetIntent 触发）。</summary>
    public void FormationIntent(float t, int gid, string intent, float heat, float value)
        => Line($"\"t\":{F1(t)},\"ev\":\"formation_intent\",\"gid\":{gid},\"intent\":\"{intent}\",\"heat\":{F2(heat)},\"value\":{F2(value)}");

    /// <summary>放弃追击（AbandonTaskFactor 升越 abandonThreshold 边沿，行为类指标）。</summary>
    public void AbandonChase(float t, SimUnit unit)
        => Line($"\"t\":{F1(t)},\"ev\":\"abandon_chase\",\"id\":{unit.Id}");

    /// <summary>局开始（首行，含配置快照摘要，分析态标记）。</summary>
    public void RunStart(string scenarioName, int seed, int runIndex, int humanCount, int undeadCount)
        => Line($"\"t\":0.0,\"ev\":\"run_start\",\"scenario\":\"{scenarioName}\",\"seed\":{seed},\"run\":{runIndex},\"human\":{humanCount},\"undead\":{undeadCount}");

    /// <summary>局结束（胜负 + 时长）。</summary>
    public void RunEnd(float t, string winner, int aliveHuman, int aliveUndead)
        => Line($"\"t\":{F1(t)},\"ev\":\"run_end\",\"winner\":\"{winner}\",\"alive_h\":{aliveHuman},\"alive_u\":{aliveUndead}");

    private void Line(string fields)
    {
        _writer.WriteLine("{" + fields + "}");
    }

    private static string F1(float v) => v.ToString("F1", Inv);
    private static string F2(float v) => v.ToString("F2", Inv);

    public void Flush() => _writer.Flush();

    public void Dispose() => _writer.Dispose();
}
