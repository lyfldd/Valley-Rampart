using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.310 · `F-15` 修法小批 验证探针（Editor-only）
//  口径真源：最高优先级文档/03_地图即数据库.md §6.5 新增条（`D782`）
//           ＋ 台账 §五十三（取向＝「允许重叠 ＋ 显式化」）
//           ＋ `HH.310` 施工清单 §三 判据 1~8
//           ＋ `HH.312` 收尾批（删 A/B 对照开关 ⇒ §C 段删·§D 单段·§F 单列）
//
//  入口纪律（test-harness-first 铁律1）：**走正门** TestHarnessApi.EnterTestRun（禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → Save → ExitTestRun → 退 Play。
//
//  判据面：
//    §A 判据1 用例 A（重叠：A 先 B 后）—— 构① 字面（同 coord 同 footprint）＋ 构② 补全（错位 1 格 ⇒ 三格类）
//    §B 判据2 用例 B（无重叠·单座）⇒ 全格 GetAt == null
//    §C 判据3 ⭐ 鉴别力自证 —— **已随 A/B 对照开关删除**（`HH.312` 收尾批；改前读数落盘 `HH.311` 报告 §三）
//    §D 判据4 无重叠零扰动（同局 Registry 逐座逐格 GetAt·单段）
//    §E 判据5 零地图变更（同 seed features+climateZones(+spawns) hash）
//    §F 判据7 ⚠️ 已知限制同报：GridSystem.GetOccupant(共享格) 单列（⛔ 本批不动）
//  落盘：Logs/hh310_f15/hh310_probe.txt（稳定名）＋ 时间戳副本 ＋ hash 文件
// ============================================================================
public static class HH310F15Probe
{
    public const string Tag = "HH310F15";
    private const string Menu = "Valley/审计/HH310 F-15/跑注册表归属探针（正门进局）";
    private const int PROBE_SEED = 29418;              // 与片 6-2/6-3 探针同 seed ⇒ hash 直比历史基线
    private const string PROBE_SLOT = "hh310_f15";
    private const long BASELINE_FEAT_HASH = unchecked((long)0x9424A5D99D9C3543UL);   // 片 6-2/6-3 四连跑（feat+climate）
    private const long BASELINE_ALL_HASH = unchecked((long)0x0A78DF92C7F30B81UL);    // 片 6-3（feat+climate+spawns）

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;
    private static long _hFeat, _hAll;

    [MenuItem(Menu, priority = 213)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 探针已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0;
        Sb.AppendLine("# HH.310 · F-15 修法小批 验证探针（正门 EnterTestRun·seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                      + "｜归属判定＝唯一路径（A/B 对照开关已删·`HH.312` 收尾批）");
        new GameObject("HH310F15ProbeHost").AddComponent<Host>().Go(Run());
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string line)
    {
        Sb.AppendLine(line);
        Debug.Log("[" + Tag + "] " + line);
    }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = PROBE_SEED, mapSeed = PROBE_SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = PROBE_SLOT, kingdomName = "河谷王国"
        };
        Log("── 正门进局（seed=" + PROBE_SEED + " Medium/difficulty=2 · 与片 6-2/6-3 探针同参）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("❌ ActiveMap 不在场 ⇒ 中止"); Finish(); yield break; }

        // §E 判据5：建局即取 hash（早取 ⇒ 免受后续 tick 影响）
        _hFeat = HashMap(map, false);
        _hAll = HashMap(map, true);
        Log("");
        Log("## §E 判据5 零地图变更（本批不碰生成期）");
        Log("§E feat+climate=" + _hFeat.ToString("X16") + " vs 基线 " + BASELINE_FEAT_HASH.ToString("X16")
            + " ⇒ " + (_hFeat == BASELINE_FEAT_HASH ? "逐值不变 ✅" : "❌ 已变"));
        Log("§E feat+climate+spawns=" + _hAll.ToString("X16") + " vs 基线 " + BASELINE_ALL_HASH.ToString("X16")
            + " ⇒ " + (_hAll == BASELINE_ALL_HASH ? "逐值不变 ✅" : "❌ 已变"));
        Log("§E ⚠️ 口径注：清单判据 5 把 `9424A5D99D9C3543` 称作「features＋climateZones＋spawns」hash —— "
            + "**实读为 feat+climate 两口径**；加 spawns 后＝0A78DF92C7F30B81（片 6-3 证据）。两列同报。");

        var reg = BuildingRegistry.Instance;
        if (reg == null) { Log("❌ BuildingRegistry 不在场 ⇒ 中止"); Finish(); yield break; }
        int live = 0;
        for (int i = 0; i < reg.All.Count; i++) if (reg.All[i] != null) live++;
        Log("§E 对照：BuildingRegistry 总数=" + live + "（片 6-2/6-3 读数 21 ⇒ " + (live == 21 ? "逐值不变 ✅" : "⚠️ 不同（如实列报）") + "）");

        var def = Resources.Load<BuildingDef>("Buildings/wall");
        if (def == null) { Log("❌ Buildings/wall 不在场 ⇒ 中止"); Finish(); yield break; }
        Log("构造用 def：id=" + def.id + "｜role=" + def.role + "｜footprint=" + def.footprint
            + "｜isObstacle=" + def.isObstacle + "（AttachComponents 零挂件：producer.rate=" + def.producer.rate
            + "／combat.attack=" + def.combat.attack + "／isConsumable=" + def.isConsumable + "）");

        var spot2x2 = FindFreeBlock(2, 2);
        if (spot2x2.x < 0) { Log("❌ 找不到 2×2 自由块 ⇒ 构造类判据无法测（如实列报）"); Finish(); yield break; }
        Log("构造区·2×2 自由块 origin=" + spot2x2 + "（用于 §D 与 §B）");

        Case_D_NoDisturb(reg, def, spot2x2);   // §D 先做（此时在册登记表未被探针触碰）

        var spot = FindFreeBlock(3, 3);
        if (spot.x < 0) { Log("❌ 找不到 3×3 自由块 ⇒ 构造类判据无法测（如实列报）"); Finish(); yield break; }
        Log("");
        Log("## 构造区（实测自由块·远离在册建筑）");
        Log("构造区·3×3 origin=" + spot + "（3×3 全格：GetAt==null ∧ GetOccupant==null ∧ 无 BuildingBlocked 位）");

        Case_A_Literal(reg, def, spot);        // §A 构①（字面）
        Case_A_Offset(reg, def, spot);         // §A 构②（补全）（§C 已随开关删除）
        Case_B_Single(reg, def, spot);         // §B 判据2
        Case_F_GridKnownLimit(reg, def, spot); // §F 判据7（已知限制·同报）

        VerifyCleanup("3×3 区", spot);
        VerifyCleanup("2×2 区", spot2x2);
        Finish();
    }

    // ========================================================================
    //  §A 构①：字面（同一 coord ＋ 同一 footprint）
    // ========================================================================
    private static void Case_A_Literal(BuildingRegistry reg, BuildingDef def, GridCoord o)
    {
        Log("");
        Log("## §A 判据1 用例 A·构①（字面：同一 coord ＋ 同一 footprint·2×2 ⇒ **全格皆共享格**）");
        var a = Make(def, o, new Vector2Int(2, 2));
        var b = Make(def, o, new Vector2Int(2, 2));
        if (a == null || b == null) { Log("❌ 构造失败"); return; }
        Log("§A-① A=B#" + a.GetInstanceID() + "（先注册）｜B=B#" + b.GetInstanceID() + "（后注册）｜同 coord=" + o + " 同 footprint=2×2");

        var cells = Footprint(o, 2, 2);
        Log("§A-① 建后登记事实（后写者胜）：" + CellsStr(reg, cells) + " ⇒ "
            + (AllEqual(reg, cells, b) ? "全格 == B ✅ 后注册者胜" : "❌"));

        reg.Unregister(a);
        Log("§A-①(a) GetAt(A 独占格)：**无 A 独占格**（同 coord 同 footprint ⇒ 全 " + cells.Length
            + " 格共享）⇒ 该列在本构造下不存在（如实列报·见构②）");
        Log("§A-①(b) GetAt(共享格)=" + CellsStr(reg, cells) + " ⇒ " + (AllEqual(reg, cells, b) ? "== B ✅" : "❌ 未命中 B"));
        Log("§A-①(c) GetAt(B 独占格)：**无 B 独占格**（同上）⇒ 该列在本构造下不存在（如实列报·见构②）");
        Log("§A-① 旁注：A ∈ All=" + InAll(reg, a) + "（`_all.Remove(b)` 只删自己·未动）｜B ∈ All=" + InAll(reg, b));

        reg.Unregister(b);
        Log("§A-①(d) Unregister(B) 后 GetAt(共享格)=" + CellsStr(reg, cells) + " ⇒ "
            + (AllNull(reg, cells) ? "== null ✅（B 是最后写入者 ⇒ 应被清）" : "❌ 未清净"));
        Kill(a); Kill(b);
    }

    // ========================================================================
    //  §A 构②：补全（错位 1 格 ⇒ A 独占 / 共享 / B 独占 三格类）
    //  （原 §C 鉴别力自证段已随 A/B 开关删除·`HH.312` 收尾批）
    // ========================================================================
    private static void Case_A_Offset(BuildingRegistry reg, BuildingDef def, GridCoord o)
    {
        Log("");
        Log("## §A 判据1 用例 A·构②（补全：同 footprint·B 错位 1 格 ⇒ A 独占/共享/B 独占 三格类）");
        Log("几何：A@(" + o.x + "," + o.y + ") 2×2｜B@(" + (o.x + 1) + "," + o.y + ") 2×2");

        var shared = new[] { new GridCoord(o.x + 1, o.y), new GridCoord(o.x + 1, o.y + 1) };
        var aOnly = new[] { new GridCoord(o.x, o.y), new GridCoord(o.x, o.y + 1) };
        var bOnly = new[] { new GridCoord(o.x + 2, o.y), new GridCoord(o.x + 2, o.y + 1) };

        // ---- 单段：现行语义（归属判定）----
        var aOff = Make(def, o, new Vector2Int(2, 2));
        var bOff = Make(def, new GridCoord(o.x + 1, o.y), new Vector2Int(2, 2));
        if (aOff == null || bOff == null) { Log("❌ 构造失败"); return; }
        Log("§A-② A=B#" + aOff.GetInstanceID() + "（先注册）｜B=B#" + bOff.GetInstanceID() + "（后注册）");
        Log("§A-② 建后：A 独占=[" + CellsStr(reg, aOnly) + "]｜共享=[" + CellsStr(reg, shared)
            + "]｜B 独占=[" + CellsStr(reg, bOnly) + "] ⇒ "
            + (AllEqual(reg, aOnly, aOff) && AllEqual(reg, shared, bOff) && AllEqual(reg, bOnly, bOff) ? "✅ 与「后写者胜」自洽" : "❌"));

        reg.Unregister(aOff);
        bool offA1 = AllNull(reg, aOnly);          // (a)
        bool offA2 = AllEqual(reg, shared, bOff);  // (b)
        bool offA3 = AllEqual(reg, bOnly, bOff);   // (c)
        Log("§A-②(a) Unregister(A) 后 GetAt(A 独占格)=[" + CellsStr(reg, aOnly) + "] ⇒ 必须 == null ⇒ " + (offA1 ? "✅" : "❌"));
        Log("§A-②(b) GetAt(共享格)=[" + CellsStr(reg, shared) + "] ⇒ 必须 == B ⇒ " + (offA2 ? "✅ == B" : "❌ 未命中 B"));
        Log("§A-②(c) GetAt(B 独占格)=[" + CellsStr(reg, bOnly) + "] ⇒ " + (offA3 ? "== B ✅" : "❌"));
        reg.Unregister(bOff);
        bool offA4 = AllNull(reg, shared);
        Log("§A-②(d) 继 Unregister(B) 后 GetAt(共享格)=[" + CellsStr(reg, shared) + "] ⇒ 必须 == null ⇒ " + (offA4 ? "✅（B 是最后写入者）" : "❌"));
        Kill(aOff); Kill(bOff);

        // §C 判据3 鉴别力自证 已随 A/B 开关删除（HH.312 收尾批）——改前读数落盘 HH.311 报告 §三／Logs/hh310_f15/
    }

    // ========================================================================
    //  §B 判据2：无重叠·单座
    // ========================================================================
    private static void Case_B_Single(BuildingRegistry reg, BuildingDef def, GridCoord o)
    {
        Log("");
        Log("## §B 判据2 用例 B（无重叠·单座）⇒ footprint 全格 GetAt 必须 == null（零行为变更）");
        var c = new GridCoord(o.x, o.y + 2);   // 自由块第 3 行（与前述构造不重叠）
        var b = Make(def, c, new Vector2Int(2, 2));
        if (b == null) { Log("❌ 构造失败"); return; }
        var cells = Footprint(c, 2, 2);
        Log("§B 建后：[" + CellsStr(reg, cells) + "] ⇒ " + (AllEqual(reg, cells, b) ? "== 该座 ✅" : "❌"));
        reg.Unregister(b);
        Log("§B Unregister 后：[" + CellsStr(reg, cells) + "] ⇒ " + (AllNull(reg, cells) ? "全格 == null ✅（零行为变更）" : "❌"));
        Kill(b);
    }

    // ========================================================================
    //  §D 判据4：无重叠零扰动（Registry 逐座逐格 GetAt·单段）
    // ========================================================================
    /// <summary>判据 4：无重叠零扰动。⚠️ 本判据**本身无鉴别力**（见下），作业面＝「现行语义是否对**他座**造成附带清除」。
    /// 做法：先在在册 21 座上取签名 S0；再在**自由块**构造非重叠测试座 ⇒ `Unregister` ⇒ 复取签名 S1；
    /// 要求 `diff(S0,S1)` == 0。
    /// ⚠️ **鉴别力声明（`L-30`）**：本判据的 `Unregister` 只触及测试座自身 footprint 的键，
    /// 而归属守卫只在**键冲突**（重叠）时才分叉 ⇒ 本判据**结构上抓不到 1(b) 那个缺陷**。
    /// ⚠️ **§C 已随开关删除（`HH.312` 收尾批）**：改前语义不再可构造 ⇒ 原本由 §C 承担的缺陷鉴别力**本批已无对照面**
    /// ⇒ ⛔ **不得因 §C 删除而把本判据升格声称缺陷判据**（改前读数落盘 `HH.311` 报告 §三）。</summary>
    private static void Case_D_NoDisturb(BuildingRegistry reg, BuildingDef def, GridCoord freeSpot)
    {
        Log("");
        Log("## §D 判据4 无重叠零扰动（在册 21 座签名 × 单段跑一遍真实 Unregister 周期）");
        var s0 = Snapshot(reg, out int c0, out int n0, out int o0, out int sf0);
        Log("§D S0（基线·在册 " + reg.All.Count + " 座）：条目=" + s0.Count + "｜格数=" + c0
            + "｜指向本座=" + sf0 + "｜指向他座=" + o0 + "｜空格=" + n0);

        bool cleared = RunOneCycle(reg, def, freeSpot, s0, out var s1, out int d);

        Log("§D 单段（现行语义）：测试座注销后其 footprint 全格 " + (cleared ? "== null ✅" : "❌ 未清净")
            + "｜在册 21 座签名 diff(S0,S1)=" + d + " ⇒ " + (d == 0 ? "0 ✅ 无附带清除" : "❌ 有附带清除"));
        Log("§D ⚠️ **鉴别力声明（`L-30`）**：本判据的 Unregister 只触及**测试座自身** footprint 的键 ⇒ "
            + "归属守卫（只在**键冲突**处分叉）**结构上抓不到 1(b) 缺陷** ⇒ 本判据不等于缺陷判据；"
            + "⚠️ §C 已随开关删除（改前语义不再可构造）⇒ 本批**无对照面**，⛔ 不得据此升格声称缺陷判据。");
    }

    /// <summary>段内跑一遍：构造非重叠测试座 → Unregister → 复取在册签名 → 清场。返回「测试座全格已清」。</summary>
    private static bool RunOneCycle(BuildingRegistry reg, BuildingDef def, GridCoord o,
                                   List<string> s0, out List<string> s1, out int diff)
    {
        var cells = Footprint(o, 2, 2);
        var t = Make(def, o, new Vector2Int(2, 2));
        if (t == null) { s1 = s0; diff = -1; return false; }
        reg.Unregister(t);
        bool cleared = AllNull(reg, cells);
        s1 = Snapshot(reg, out _, out _, out _, out _);
        diff = 0;
        int n = Mathf.Min(s0.Count, s1.Count);
        for (int i = 0; i < n; i++) if (s0[i] != s1[i]) diff++;
        if (s0.Count != s1.Count) diff = -1;
        Kill(t);
        return cleared;
    }

    private static List<string> Snapshot(BuildingRegistry reg, out int cells, out int nulls, out int others, out int selfs)
    {
        var list = new List<string>();
        cells = 0; nulls = 0; others = 0; selfs = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b == null) continue;
            int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new GridCoord(b.coord.x + dx, b.coord.y + dy);
                    var got = reg.GetAt(c);
                    cells++;
                    if (got == null) { nulls++; list.Add(c.x + "," + c.y + "=null"); }
                    else if (!ReferenceEquals(got, b)) { others++; list.Add(c.x + "," + c.y + "=other#" + got.GetInstanceID()); }
                    else { selfs++; list.Add(c.x + "," + c.y + "=self#" + got.GetInstanceID()); }
                }
        }
        list.Sort(System.StringComparer.Ordinal);
        return list;
    }

    // ========================================================================
    //  §F 判据7：⚠️ 已知限制同报（⛔ 本批不动 GridSystem）
    // ========================================================================
    private static void Case_F_GridKnownLimit(BuildingRegistry reg, BuildingDef def, GridCoord o)
    {
        Log("");
        Log("## §F 判据7 ⚠️ 已知限制同报：GridSystem.GetOccupant(共享格)（⛔ 本批一行未动）");
        var grid = GridSystem.Instance;
        if (grid == null) { Log("§F ❌ GridSystem 不在场"); return; }
        var shared = new GridCoord(o.x + 1, o.y);
        var aOnly = new GridCoord(o.x, o.y);

        // 单列：现行语义
        var a2 = Make(def, o, new Vector2Int(2, 2));
        var b2 = Make(def, new GridCoord(o.x + 1, o.y), new Vector2Int(2, 2));
        if (a2 == null || b2 == null) { Log("§F ❌ 构造失败"); return; }
        Log("§F 建后（两座重叠·后写者胜）：GetOccupant(共享格)=" + Name(grid.GetOccupant(shared))
            + "｜GetOccupant(A 独占格)=" + Name(grid.GetOccupant(aOnly)));
        reg.Unregister(a2);
        var occ = grid.GetOccupant(shared);
        Log("§F **单列（现行语义）**：Unregister(A) 后 GetOccupant(共享格)=" + Name(occ));

        // §F 改前语义（A 列）已随 A/B 开关删除（HH.312 收尾批）——改前读数落盘 HH.311 报告 §三／Logs/hh310_f15/
        Log("§F ⇒ Unregister 不触 GridSystem（`BuildingRegistry` 与 `GridSystem._occupants` 双写、各自独立）"
            + " ⇒ 本批**未改**该面（`03` §6.5 ④「已知限制」照旧登记·不计 FAIL）" + (occ != null ? "" : " ⚠️ 共享格为空"));

        // 已知限制的可读化实证：真删门的占格释放路径（FreeFootprint）无条件清空共享格
        grid.FreeFootprint(o, 2, 2);                      // 模拟「拆 A」的占格释放（无条件）
        var afterFree = grid.GetOccupant(shared);
        Log("§F 已知限制实证（临时·随后复原）：FreeFootprint(A footprint) ⇒ GetOccupant(共享格)=" + Name(afterFree)
            + " ⇒ " + (afterFree == null ? "**静默清掉后注册者的占格** ⇒ 坐实「已知限制·不可恢复」" : "仍非空"));
        grid.MarkOccupiedFootprint(b2.coord, Mathf.Max(1, b2.footprint.x), Mathf.Max(1, b2.footprint.y), b2);
        Log("§F 已复原：GetOccupant(共享格)=" + Name(grid.GetOccupant(shared)));

        reg.Unregister(a2); reg.Unregister(b2);
        Kill(a2); Kill(b2);
    }

    // ========================================================================
    //  工具
    // ========================================================================
    private static Building Make(BuildingDef def, GridCoord c, Vector2Int fp)
    {
        // 走「便捷口」（含实例/配置判空；进局态实例必在 ⇒ fallback 不会被用到，故传 zero 以免就地展开算式）
        var world = GridSystem.FootprintCenterWorld(c, fp, Vector3.zero);
        bool ok = BuildingFactory.Instance != null && BuildingFactory.Instance.CreateBuildingInstance(
            def, BuildingType.None, c, fp, world,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Abandoned, kingdomId: 0);
        if (!ok) return null;
        return BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(c) : null;
    }

    private static bool InAll(BuildingRegistry reg, Building b)
    {
        for (int i = 0; i < reg.All.Count; i++) if (ReferenceEquals(reg.All[i], b)) return true;
        return false;
    }

    /// <summary>清场：注销 ＋ 释放占格 ＋ 销毁（探针自建物不留残留）。</summary>
    private static void Kill(Building b)
    {
        if (b == null) return;
        var reg = BuildingRegistry.Instance;
        if (reg != null) reg.Unregister(b);
        var grid = GridSystem.Instance;
        if (grid != null) grid.FreeFootprint(b.coord, Mathf.Max(1, b.footprint.x), Mathf.Max(1, b.footprint.y));
        Object.Destroy(b.gameObject);
    }

    private static GridCoord[] Footprint(GridCoord o, int w, int h)
    {
        var list = new List<GridCoord>();
        for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) list.Add(new GridCoord(o.x + dx, o.y + dy));
        return list.ToArray();
    }

    private static string CellsStr(BuildingRegistry reg, GridCoord[] cells)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("(").Append(cells[i].x).Append(",").Append(cells[i].y).Append(")=").Append(Name(reg.GetAt(cells[i])));
        }
        return sb.ToString();
    }

    private static string Name(Building b) => b == null ? "null" : ("B#" + b.GetInstanceID());
    private static string Name(IGridOccupant o)
    {
        if (o == null) return "null";
        var uo = o as Object;
        return "occ:" + o.GetType().Name + "#" + (uo != null ? uo.GetInstanceID() : 0);
    }

    private static bool AllEqual(BuildingRegistry reg, GridCoord[] cells, Building b)
    {
        for (int i = 0; i < cells.Length; i++) if (!ReferenceEquals(reg.GetAt(cells[i]), b)) return false;
        return true;
    }
    private static bool AllNull(BuildingRegistry reg, GridCoord[] cells)
    {
        for (int i = 0; i < cells.Length; i++) if (reg.GetAt(cells[i]) != null) return false;
        return true;
    }

    private static GridCoord FindFreeBlock(int w, int h)
    {
        var grid = GridSystem.Instance;
        var reg = BuildingRegistry.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (grid == null || reg == null || map == null) return new GridCoord(-1, -1);
        for (int y = map.height - h - 2; y > 2; y -= 3)          // 从右下角向内扫（远离出生点）
            for (int x = map.width - w - 2; x > 2; x -= 3)
            {
                bool ok = true;
                for (int dy = 0; dy < h && ok; dy++)
                    for (int dx = 0; dx < w && ok; dx++)
                    {
                        var c = new GridCoord(x + dx, y + dy);
                        if (!grid.IsInBounds(c)) { ok = false; break; }
                        if (reg.GetAt(c) != null) { ok = false; break; }
                        if (grid.GetOccupant(c) != null) { ok = false; break; }
                        if ((grid.GetWalkFlags(c) & WalkFlags.BuildingBlocked) != 0) { ok = false; break; }
                    }
                if (ok) return new GridCoord(x, y);
            }
        return new GridCoord(-1, -1);
    }

    private static void VerifyCleanup(string label, GridCoord o)
    {
        Log("");
        Log("## 收尾自检（探针不留残留）·" + label);
        var grid = GridSystem.Instance;
        var reg = BuildingRegistry.Instance;
        int badReg = 0, badOcc = 0;
        for (int dy = -1; dy < 4; dy++)
            for (int dx = -1; dx < 4; dx++)
            {
                var c = new GridCoord(o.x + dx, o.y + dy);
                if (reg != null && reg.GetAt(c) != null) badReg++;
                if (grid != null && grid.GetOccupant(c) != null) badOcc++;
            }
        Log(label + " 构造区 5×5 邻域残留：登记表命中=" + badReg + "｜GridSystem 占格命中=" + badOcc
            + " ⇒ " + ((badReg == 0 && badOcc == 0) ? "0 / 0 ✅ 无残留" : "❌ 有残留（如实列报）"));
        int live = 0;
        if (reg != null) for (int i = 0; i < reg.All.Count; i++) if (reg.All[i] != null) live++;
        Log(label + " BuildingRegistry 总数（收尾）=" + live);
    }

    // ========================================================================
    //  hash（逐字照搬片 6-3 探针实现 ⇒ 与历史基线可直接比对）
    // ========================================================================
    private static long HashMap(MapData map, bool withSpawns)
    {
        if (map == null || map.features == null) return 0;
        unchecked
        {
            ulong h1 = 14695981039346656037UL;
            ulong h2 = 0;
            for (int i = 0; i < map.features.Length; i++)
            {
                h1 = (h1 ^ (ulong)(int)map.features[i]) * 1099511628211UL;
                h2 += (ulong)(int)map.features[i];
            }
            if (map.climateZones != null)
                for (int i = 0; i < map.climateZones.Length; i++)
                {
                    h1 = (h1 ^ ((ulong)(int)map.climateZones[i] + 7UL)) * 1099511628211UL;
                    h2 += (ulong)(int)map.climateZones[i] * 31UL;
                }
            if (withSpawns)
            {
                if (map.kingdomSpawns != null)
                    for (int i = 0; i < map.kingdomSpawns.Count; i++)
                    {
                        var k = map.kingdomSpawns[i];
                        h1 = (h1 ^ (ulong)(uint)(k.x * 313 + k.y * 317 + i)) * 1099511628211UL;
                    }
                if (map.threatSpawns != null)
                    for (int i = 0; i < map.threatSpawns.Count; i++)
                    {
                        var s = map.threatSpawns[i];
                        h1 = (h1 ^ (ulong)(uint)(s.coord.x * 131 + s.coord.y * 137 + (int)s.faction * 977 + s.strength * 17)) * 1099511628211UL;
                        h2 += (ulong)(uint)((int)(s.direction.x * 10000f) * 199 + (int)(s.direction.y * 10000f) * 211);
                    }
            }
            return (long)(h1 ^ (h2 << 1));
        }
    }

    // ========================================================================
    //  收尾（L-32）＋ 落盘
    // ========================================================================
    private static void Finish()
    {
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(PROBE_SLOT);
        TestHarnessApi.ExitTestRun();

        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + PROBE_SLOT + "）→ 退 Play（L-32 条文1）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh310_f15");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh310_probe.txt");
            File.WriteAllText(stable, Sb.ToString());
            string stamped = Path.Combine(dir, "hh310_probe_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(stamped, Sb.ToString());
            File.WriteAllText(Path.Combine(dir, "hh310_hash.txt"),
                "feat=" + _hFeat.ToString("X16") + " all=" + _hAll.ToString("X16") + " seed=" + PROBE_SEED
                + " at " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本 ＋ hash");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}
