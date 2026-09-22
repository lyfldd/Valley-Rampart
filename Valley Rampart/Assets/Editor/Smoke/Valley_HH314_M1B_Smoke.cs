using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ============================================================================
//  HH.314 · M1-B（账本 ＋ 读口）同局冒烟（Editor-only）
//  口径真源：HH.314 任务书 §二（承 HH.313 裁决 §五：M1-A 冒烟并入本片）
//           ＋ 09_资源与仓库.md §5.2（三条硬规则）／§7.5（查档 ①~⑦）／§六（账本行＝仓库×资源）
//
//  入口纪律（test-harness-first 铁律1）：**走正门** TestHarnessApi.EnterTestRun（禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → 封盘 → ExitTestRun → 退 Play（禁留 1x 余留世界）。
//  在线判据（L-34）：§G 产出观测设「命中即停」。
//
//  段：
//    §A 起局（正门）+ 直建三座（Warehouse／Granary／quarry）
//    §B 入仓读数（一条容量线 · 体积 · 部分成功 · 标签拒收）
//    §C 事件契约计数（何时发／发几次／空转不发）
//    §D 面板读数（反射**真** WarehousePanel：专属基线 → 转通用 → 逐行 delta）
//    §F 件4 真缺口实证（Granary Lv3 容量未随等级刷新 ⇒ 读档过度 clamp）
//    §G 产出／搬运观测（quarry 等 3 游戏日·命中即停 ＋ HarvestCarry 真搬运口）
//    §E 存读档复查（读数逐项对照）
//  落盘：Logs/hh314_m1b/hh314_m1b_smoke.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH314M1BSmoke
{
    public const string Tag = "HH314M1B";
    private const string Menu = "Valley/验证/HH314 M1-B 账本与读口 同局冒烟（正门进局）";
    private const int SEED = 31418;
    private const string SLOT = "hh314_m1b";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;

    [MenuItem(Menu, priority = 214)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 冒烟已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0;
        Sb.AppendLine("# HH.314 · M1-B（账本 ＋ 读口）同局冒烟（正门 EnterTestRun · seed=" + SEED + " 槽=" + SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        new GameObject("HH314M1BSmokeHost").AddComponent<Host>().Go(Run());
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string s) { Sb.AppendLine(s); Debug.Log("[" + Tag + "] " + s); }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = SLOT, kingdomName = "M1B冒烟"
        };
        Log("── §A 正门进局（seed=" + SEED + " Small/difficulty=2）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }
        Log("§A 起局：地图=" + map.width + "x" + map.height + " 日=" + Day() + " 建筑数=" + LiveBuildings()
            + " 倍率=" + Time.timeScale);

        // ---- 直建三座（Warehouse＝挂仓专属木 / Granary＝挂仓专属粮 / quarry＝产石）----
        var wh = Make("Warehouse", 20, 20); var whCoord = LastCoord;
        var gr = Make("Granary", 26, 20); var grCoord = LastCoord;
        var qy = Make("quarry", 32, 20);
        if (wh == null || gr == null) { Log("❌ 直建失败 ⇒ 中止"); Finish(); yield break; }
        var st = wh.GetComponent<StorageComponent>();
        var gst = gr.GetComponent<StorageComponent>();
        Log("§A 直建：Warehouse=" + (wh != null) + "@" + whCoord.x + "," + whCoord.y
            + " Granary=" + (gr != null) + "@" + grCoord.x + "," + grCoord.y
            + " quarry=" + (qy != null) + " ⇒ 建筑数=" + LiveBuildings());

        // ================= §B 入仓读数 =================
        Log("");
        Log("## §B 入仓读数（09 §5.2 三条硬规则）");
        if (st == null) { Log("❌ Warehouse 无 StorageComponent ⇒ 中止"); Finish(); yield break; }
        Log("§B 仓声明（专属）＝ [" + Join(st.DeclaredPaths) + "] capacity=" + st.capacity
            + "（＝ def.producer.capacity × LevelScale）");
        int d1 = st.Deposit(ResourceType.Wood, 60);          // cap 40 ⇒ 部分成功
        int d2 = st.Deposit(ResourceType.Stone, 10);         // 标签拒收
        Log("§B Deposit(Wood,60)=" + d1 + "（期望 40＝放到满为止·部分成功）；Deposit(Stone,10)=" + d2 + "（期望 0＝标签拒收）");
        Log("§B 读数：TotalCount=" + st.TotalCount + " UsedSpace=" + st.UsedSpace + "/" + st.capacity
            + " FreeSpace=" + st.FreeSpace + " IsFull=" + st.IsFull
            + " CanAccept(Wood)=" + st.CanAccept(ResourceType.Wood) + " Contents=" + st.Contents);
        Log("§B 判据：TotalCount(" + st.TotalCount + ") == UsedSpace(" + st.UsedSpace + ")"
            + " ⇒ " + (st.TotalCount == st.UsedSpace ? "体积全 1 时两口径相等 ✅" : "⚠️ 体积非 1（如实列报）"));

        // ================= §C 事件契约计数 =================
        Log("");
        Log("## §C 事件契约计数（M1-B 件1 落码的契约条文）");
        int ev = 0;
        System.Action<StorageComponent> h = _ => ev++;
        if (gst != null)
        {
            gst.OnStorageChanged += h;
            ev = 0; gst.Deposit(ResourceType.Food, 5);       Log("§C Granary·Add 成功     ⇒ 事件 " + ev + " 次（契约期望 1）");
            ev = 0; gst.Deposit(ResourceType.Stone, 5);      Log("§C Granary·标签拒收     ⇒ 事件 " + ev + " 次（契约期望 0）");
            ev = 0; gst.TakeOut(ResourceType.Food, 2);       Log("§C Granary·TakeOut 成功  ⇒ 事件 " + ev + " 次（契约期望 1）");
            ev = 0; gst.TakeOut(ResourceType.Food, 0);       Log("§C Granary·amount≤0      ⇒ 事件 " + ev + " 次（契约期望 0）");
            ev = 0; gst.Clear();                             Log("§C Granary·Clear         ⇒ 事件 " + ev + " 次（契约期望 1）");
            gst.OnStorageChanged -= h;
        }
        else Log("⚠️ Granary 无 StorageComponent ⇒ §C 跳过");

        // ================= §D 面板读数（真面板）=================
        Log("");
        Log("## §D 面板读数（反射**真** WarehousePanel.RebuildStorageList）");
        var panel = Object.FindObjectOfType<WarehousePanel>();
        Log("§D 面板实例=" + (panel != null ? "在场" : "**不在场**（§D 跳过）"));
        Dictionary<string, int[]> before = null, after = null;
        if (panel != null)
        {
            RebuildPanel(panel);
            before = ReadPanelRows(panel);
            Log("§D 基线（专属声明）行数=" + (before != null ? before.Count : -1) + " ⇒ " + RowsText(before));

            // 转「通用仓 res」⇒ 逐资源展开
            st.SetDeclaredPaths(null);
            Log("§D 转通用：Warehouse 声明＝ [" + Join(st.DeclaredPaths) + "]"
                + " ⇒ Accepts(Wood/Stone/Food/Gold)=" + st.Accepts(ResourceType.Wood) + "/"
                + st.Accepts(ResourceType.Stone) + "/" + st.Accepts(ResourceType.Food) + "/" + st.Accepts(ResourceType.Gold));
            RebuildPanel(panel);
            after = ReadPanelRows(panel);
            Log("§D 通用后行数=" + (after != null ? after.Count : -1) + " ⇒ " + RowsText(after));
            Log("§D ⭐ 容量列 delta（通用后 − 基线）＝ " + DeltaText(before, after)
                + " ｜判读：通用仓 capacity=" + st.capacity + " 应**每行 +" + st.capacity
                + " 各一次**；若出现 +" + (st.capacity * 13) + " ⇒ 才是任务书所称「N 倍虚增」");
        }

        // ---- N+1 契约（用转通用后的仓：两条都被接受）----
        if (st != null)
        {
            st.OnStorageChanged += h;
            ev = 0;
            st.RestoreContents(ResourceList.Of(
                new ResourceAmount(ResourceType.Wood, 2),
                new ResourceAmount(ResourceType.Stone, 3)));
            Log("§C 通用仓·RestoreContents(2 条·全被接受) ⇒ 事件 " + ev + " 次（契约期望 3＝N+1）");
            st.OnStorageChanged -= h;
        }

        // ================= §F 件4 真缺口实证 =================
        Log("");
        Log("## §F 件4 真缺口实证（读档 capacity 未随 level 刷新）");
        if (gr != null && gst != null)
        {
            int lv0 = gr.level; int cap0 = gst.capacity;
            gr.level = 3;
            gst.RefreshCapacity();
            int cap3 = gst.capacity;
            Log("§F Granary level " + lv0 + "→3；capacity " + cap0 + "→" + cap3
                + "（期望 60×2×2=240；LevelScale 连乘 levels[].statScale）");
            int filled = gst.Deposit(ResourceType.Food, 200);
            Log("§F 入粮 200 ⇒ 实际入仓=" + filled + " TotalCount=" + gst.TotalCount
                + " UsedSpace=" + gst.UsedSpace + "/" + gst.capacity);
        }

        // ================= §G 产出／搬运观测（命中即停 · L-34）=================
        Log("");
        Log("## §G 产出／搬运观测");
        if (qy != null)
        {
            var qst = qy.GetComponent<StorageComponent>();
            int d0 = Day(); float t0 = Time.realtimeSinceStartup;
            int hitDay = -1;
            while (Day() < d0 + 3 && Time.realtimeSinceStartup - t0 < 150f)
            {
                yield return null;
                if (qst != null && qst.TotalCount > 0) { hitDay = Day(); break; }   // 命中即停
            }
            Log("§G quarry 本地仓（专属 " + (qst != null ? "[" + Join(qst.DeclaredPaths) + "]" : "无仓") + "）"
                + "：D" + d0 + "→D" + Day() + " ⇒ TotalCount=" + (qst != null ? qst.TotalCount : -1)
                + (hitDay > 0 ? "（**命中即停 @D" + hitDay + "**）" : "（3 日窗内未产 ⇒ 无工人/未派工·如实记）"));
        }
        if (st != null)
        {
            int before2 = st.TotalCount;
            int carried = st.Harvest();          // ⭐ `M1-G-1`：原直通国库口已删 ⇒ 本探针改走 `Harvest()`（手动收取口径）
            Log("§G 手动收取口（原直通国库口 · `M1-G-1` 已删）：TotalCount " + before2 + "→" + st.TotalCount
                + "（搬走 " + carried + "，≤ 携带量 " + st.GetCarryAmount(st.PrimaryStoredType()) + "）");
        }

        // ================= §E 存读档复查 =================
        Log("");
        Log("## §E 存读档复查");
        string whBefore = st != null ? st.Contents.ToString() : "-";
        string grBefore = gst != null ? gst.Contents.ToString() : "-";
        int grCapBefore = gst != null ? gst.capacity : -1;
        int grCntBefore = gst != null ? gst.TotalCount : -1;
        Log("§E 存档前：Warehouse Contents=" + whBefore + "；Granary level=" + (gr != null ? gr.level : -1)
            + " capacity=" + grCapBefore + " Contents=" + grBefore);
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        Log("§E Save(" + SLOT + ") = " + saved);
        bool loaded = SaveManager.Instance != null && SaveManager.Instance.Load(SLOT);
        Log("§E Load = " + loaded);
        yield return null; yield return null;

        var wh2 = FindAt(whCoord);
        var gr2 = FindAt(grCoord);
        var st2 = wh2 != null ? wh2.GetComponent<StorageComponent>() : null;
        var gst2 = gr2 != null ? gr2.GetComponent<StorageComponent>() : null;
        Log("§E 读档后：Warehouse=" + (wh2 != null) + " 声明=[" + (st2 != null ? Join(st2.DeclaredPaths) : "-")
            + "] Contents=" + (st2 != null ? st2.Contents.ToString() : "-"));
        Log("§E 读档后：Granary=" + (gr2 != null) + " level=" + (gr2 != null ? gr2.level : -1)
            + " capacity=" + (gst2 != null ? gst2.capacity : -1) + " Contents=" + (gst2 != null ? gst2.Contents.ToString() : "-"));
        int cap2 = gst2 != null ? gst2.capacity : -1;
        int cnt2 = gst2 != null ? gst2.TotalCount : -1;
        Log("§E ⭐ 件4 缺口判据：存档前 Granary level=3 capacity=" + grCapBefore + " TotalCount=" + grCntBefore
            + " ⇒ 读档后 level=" + (gr2 != null ? gr2.level : -1) + " capacity=" + cap2 + " TotalCount=" + cnt2
            + " ⇒ " + (cap2 != grCapBefore
                ? "容量未随 level 刷新（缺口坐实）；存量 " + grCntBefore + "→" + cnt2 + " 被 clamp 净损 " + (grCntBefore - cnt2)
                : "容量与存档前一致（未复现）"));

        Finish();
    }

    // ======================= 工具 =======================

    private static int Day() => TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;

    private static int LiveBuildings()
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null) return -1;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++) if (reg.All[i] != null) n++;
        return n;
    }

    private static string Join(string[] a) => a == null ? "null" : string.Join(",", a);

    private static Building Make(string defId, int x, int y)
    {
        var def = BuildingFactory.FindDefById(defId);
        if (def == null) { Log("❌ def 缺：" + defId); return null; }
        var map = WorldManager.Instance.ActiveMap;
        var cell = MapGenRules.NearestWalkable(map, x, y);
        var c = new GridCoord(cell.x, cell.y);
        LastCoord = c;
        var fp = def.footprint.x > 0 ? def.footprint : Vector2Int.one;
        var world = GridSystem.FootprintCenterWorld(c, fp, Vector3.zero);
        bool ok = BuildingFactory.Instance != null && BuildingFactory.Instance.CreateBuildingInstance(
            def, def.sourceType, c, fp, world,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: 0);
        if (!ok) { Log("❌ 直建失败：" + defId + " @" + c.x + "," + c.y); return null; }
        return BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(c) : null;
    }

    /// <summary>最近一次直建落点（读档后按坐标复取——按 def 名查会撞到同型既有建筑）。</summary>
    private static GridCoord LastCoord;

    private static Building FindAt(GridCoord c)
        => BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(c) : null;

    /// <summary>反射驱动**真面板**重建（`Open(Interactor)` 的 Interactor 是值类型 ⇒ 不能传 null，改走 Bind+RebuildStorageList）。</summary>
    private static void RebuildPanel(WarehousePanel panel)
    {
        var b = typeof(WarehousePanel).GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Instance);
        if (b != null) b.Invoke(panel, null);
        var r = typeof(WarehousePanel).GetMethod("RebuildStorageList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (r != null) r.Invoke(panel, null);
    }

    /// <summary>反射读**真面板**的 `_storageList` 行 ⇒ 资源名 → [stored, cap]。</summary>
    private static Dictionary<string, int[]> ReadPanelRows(WarehousePanel panel)
    {
        var res = new Dictionary<string, int[]>();
        var f = typeof(WarehousePanel).GetField("_storageList", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = f != null ? f.GetValue(panel) as VisualElement : null;
        if (list == null) { Log("⚠️ 面板 _storageList 为空（Bind 未完成）"); return res; }
        foreach (var row in list.Children())
        {
            var labels = new List<Label>();
            foreach (var el in row.Children()) if (el is Label l) labels.Add(l);
            if (labels.Count < 2) continue;
            var parts = labels[1].text.Split('/');
            if (parts.Length < 2) continue;
            int stored, cap;
            if (!int.TryParse(parts[0].Trim(), out stored)) continue;
            if (!int.TryParse(parts[1].Trim(), out cap)) continue;
            res[labels[0].text] = new[] { stored, cap };
        }
        return res;
    }

    private static string RowsText(Dictionary<string, int[]> rows)
    {
        if (rows == null) return "-";
        var sb = new StringBuilder();
        foreach (var kv in rows) sb.Append(kv.Key + "=" + kv.Value[0] + "/" + kv.Value[1] + " ");
        return sb.ToString().Trim();
    }

    private static string DeltaText(Dictionary<string, int[]> before, Dictionary<string, int[]> after)
    {
        if (after == null) return "-";
        var sb = new StringBuilder();
        foreach (var kv in after)
        {
            int b = (before != null && before.ContainsKey(kv.Key)) ? before[kv.Key][1] : 0;
            sb.Append(kv.Key + (kv.Value[1] - b >= 0 ? "+" : "") + (kv.Value[1] - b) + " ");
        }
        return sb.ToString().Trim();
    }

    // ======================= 收尾（L-32）＋ 落盘 =======================

    private static void Finish()
    {
        Time.timeScale = 0f;                                     // L-32 条文3：真暂停
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();
        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + SLOT + "）→ 退 Play（L-32 条文1：禁留 1x 余留世界）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh314_m1b");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh314_m1b_smoke.txt");
            File.WriteAllText(stable, Sb.ToString());
            File.WriteAllText(Path.Combine(dir, "hh314_m1b_smoke_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}
