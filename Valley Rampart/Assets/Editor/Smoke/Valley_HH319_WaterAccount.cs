using System.Collections;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.319 `M1-F` · `F-1` 补证单（`D809`）· **水账取证**（第二套搬运链落点）
//  用法：菜单「Valley/验证/HH319 水账取证」——进 Play 后点。
//  目的：`ScheduleCenterStub.DispatchTransport` 对"水井仓"（`IsReadyToHarvest()=TotalCount>0`）
//        会派工人执行 `BehaviorExecutor → StorageComponent.HarvestCarry()` ⇒ 本容器**复现该落点动作**
//        并比对「全库 Water 合计」⇒ 判断水是否有落点（国库 `VaultPaths` 不含 `res_fluid.water`）。
//  ⛔ 本容器只取证：不改生产码、不动水域逻辑、不改资产。
//  收尾：QuitSmoke（退 Play）。
// ============================================================================
public static class Valley_HH319_WaterAccount
{
    [MenuItem("Valley/验证/HH319 水账取证")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319取证] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_AcctHost").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    /// <summary>⭐ 只统计**玩家国（k0）**建筑的仓 ⇒ 隔离 AI 国产水的干扰（v2 修正）。</summary>
    private static int PlayerWater()
    {
        int sum = 0;
        foreach (var s in Object.FindObjectsOfType<StorageComponent>())
        {
            if (s == null) continue;
            var b = s.GetComponent<Building>();
            if (b == null || b.kingdomId != 0) continue;
            sum += s.GetAmount(ResourceType.Water);
        }
        return sum;
    }

    private static IEnumerator RunCoroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20319, mapSeed = 20319, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_w319Q", kingdomName = "取证"
        };
        SmokeApi.EnterGame(cfg);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[HH319取证] 等就绪超时。"); yield break; }
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
        if (well == null) { Debug.LogError("[HH319取证] 建井失败。"); SmokeApi.QuitSmoke(); yield break; }
        var st = well.GetComponent<StorageComponent>();
        yield return new WaitForSeconds(3f);   // 让水井自然产水（免工自产 · rate=4/秒）

        // ⭐ 前后**同帧**读（⛔ 不插 yield ⇒ 无产水增量干扰）；只算**玩家国**（隔离 AI 国）
        int ownBefore = st.GetAmount(ResourceType.Water);
        int allBefore = PlayerWater();
        int carried = ownBefore > 0 ? st.Harvest() : 0;   // ⭐ `M1-G-1`：原链 B 落点（直通国库口）已删 ⇒ 改走 `Harvest()`
        int ownAfter = st.GetAmount(ResourceType.Water);
        int allAfter = PlayerWater();

        Debug.Log("[HH319取证] ⭐ HarvestCarry（第二套搬运链 ScheduleCenterStub 的落点动作）对水的影响：");
        Debug.Log($"[HH319取证]   井仓 {ownBefore} → {ownAfter}（HarvestCarry 返回 {carried}）");
        Debug.Log($"[HH319取证]   玩家国 Water 合计（同帧前后）{allBefore} → {allAfter}（差 {allAfter - allBefore}）");
        Debug.Log($"[HH319取证]   ⇒ 搬走={carried} · 玩家国全库差={allAfter - allBefore} ⇒ "
                  + (carried > 0 && (allAfter - allBefore) <= -1 ? "⚠️ 水被销毁（国库不收 res_fluid.water ⇒ ModifyResource 丢弃）"
                     : "水有落点（进了某个收水仓）"));
        SmokeApi.QuitSmoke();
    }
}
