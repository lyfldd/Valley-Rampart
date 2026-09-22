using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.319 `M1-F` · `U-15` 止血批（`D809`/`D810`）· **阶段 A 探针**（短局 · 判据 1/4/5/8）
//  菜单：「Valley/验证/HH319 U15止血探针_阶段A」——进 Play 后点。
//
//  判据覆盖：
//   1 ⭐⭐ 箱子侧 A/B 对照（先"止血后"生产落点 ⇒ 再"止血前等效"路径 ⇒ 箱数/含水明细）
//   4 面板布尔（`IsReadyToHarvest`）：纯水井仓 ⇒ false（灰）· 混合仓 ⇒ true（可点）
//   5 `Harvest()` 不清不可收资源（混合仓：粮入国库 · 水留仓 · 逐值）
//   8 仓内容物逐值（本批零 schema 改动 ⇒ 给"形状未变"读数）
//  另：短局内观察是否出现「派发搬运任务 @ 水井格」（判据 2 的预证 · 长局另跑）。
//  ⛔ 本容器不改生产码；收尾 ExitTestRun ＋ QuitSmoke。
// ============================================================================
public static class Valley_HH319_U15Probe
{
    [MenuItem("Valley/验证/HH319 U15止血探针_阶段A")]
    public static void RunA()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319U15] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_U15Host").AddComponent<RunHost>().Host(RunA_Coroutine());
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    private static List<ChestEntity> AllChests()
    {
        var buf = new List<ChestEntity>();
        if (ChestManager.Instance != null)
            ChestManager.Instance.FillChestsInCellRect(new RectInt(-4096, -4096, 8192, 8192), buf);
        return buf;
    }

    private static string ChestDump()
    {
        var sb = new StringBuilder();
        var list = AllChests();
        sb.Append("Count=").Append(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            var ce = list[i];
            if (ce == null) continue;
            sb.Append(" [").Append(i).Append("](").Append(ce.cell.x).Append(",").Append(ce.cell.y).Append(")=").Append(ce.contents);
        }
        return sb.ToString();
    }

    private static IEnumerator RunA_Coroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20319, mapSeed = 20319, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_w319u15", kingdomName = "止血"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);   // 短局用 15× 省时（止血判定与时基无关）
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 2)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[HH319U15] 等世界就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        // 建一口玩家国水井（生产对象）
        Building well = null;
        var def = Resources.Load<BuildingDef>("Buildings/Well");
        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        for (int ring = 0; ring <= 5 && well == null; ring++)
            for (int dx = -ring; dx <= ring && well == null; dx++)
                for (int dy = -ring; dy <= ring && well == null; dy++)
                {
                    if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;
                    var pos = anchor + new Vector2(-5f + dx, dy);
                    var co = GridSystem.Instance.WorldToCoord(pos);
                    if (!co.HasValue) continue;
                    if (BuildingFactory.Instance.CreateBuildingInstance(def, def.sourceType, co.Value,
                            new Vector2Int(Mathf.Max(1, def.footprint.x), Mathf.Max(1, def.footprint.y)), pos,
                            true, ResourceGrade.Normal, false, BuildingState.Active, 0))
                        well = GridSystem.Instance.GetOccupant(co.Value) as Building;
                }
        if (well == null) { Debug.LogError("[HH319U15] 建井失败。"); TestHarnessApi.ExitTestRun(); SmokeApi.QuitSmoke(); yield break; }
        var st = well.GetComponent<StorageComponent>();
        var vault = TreasureVault.Get(0);
        Debug.Log($"[HH319U15] 布置：well@{well.transform.position} 声明={string.Join("|", st.DeclaredPaths)} 国库Accepts(Water)={vault?.Accepts(ResourceType.Water)}");
        yield return new WaitForSeconds(3f);   // 水井免工自产

        // ── 判据 1 · 止血后（生产落点 `HarvestCarry`） + 判据 4（纯水 ⇒ 灰）──
        int w0 = st.GetAmount(ResourceType.Water);
        Debug.Log($"[HH319U15] §1-A 止血后（前置）：井仓 Water={w0} · IsReadyToHarvest={st.IsReadyToHarvest()}（期望 False ⇒ 面板灰）· {ChestDump()}");
        int carried = st.Harvest();                 // ⭐ `M1-G-1`：原链 B 落点（直通国库口）已删 ⇒ 改走 `Harvest()`
        yield return null;
        Debug.Log($"[HH319U15] §1-A 止血后（结果）：HarvestCarry 返回={carried}（期望 0 ⇒ 完全不取）· 井仓 Water={st.GetAmount(ResourceType.Water)}（期望 {w0} · 留仓）· {ChestDump()}（期望 Count=0）");

        // ── 判据 1 · 止血前等效（旧路径落点＝直接 ModifyResource ⇒ 国库不收 ⇒ overflow 装箱）──
        int chestBefore = AllChests().Count;
        RulerController.Instance?.ModifyResource(ResourceType.Water, true, 5);   // ⭐ 旧 HarvestCarry 的落点（不改码复现）
        yield return null;
        Debug.Log($"[HH319U15] §1-B 止血前等效（旧落点 ModifyResource(Water,true,5)）：箱数 {chestBefore} → {AllChests().Count} · {ChestDump()}（期望 Count>0 且内容含水 ⇒ 证实「转箱」）");

        // ── 判据 5 + 4 · 混合仓（水 + 粮）：Harvest() 只收可收的，水留仓 ──
        // ⚠️ 水井仓声明 = `res_fluid.water` ⇒ 收不了粮（`Add(Food)` 被标签拦截）⇒ 先临时改成**通用仓**（`res`）
        //    造"水 + 粮"混合态（⚠️ 只改运行时实例的声明 · ⛔ 不动资产 · 属探针构造）。
        st.SetDeclaredPaths(new[] { "res" });
        st.Add(ResourceType.Food, 10);
        // ⚠️ 本局国库初始已满（金币100/石材100/木材100/食物50 ⇒ 占用 250/250）⇒ 先腾出空间，
        //    才能验证「可收且有空间 ⇒ 粮入国库」（否则只验到容量守卫 ⇒ 见上一轮读数）。
        if (vault != null) vault.Take(ResourceType.Food, 30);
        int foodVault0 = vault != null ? vault.GetAmount(ResourceType.Food) : -1;
        int w1 = st.GetAmount(ResourceType.Water);
        int f1 = st.GetAmount(ResourceType.Food);
        Debug.Log($"[HH319U15] §5 前置：混合仓 水={w1} 粮={f1} · IsReadyToHarvest={st.IsReadyToHarvest()}（期望 True ⇒ 可点）· 国库粮={foodVault0}");
        int got = st.Harvest();
        int w2 = st.GetAmount(ResourceType.Water);
        int f2 = st.GetAmount(ResourceType.Food);
        int foodVault1 = vault != null ? vault.GetAmount(ResourceType.Food) : -1;
        Debug.Log($"[HH319U15] §5 结果：Harvest 返回={got}（期望 10＝只收粮）· 井仓 水 {w1}→{w2}（期望不变＝留仓）· 粮 {f1}→{f2}（期望 0）· 国库粮 {foodVault0}→{foodVault1}（期望 +10）");

        // ── 判据 8 · 内容物逐值（形状读数）──
        Debug.Log($"[HH319U15] §8 仓内容物逐值：well.Contents={st.Contents} · 国库 Contents={vault?.Contents}");

        // ── 判据 2 预证：等 10 秒看有无「派发搬运任务 @ 水井格」 ──
        yield return new WaitForSeconds(10f);
        Debug.Log($"[HH319U15] §2 预证：等 10 秒后 {ChestDump()} · 井仓 Water={st.GetAmount(ResourceType.Water)}（请对照 Console 有无「派发搬运任务 @ {well.transform.position}」行）");

        Debug.Log("[HH319U15] ── 阶段 A 收尾（ExitTestRun ＋ 退 Play）");
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }
}
