using System.Text;
using UnityEngine;
using UnityEditor;
using static KingdomRegistry;

// ============================================================================
//  2_17 步骤 2a：KingdomState.resources 转正 AI 国库真源 + 入档回环（真 Save/Load 全阶段链路）
//  用法：菜单「Valley/验证/2_17_步骤2a_国库真源循环」——须 Play 上下文，且从干净 Play 入口启动。
//  覆盖（对账裁决 ② B）：CanAfford/Spend/Refund/AddResources 台账语义 + resources 跨存读回环。
//  收口：不改产品代码；运行结束清测试存档。
// ============================================================================
public static class Valley2_17_Smoke_Treasury2a
{
    private const int SEED = 888888;
    private const string SLOT = "smoke_2a_treasury";

    [MenuItem("Valley/验证/2_17_步骤2a_国库真源循环")]
    public static void Run()
    {
        var sb = new StringBuilder();
        bool allPass = true;

        var lm = LoadManager.Instance;
        var sm = SaveManager.Instance;
        var _ = KingdomRegistry.Instance;   // 物化单例
        if (lm == null || sm == null) { Debug.LogError("[2_17_2a] LoadManager/SaveManager 不可用。"); return; }

        lm.InitializeNewGame(new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
            worldSize = WorldSize.Medium, kingdomName = "2a国库真源冒烟", selectedSlotId = SLOT
        });

        // 取一个 AI 王国（id>0）作为国库测试对象
        KingdomState ai = null;
        foreach (var st in KingdomRegistry.Instance.GetAll())
            if (st.id > 0) { ai = st; break; }
        if (ai == null) { Debug.LogError("[2_17_2a] 未找到 AI 王国，中断。" ); return; }
        int aiId = ai.id;
        sb.Append($"目标国: k{aiId} ");

        // ---- 国库读 API 语义断言（台账，不进玩家事件链）----
        ai.resources = new ResourcePack { gold = 100, stone = 50, wood = 30, food = 40, metal = 10 };
        bool affordOk1 = ai.CanAfford(new ResourcePack { gold = 30 });          // true
        bool affordOk2 = !ai.CanAfford(new ResourcePack { gold = 999 });        // false
        sb.Append($"CanAfford={(affordOk1&&affordOk2?"OK":"FAIL")} ");

        ai.Spend(new ResourcePack { gold = 20, stone = 10 });                    // 80/40/30/40/10
        ai.Refund(new ResourcePack { gold = 10 }, 0.5f);                        // 85/40/30/40/10
        ai.AddResources(new ResourcePack { wood = 5 });                         // 85/40/35/40/10
        bool ledgerOk = ai.GetResourceValue(ResourceType.Gold) == 85
                     && ai.GetResourceValue(ResourceType.Wood) == 35
                     && ai.GetResourceValue(ResourceType.Stone) == 40;
        sb.Append($"台账增减={(ledgerOk?"OK":"FAIL")} ");

        // ---- 入档回环：真 SaveManager.Save/Load 全阶段链路 ----
        bool saved = sm.Save(SLOT);
        bool loaded = sm.Load(SLOT);
        sb.Append($"save={(saved?"OK":"FAIL")} load={(loaded?"OK":"FAIL")} ");
        if (!saved || !loaded) { Debug.LogError("[2_17_2a] 存/读失败，中断。" + sb); return; }

        // 读档后重取同 id 王国，断言 resources 经真实链路恢复一致
        KingdomState restored = null;
        foreach (var st in KingdomRegistry.Instance.GetAll())
            if (st.id == aiId) { restored = st; break; }
        bool roundtripGold = restored != null && restored.GetResourceValue(ResourceType.Gold) == 85;
        bool roundtripWood = restored != null && restored.GetResourceValue(ResourceType.Wood) == 35;
        bool roundtripAfford = restored != null && restored.CanAfford(new ResourcePack { gold = 85, wood = 35 });
        sb.Append($"读档回环(gold85/wood35/afford)={(roundtripGold&&roundtripWood&&roundtripAfford?"OK":"FAIL")} ");

        allPass = affordOk1 && affordOk2 && ledgerOk && saved && loaded && roundtripGold && roundtripWood && roundtripAfford;

        Debug.Log($"[2_17_2a] {sb}");
        Debug.Log($"[2_17_2a] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（国库真源读API+入档回环）=====");

        try { sm.Delete(SLOT); } catch { /* 忽略 */ }
    }
}