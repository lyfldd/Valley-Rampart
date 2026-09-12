using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · 聚合入口（ChainAuditMenu · Editor-only）
//  规格真源：HH.240 任务书 T3/T9/T10 ＋ 断链校验台_设计稿 §二/§五/§六
//
//  菜单：Valley/审计/ChainAudit
//    · 运行全量（R3+R4+R5+R6 → 幂等报告 ＋ 分级判定 ＋ 三角闭合）
//    · 运行静态层（不含探针；同"运行全量"，探针矩阵在场时自动并入）
//    · 打开报告目录
//  数据流（设计稿 §六）：改代码/SO → 菜单 → {R3,R4,R5,R6} → 合并违规 → 分级判定 → 幂等报告
//                       跑局（正门）→ ChainProbeFacade → 能力观测矩阵（补"观测"角）→ 合并=三角闭合
//
//  运行时机（用户定案）：菜单手动 ＋ 交付/验收强制；本批**不做** AssetPostprocessor 自动跑。
//  接口纪律：本器**只读**声明面，只列报不修、不回写（防双源 L-15）。
// ============================================================================
public static class ChainAuditMenu
{
    private const string Tag = "ChainAudit";
    public const string FullMenu = ChainAuditCore.MenuRoot + "/运行全量（R3+R4+R5+R6）";
    public const string StaticMenu = ChainAuditCore.MenuRoot + "/运行静态层";

    [MenuItem(FullMenu, priority = 100)]
    public static void RunFromMenu() => Run();

    [MenuItem(StaticMenu, priority = 101)]
    public static void RunStaticFromMenu() => Run();

    [MenuItem(ChainAuditCore.MenuRoot + "/打开报告目录", priority = 200)]
    public static void OpenReportDir()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), ChainAuditCore.ReportRoot);
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }

    /// <summary>核心入口（菜单/MCP 共用）。</summary>
    public static void Run()
    {
        string dataPath = Application.dataPath;
        var sb = new StringBuilder();

        var r3 = R3_SupplyChain.Run(dataPath);
        var r4 = R4_ActionReach.Run(dataPath);
        var r5 = R5_SixStage.Run(dataPath);
        var r6 = R6_DocDrift.Run(dataPath);

        var all = new List<ChainAuditCore.Violation>();
        all.AddRange(r3.Violations); all.AddRange(r4.Violations);
        all.AddRange(r5.Violations); all.AddRange(r6.Violations);
        ChainAuditCore.SortViolations(all);

        // 三角闭合：行为层探针矩阵在场时并入（观测角）
        var probe = ChainProbeFacade.ReadMatrixArtifact();
        bool probePresent = probe != null && probe.HasMatrix;
        int probeRed = probePresent ? probe.RedCount : 0;

        int red = ChainAuditCore.CountLevel(all, ChainAuditCore.Severity.Red);
        int yellow = ChainAuditCore.CountLevel(all, ChainAuditCore.Severity.Yellow);
        int exempt = R3_SupplyChain.ExemptCount + R4_ActionReach.ExemptCount + R5_SixStage.ExemptCount + R6_DocDrift.ExemptCount;

        // ── 报告头（幂等：不含时间戳）────────────────────────────────────────
        sb.AppendLine("# ChainAudit 报告（HH.240 断链校验台 · 静态层 R3/R4/R5/R6）");
        sb.AppendLine("# 立论：断链＝「声明↔实现↔观测」三角缺角；可达性只能由行为层证明（设计稿 §一）");
        sb.AppendLine("# 反射域=" + ChainAuditCore.ProductionAssemblyName + "（Editor 程序集不入扫描，防自噪）");
        sb.AppendLine("# 稳定报告不含时间戳（幂等：同库重复运行逐字节一致）");
        sb.AppendLine("# 接口纪律：只读声明面，只列报不修、不回写（防双源 L-15）；豁免表变更须策划端确认（D561）");
        sb.AppendLine();

        ChainAuditSpec.AppendReport(sb);
        R3_SupplyChain.AppendReport(sb, r3);
        R4_ActionReach.AppendReport(sb, r4);
        R5_SixStage.AppendReport(sb, r5);
        R6_DocDrift.AppendReport(sb, r6);

        // ── 探针矩阵（行为层·观测角；独立产物在场时并入）────────────────────
        sb.AppendLine("===== ChainProbeFacade 能力观测矩阵（行为层·观测角）=====");
        if (probePresent)
        {
            sb.AppendLine("  矩阵产物=" + probe.Path + "（在场；以下为只读并入）");
            foreach (var l in probe.Lines) sb.AppendLine("  " + l);
        }
        else
        {
            sb.AppendLine("  矩阵产物缺席（Logs/ChainAudit/chain_probe_matrix.txt 无）");
            sb.AppendLine("  ⇒ 观测角缺席：可达性不可证（设计稿 §一：静态永远证不了\"永不选中\"）");
            sb.AppendLine("  ⇒ 运行 `Valley/审计/ChainAudit/跑行为探针（正门进局）` 后重跑本报告即并入");
        }
        sb.AppendLine();

        // ── 全量违例合并（分级）──────────────────────────────────────────────
        sb.AppendLine("===== 全量违例合并（分级门禁）=====");
        sb.AppendLine("  分级判据：🔴结构性断链=硬拦 ／ 🟡语义可疑=列报 ／ 豁免=不计（变更须策划端确认 D561）");
        ChainAuditCore.AppendViolations(sb, "全量违例（明细为主 L-11）", all);
        sb.AppendLine();

        // ── 汇总与判定 ───────────────────────────────────────────────────────
        sb.AppendLine("===== 汇总与判定（L-11：仅索引；明细见上）=====");
        sb.AppendLine("  R3=" + r3.Violations.Count + "  R4=" + r4.Violations.Count
                      + "  R5=" + r5.Violations.Count + "  R6=" + r6.Violations.Count + "（静态层）");
        sb.AppendLine("  分级：🔴=" + red + "  🟡=" + yellow + "  豁免=" + exempt);
        sb.AppendLine("  " + ChainAuditCore.JudgeLine(red, yellow, exempt));
        sb.AppendLine("  " + ChainAuditCore.TriangleVerdict(red, yellow, probeRed, probePresent));
        sb.AppendLine("  四案复现自证：R3(DZ-072类)=" + (R3_SupplyChain.SelfTestOk ? "✅" : "❌")
                      + "  R4(DZ-043类)=" + (R4_ActionReach.SelfTestOk ? "✅" : "❌")
                      + "  R5(幽灵引用类)=" + (R5_SixStage.SelfTestOk ? "✅" : "❌")
                      + "  R6(双向列报)=" + (R6_DocDrift.SelfTestOk ? "✅" : "❌"));

        string report = sb.ToString();
        ChainAuditCore.LogReport(report, Tag);
        ChainAuditCore.WriteChainReport(report, Tag);
    }
}
