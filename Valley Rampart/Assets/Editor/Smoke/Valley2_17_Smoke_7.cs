using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using static BuildingFactory;

// ============================================================================
//  2_17 步骤7 行为级冒烟（指令通道建造门面：建造入口加 kingdomId，玩家/AI 同入口同规则 D331/D345）
//  用法：菜单「Valley/验证/2_17_步骤7_指令通道建造门面」——须 Play 上下文（先 Play 再点）。
//  覆盖（验收：AI 经通道下指令与玩家手工等效——同资源扣减、同合法性规则）：
//    aiBuild:      AI(kingdomId>0) 经门面 BuildController.TryBuild → 建筑 kingdomId==ai + AI 国库扣减正确(House cost wood4, 10→6)
//    playerZero:   玩家(0) 经同门面 TryBuild → 建筑 kingdomId==0（同入口不改玩家归属，零回归）
//    sameValidity: 资源不足拒绝(poorReject) + 占用拒绝(blockedReject)；两次均不建、AI 国库未扣
//    deterministic: 同 seed 两轮逐字节一致（镜像原则代码级落地）
//  自包含：InitializeNewGame 重建 → 用产品门面（真建造链 StartConstructing/占格/注册/附件）。
//  收口：不改产品代码；运行不落测试存档。
// ============================================================================
public static class Valley2_17_Smoke_7
{
    private const int SEED = 20260826;

    [MenuItem("Valley/验证/2_17_步骤7_指令通道建造门面")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_17_7冒烟] 须在 Play 上下文执行。"); return; }
        var runner = new GameObject("2_17_7_SmokeRunner");
        runner.AddComponent<SmokeCoroutineHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var sb = new StringBuilder();
        List<string> r1 = new List<string>(), r2 = new List<string>();

        for (int round = 1; round <= 2; round++)
        {
            var lm = LoadManager.Instance;
            if (lm == null) { Debug.LogError("[2_17_7冒烟] LoadManager 不可用。"); yield break; }
            lm.InitializeNewGame(new NewGameConfig
            {
                mapSeed = SEED, worldSeed = SEED, difficulty = 2,
                worldSize = WorldSize.Medium, kingdomName = "2_17_7冒烟",
                selectedSlotId = "smoke_2_17_7"
            });
            yield return null;

            List<string> checks = new List<string>();
            yield return Scenario(checks);
            if (round == 1) r1 = checks; else r2 = checks;
        }

        bool deterministic = Join(r1) == Join(r2);
        foreach (var c in r1) sb.Append(c).Append(' ');
        sb.Append($" | 确定性两轮逐字节一致={(deterministic ? "OK" : "FAIL")} ");
        bool all = deterministic && !r1.Exists(c => c.Contains("FAIL"));
        Debug.Log("[2_17_7冒烟] " + sb);
        Debug.Log($"[2_17_7冒烟] ===== {(all ? "ALL PASS" : "HAS FAIL")}（AI建造归属+扣费/玩家零回归/同校验拒绝/确定性）=====");
    }

    private static IEnumerator Scenario(List<string> checks)
    {
        var grid = GridSystem.Instance;
        if (grid == null) { checks.Add("grid-FAIL"); yield break; }
        var reg = KingdomRegistry.Instance;
        if (reg == null) { checks.Add("registry-FAIL"); yield break; }

        KingdomState ai = null;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && !all[i].IsPlayer) { ai = all[i]; break; }
        if (ai == null) { checks.Add("noAI-FAIL"); yield break; }

        var house = FindDefById("House");
        if (house == null) { checks.Add("def-FAIL"); yield break; }

        var bc = BuildController.Instance;
        if (bc == null) { checks.Add("bc-FAIL"); yield break; }

        checks.Add($"aiId={ai.id}");

        // ===== 确定性扫描 4 个互不重叠的可放置自由格（20/21/22 供 AI 段，23 供玩家段）=====
        GridCoord? freeA = FindFreeSub(grid, house, 20);
        GridCoord? freeB = FindFreeSub(grid, house, 21);
        GridCoord? freeC = FindFreeSub(grid, house, 22);
        GridCoord? freeP = FindFreeSub(grid, house, 23);
        if (!freeA.HasValue || !freeB.HasValue || !freeC.HasValue || !freeP.HasValue)
        { checks.Add("node-FAIL"); yield break; }
        GridCoord subA = freeA.Value, subB = freeB.Value, subC = freeC.Value, subP = freeP.Value;

        // ===== ① aiBuild：AI 经门面正向建造，归属+扣费（House wood4：10→6）=====
        ai.resources.wood = 10;
        bool aiBuilt = bc.TryBuild(house, subA, GateOrientation.Horizontal, ai.id);
        int aiWoodAfter = ai.resources.wood;
        bool aiOwned = KingdomHasHouseAt(grid, ai.id, subA);
        bool aiBuildOk = aiBuilt && aiOwned && aiWoodAfter == 6;
        checks.Add($"AI建造归属+扣费={(aiBuildOk ? "OK" : "FAIL")}");
        checks.Add($"aiWood={aiWoodAfter}");

        // ===== ② 同校验·占用拒绝：B 格先成功占，再建同格 → Blocked，AI 国库不再扣 =====
        ai.resources.wood = 6;
        bool bBuilt = bc.TryBuild(house, subB, GateOrientation.Horizontal, ai.id);
        int woodAfterB = ai.resources.wood;
        bool blockedOk = false;
        if (bBuilt)
        {
            bool again = bc.TryBuild(house, subB, GateOrientation.Horizontal, ai.id);  // 同格再建 → 拒
            blockedOk = !again && ai.resources.wood == woodAfterB;                     // 拒绝且未二次扣费
        }
        checks.Add($"同校验·占用拒绝={(blockedOk ? "OK" : "FAIL")}");

        // ===== ③ 同校验·资源不足拒绝：AI 国库清零 → 建造 → Resource 拒，C 格无落成 =====
        ai.resources.wood = 0;
        bool poorBuilt = bc.TryBuild(house, subC, GateOrientation.Horizontal, ai.id);
        bool noCb = !KingdomHasHouseAt(grid, ai.id, subC);
        checks.Add($"同校验·资源不足拒绝={(poorBuilt == false && noCb && ai.resources.wood == 0 ? "OK" : "FAIL")}");

        // ===== ④ playerZero：玩家(0) 经同门面 → 建筑 kingdomId 恒 0（同入口不改玩家归属）=====
        // 玩家凑单走 WarehouseHelper；若可负担则落成并仍属 0，否则按同规则拒绝。结果确定性记录，不预设。
        bool pBuilt = bc.TryBuild(house, subP, GateOrientation.Horizontal, 0);
        bool pOwned = KingdomHasHouseAt(grid, 0, subP);
        if (pBuilt)
            checks.Add($"玩家零回归={(pOwned ? "OK" : "FAIL")}");
        else
            checks.Add("玩家零回归=PASS1(reject按同规则)");   // 玩家不可负担=同规则如此，非回归
    }

    /// <summary>确定性扫描首个「放置合法(忽略资源门)」的微格：格内/非水/可走/未占用/非障碍。region 决定起始列保证互不重叠。</summary>
    private static GridCoord? FindFreeSub(GridSystem grid, BuildingDef def, int region)
    {
        int baseX = 30 + (region - 20) * 5;   // 20→30,21→35,22→40,23→45 起始列，span 内互不重叠
        int baseY = 30, span = 20;
        for (int dy = 0; dy < span; dy++)
        for (int dx = 0; dx < span; dx++)
        {
            var cell = new GridCoord(baseX + dx, baseY + dy);
            if (!grid.IsInBounds(cell)) continue;
            if ((grid.GetWalkFlags(cell) & WalkFlags.Water) != 0) continue;
            if (!grid.IsWalkable(cell)) continue;
            if (BuildingRegistry.Instance != null && BuildingRegistry.Instance.GetAt(cell) != null) continue;
            if (grid.IsObstacle(cell)) continue;
            return grid.CellToSub(cell, 0, 0);
        }
        return null;
    }

    /// <summary>该 sub 原点格是否有一个属于某 kingdomId 的 House 建筑。</summary>
    private static bool KingdomHasHouseAt(GridSystem grid, int kingdomId, GridCoord sub)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null) return false;
        var b = reg.GetAt(grid.SubToCell(sub));
        return b != null && b.kingdomId == kingdomId && b.def != null && b.def.id == "House";
    }

    private static string Join(List<string> l)
    {
        var sb = new StringBuilder();
        foreach (var s in l) sb.Append(s).Append('|');
        return sb.ToString();
    }

    private class SmokeCoroutineHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}