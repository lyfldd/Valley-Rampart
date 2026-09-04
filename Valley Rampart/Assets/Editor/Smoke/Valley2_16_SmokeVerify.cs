using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_16 P0 冒烟取证工具（确定性验收）——验证工具，不入运行时系统代码，2_17 回归可复用
//  用法：菜单「Valley/验证/2_16 P0冒烟取证(9组合×2局)」
//        - 编辑模式：仅跑「单组合」也能触发，但 Singalton 自动创建在编辑态调 DontDestroyOnLoad 会抛异常，
//          故本工具须在 Play 上下文执行（先 Play，再 ExecuteMenuItem）。
//  覆盖验收项：
//    1. 同 seed 两次生成 → canonical dump 逐字节一致（计划冒烟 #1 确定性）
//    2. D288 档位：AI 数 Small2~3 / Medium3~4 / Large4~6
//    3. 错峰档人口台账：d1 帐篷 4w0war / d2 村落 6w2war / d3 要塞 8w4war
//    4. 模板不重复抽取（map.kingdomTemplates[1..N] 名互异）
//    5. kingdomId 归属：全部 AI id>=1（编辑模式玩家未注册，故仅验 AI 侧；玩家 id=0 已由 Play 段验）
//  canonical dump 只序列化业务数据（seed/spawn 格/模板名/性格/账本），
//  排除 GetInstanceID/对象哈希/字典序——否则「逐字节一致」永远假红。
// ============================================================================
public static class Valley2_16SmokeVerify
{
    private const int SEED = 12345;   // 固定种子：逐组合同 seed 双跑比对确定性

    [MenuItem("Valley/验证/2_16 P0冒烟取证(9组合×2局)")]
    static void RunSmoke_Full() => Run(full: true);

    [MenuItem("Valley/验证/2_16 P0冒烟取证(单组合 Medium/Normal)")]
    static void RunSmoke_Single() => Run(full: false);

    // D522 自动跑（HH.64 段B#2）：MCP 无人点对话框 → 无模态版；活局守卫=HH.57 §五 铁律代码化——
    // 本工具含 ResetWorld+GenerateMapForPreview×18（世界副作用），禁止在任何已进局世界执行。
    [MenuItem("Valley/验证/2_16 P0冒烟取证_自动跑(无框·禁活局)")]
    static void RunSmoke_Auto() => Run(full: true, showDialog: false);

    private readonly struct ComboResult { public readonly bool ok; public readonly string detail; public ComboResult(bool ok, string detail) { this.ok = ok; this.detail = detail; } }

    static void Run(bool full, bool showDialog = true)
    {
        var wm = Object.FindAnyObjectByType<WorldManager>();
        if (wm == null)
        {
            Debug.LogError("[2_16冒烟] WorldManager 未找到，无法取证。");
            return;
        }
        // 活局守卫（HH.57 事故铁律）：ActiveMap 在场=已进局（正式进局链写入）——拒绝执行，防重置用户世界。
        if (WorldManager.Instance != null && WorldManager.Instance.ActiveMap != null)
        {
            Debug.LogError("[2_16冒烟] 活局守卫：ActiveMap 在场（已进局世界）——本工具含 ResetWorld 禁在活局执行（HH.57 §五）。请裸 Play（不进局）重试。");
            if (showDialog) EditorUtility.DisplayDialog("2_16 P0 冒烟取证", "活局守卫：已进局世界禁执行（HH.57 铁律），请裸 Play 重试", "确定");
            return;
        }

        var sizes = full ? new[] { WorldSize.Small, WorldSize.Medium, WorldSize.Large }
                          : new[] { WorldSize.Medium };
        var diffs = full ? new[] { 1, 2, 3 } : new[] { 2 };

        bool allPass = true;
        foreach (var size in sizes)
        foreach (var diff in diffs)
        {
            var r = VerifyCombo(wm, size, diff, SEED);
            allPass &= r.ok;
            Debug.Log($"[2_16冒烟] size={size} diff={diff} => {(r.ok ? "PASS" : "FAIL")} | {r.detail}");
        }
        Debug.Log($"[2_16冒烟] ===== {(full ? "9组合×2局" : "单组合")} 总体: {(allPass ? "ALL PASS" : "HAS FAIL")} =====");
        if (showDialog)
            EditorUtility.DisplayDialog("2_16 P0 冒烟取证", allPass ? "全部 PASS" : "存在 FAIL，见 Console 明细", "确定");
    }

    static ComboResult VerifyCombo(WorldManager wm, WorldSize size, int difficulty, int seed)
    {
        // ---- 第一遍 ----
        ResetWorld();
        var map1 = wm.GenerateMapForPreview(seed, size, difficulty);
        var dump1 = Canonical(map1);
        int aiCount1 = KingdomRegistry.Instance.Count;

        // ---- 第二遍（同 seed 重跑） ----
        ResetWorld();
        var map2 = wm.GenerateMapForPreview(seed, size, difficulty);
        var dump2 = Canonical(map2);
        int aiCount2 = KingdomRegistry.Instance.Count;

        var sb = new StringBuilder();
        bool determinism = dump1 == dump2;
        sb.Append($"determinism={(determinism ? "yes" : "NO")} ");

        bool aiOk = aiCount1 == aiCount2 && InD288Range(size, aiCount1);
        sb.Append($"D288={aiCount1}->{(aiOk ? "ok" : "BAD")} ");

        // 模板不重复（map2 的 AI 段模板名互异）
        bool tplDistinct = true;
        if (map2.kingdomTemplates != null)
        {
            var seen = new HashSet<string>();
            for (int i = 1; i < map2.kingdomTemplates.Count; i++)
            {
                var t = map2.kingdomTemplates[i];
                if (t == null) continue;
                if (!seen.Add(t.templateName)) { tplDistinct = false; break; }
            }
        }
        sb.Append($"tplDistinct={(tplDistinct ? "ok" : "DUP")} ");

        // 错峰档人口台账 + kingdomId 归属（用 register2）
        bool tierOk = false, kidOk = true;
        var reg = KingdomRegistry.Instance;
        if (reg.Count > 0)
        {
            var k = reg.GetAll()[0];
            tierOk = (difficulty == 1 && k.workerCount == 4 && k.warriorCount == 0)
                  || (difficulty == 2 && k.workerCount == 6 && k.warriorCount == 2)
                  || (difficulty == 3 && k.workerCount == 8 && k.warriorCount == 4);
            sb.Append($"tier(kid{k.id},{k.workerCount}w/{k.warriorCount}war,wood{k.resources.wood})=(difficulty{difficulty}->{(tierOk ? "ok" : "BAD")}) ");
        }
        else sb.Append("tier=NO-KINGDOM ");
        foreach (var kk in reg.GetAll()) if (kk.id < 1) { kidOk = false; break; }
        sb.Append($"kingdomId={(kidOk ? "ok" : "BAD")}");

        bool pass = determinism && aiOk && tplDistinct && tierOk && kidOk;
        return new ComboResult(pass, sb.ToString());
    }

    static bool InD288Range(WorldSize size, int aiCount)
    {
        if (size == WorldSize.Small) return aiCount >= 2 && aiCount <= 3;
        if (size == WorldSize.Medium) return aiCount >= 3 && aiCount <= 4;
        return aiCount >= 4 && aiCount <= 6;   // Large
    }

    /// <summary>跨次生成重置：清注册表（重置 id）+ 清建筑实体 + 清单位实体（防多次生成累加）。须在 Play 上下文调用。
    /// HH.64 段B#2 适配（2_17 步骤4 台账转派生）：workerCount/warriorCount 现由存活实体按 kingdomId 派生
    /// （KingdomState ①真源演进），本工具旧版只清王国/建筑不清单位 → 前遍工人实体残留 → 跨遍累加 →
    /// determinism 假红（9/9 FAIL，工人 4→8 膨胀实录）。取证确定性断言必须实体级清场。</summary>
    static void ResetWorld()
    {
        if (KingdomRegistry.Instance != null) KingdomRegistry.Instance.ResetState();
        if (BuildingFactory.Instance != null) BuildingFactory.Instance.ClearAllBuildings();
        // 单位实体清算：注册表内单位 GameObject 即时销毁（同步取证循环需立即反映，不用帧末 Destroy）+ 注册表清空
        if (UnitRegistry.Instance != null)
        {
            foreach (var u in UnitRegistry.Instance.GetAllUnits())
                if (u != null) Object.DestroyImmediate(u.gameObject);
            UnitRegistry.Instance.Clear();
        }
    }

    /// <summary>业务数据规范化 dump（Newline 分隔，去除排序噪声/对象哈希）。</summary>
    static string Canonical(MapData map)
    {
        var sb = new StringBuilder();
        sb.Append(map.seed).Append('|').Append(map.width).Append('x').Append(map.height);
        if (map.kingdomSpawns != null)
            for (int i = 0; i < map.kingdomSpawns.Count; i++)
                sb.Append('\n').Append('S').Append(i).Append(':').Append(map.kingdomSpawns[i].x).Append(',').Append(map.kingdomSpawns[i].y);
        if (map.kingdomTemplates != null)
            for (int i = 0; i < map.kingdomTemplates.Count; i++)
                sb.Append('\n').Append('T').Append(i).Append(':').Append(map.kingdomTemplates[i] != null ? map.kingdomTemplates[i].templateName : "PLAYER");
        var reg = KingdomRegistry.Instance;
        if (reg != null)
            foreach (var k in reg.GetAll())
            {
                sb.Append('\n').Append('K').Append(k.id)
                  .Append(':').Append(k.name)
                  .Append('|').Append(Arr(k.personality))
                  .Append('|').Append(k.workerCount).Append('/').Append(k.warriorCount)
                  .Append('|').Append(k.resources.wood).Append('/').Append(k.resources.stone)
                  .Append('/').Append(k.resources.gold);
            }
        return sb.ToString();
    }

    static string Arr(float[] a)
    {
        if (a == null || a.Length == 0) return "[]";
        var sb = new StringBuilder("[");
        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i].ToString("R")); }
        return sb.Append(']').ToString();
    }
}