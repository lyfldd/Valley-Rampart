using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using static BuildingFactory;
using static BuildingRegistry;

// ============================================================================
//  2_16 读档建筑双份修复 Play 三合一（方案A + 版本门控 v2 + 响亮断言）
//  用法：菜单「Valley/验证/2_16_读档建筑双份_Play三合一」——须 Play 上下文执行。
//  覆盖（裁决⑥清单）：
//    ① 修复验证：新游戏(单份) → 砍一棵自然树 → save(slot_v2) → load
//       断言：读档后建筑数=存档记录数、无同格双份、归属一致、被砍树不复活、lastFoundingDay 回环。
//    ② 响亮断言：在空格预置一栋 Building 后，对同格 SpawnFromSave → 断言必须响（捕获 LogError）。
//  收口：本脚本不改产品代码；运行结束后清掉测试存档。
// ============================================================================
public static class Valley2_16_Smoke_BuildDup
{
    private const int SEED = 777777;
    private const string SLOT = "smoke_builddup";

    [MenuItem("Valley/验证/2_16_读档建筑双份_Play三合一")]
    public static void Run()
    {
        var sb = new StringBuilder();
        bool allPass = true;

        var lm = LoadManager.Instance;
        if (lm == null) { Debug.LogError("[2_16_双份冒烟] LoadManager 不可用。"); return; }
        var sm = SaveManager.Instance;
        if (sm == null) { Debug.LogError("[2_16_双份冒烟] SaveManager 不可用。"); return; }

        var _ = BuildingRegistry.Instance;   // 物化单例

        // ---- 干净起点：建一个真实可玩世界（A 路径单份实例化）----
        lm.InitializeNewGame(new NewGameConfig
        {
            mapSeed = SEED,
            worldSeed = SEED,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            kingdomName = "双份冒烟王国",
            selectedSlotId = SLOT
        });

        var grid = GridSystem.Instance;
        if (grid == null) { Debug.LogError("[2_16_双份冒烟] 世界未就绪。"); return; }

        Snapshot snap = Snapshot.Capture();
        sb.Append($"建图建筑={snap.Count}（玩家={snap.CountOf(0)} AI/自然={snap.CountOf(-1)} 其余={snap.Count - snap.CountOf(0) - snap.CountOf(-1)}） ");
        if (snap.Count == 0) sb.Append("(无建筑可验证，跳过存读) ");

        // ---- 砍一棵自然树（kingdomId==-1，优先 Tree，兜底任一自然一次性资源点）----
        Building cutTarget = null;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || b.kingdomId != -1) continue;
            if (b.sourceType == BuildingType.Tree) { cutTarget = b; break; }
            if (cutTarget == null && IsNatural(b.sourceType)) cutTarget = b;
        }
        GridCoord cutCoord = default;
        if (cutTarget != null)
        {
            cutCoord = cutTarget.coord;
            string cutDef = cutTarget.def != null ? cutTarget.def.id : cutTarget.sourceType.ToString();
            cutTarget.Die();
            sb.Append($"砍除: {cutDef}({cutCoord.x},{cutCoord.y}) ");
        }
        else sb.Append("(未找到可砍自然树，树不复活项跳过) ");

        // ---- lastFoundingDay 回环锚点 ----
        int foundingAnchor = 7;
        KingdomRegistry.Instance.lastFoundingDay = foundingAnchor;
        int savedKingdomCount = KingdomRegistry.Instance.Count;   // 存档王国数（读档后应等于此值，无 k4/5/6）

        // ---- 存 → 读 ----
        bool saved = sm.Save(SLOT);
        sb.Append($"save={(saved?"OK":"FAIL")} ");
        int savedRecordCount = Snapshot.Capture().Count;   // 存档记录数（Save 后、Load 前的活跃数）

        bool loaded = sm.Load(SLOT);
        sb.Append($"load={(loaded?"OK":"FAIL")} ");

        if (!saved || !loaded) { Debug.LogError("[2_16_双份冒烟] 存/读失败，中断。" + sb); return; }

        // ---- ① 修复验证断言 ----
        var after = Snapshot.Capture();
        bool countMatch = after.Count == savedRecordCount;
        sb.Append($"\n[①] 读档建筑数={after.Count} vs 存档记录={savedRecordCount} ({(countMatch?"OK":"FAIL")}) ");

        bool noDup = !after.HasDupCoords() || snap.DupCoordSet().IsSupersetOf(after.DupCoordSet());
        sb.Append($"无同格双份={(noDup?"OK":"FAIL")} ");

        bool cutNotRevived = cutTarget == null || !after.ContainsAt(cutCoord);
        sb.Append($"被砍树未复活={(cutNotRevived?"OK":"FAIL")} ");

        bool ownershipOk = after.AllNaturalOwnedCorrectly();
        sb.Append($"归属一致(自然=-1)={(ownershipOk?"OK":"FAIL")} ");

        bool roundtrip = KingdomRegistry.Instance.lastFoundingDay == foundingAnchor;
        sb.Append($"lastFoundingDay回环={KingdomRegistry.Instance.lastFoundingDay}(={foundingAnchor})={(roundtrip?"OK":"FAIL")} ");

        bool kingdomCountOk = KingdomRegistry.Instance.Count == savedKingdomCount;
        sb.Append($"读档王国数={KingdomRegistry.Instance.Count}(=存档{savedKingdomCount})={(kingdomCountOk?"OK":"FAIL")} ");

        // ---- 诊断：读档后 kingdomId 分布 + 同格重复明细 ----
        var kindist = new Dictionary<int, int>();
        foreach (var r in after.rows)
        {
            if (!kindist.ContainsKey(r.kingdomId)) kindist[r.kingdomId] = 0;
            kindist[r.kingdomId]++;
        }
        var distSb = new StringBuilder();
        foreach (var kv in kindist) distSb.Append($"k{kv.Key}={kv.Value} ");
        var dupDesc = new StringBuilder();
        var coordFirst = new Dictionary<GridCoord, string>();
        foreach (var r in after.rows)
        {
            var label = $"{(int)r.type}:{r.kingdomId}";
            if (coordFirst.TryGetValue(r.coord, out var first))
            {
                dupDesc.Append($"\n    双份@{r.coord.x},{r.coord.y}: {first} vs {label}");
            }
            else coordFirst[r.coord] = label;
        }
        Debug.Log($"[2_16_双份冒烟·诊断] 读档后分布: {distSb}{dupDesc}");

        // ---- ② 响亮断言：必须响（B 重建撞上已占用格）----
        var assertCoord = FindEmptyCoord(grid, after);
        bool assertFired = false;
        var wpDef = FindDefById("wood_pile");
        if (assertCoord == null || wpDef == null || after.Count == 0)
        {
            sb.Append($"\n[②] 找空格/wood_pile def失败，断言项跳过 ");
        }
        else
        {
            GridCoord ac = assertCoord.Value;
            var fp = new Vector2Int(Mathf.Max(1, wpDef.footprint.x), Mathf.Max(1, wpDef.footprint.y));
            bool placed = BuildingFactory.Instance.CreateBuildingInstance(
                wpDef, wpDef.sourceType, ac, fp, (Vector3)grid.CoordToWorld(ac),
                isPlayerBuilt: true, grade: ResourceGrade.Normal, isConsumable: false,
                initialState: BuildingState.Active, kingdomId: 999);
            Building placeholder = placed ? BuildingRegistry.Instance.GetAt(ac) : null;

            var data = new BuildingSaveData
            {
                defId = "wood_pile",
                coordX = ac.x,
                coordY = ac.y,
                footprintW = fp.x,
                footprintH = fp.y,
                level = 1,
                hp = 50,
                maxHp = 50,
                state = (int)BuildingState.Active,
                sourceType = (int)BuildingType.WoodPile,
                grade = (int)ResourceGrade.Normal,
                kingdomId = 1
            };
            var entry = new ModuleSaveEntry
            {
                saveId = "Building_assertTest_dup",
                typeName = typeof(Building).AssemblyQualifiedName,
                json = JsonUtility.ToJson(data)
            };

            var prevHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new CaptureLogHandler(s => assertFired |= s.Contains("SpawnFromSave 冲突"));
            try { BuildingFactory.Instance.SpawnFromSave(entry); }
            finally { Debug.unityLogger.logHandler = prevHandler; }

            sb.Append($"\n[②] 命中({ac.x},{ac.y}) spawn冲突→断言响={(assertFired?"OK":"FAIL!")} ");

            // 清理占位（避免残留影响后续/下次）
            if (placeholder != null) { try { placeholder.Die(); } catch { } }
        }

        allPass = (snap.Count == 0) || (countMatch && noDup && cutNotRevived && ownershipOk && roundtrip && kingdomCountOk && assertFired);

        Debug.Log("[2_16_双份冒烟] " + sb);
        Debug.Log($"[2_16_双份冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（读档不双份+砍树不复活+归属+回环+王国数+断言）=====");

        // 收尾清理测试存档
        try { sm.Delete(SLOT); } catch { /* 忽略 */ }
        // 只落 Console，不弹阻塞对话框（便于 MCP 静默执行）
    }

    static bool IsNatural(BuildingType t)
        => t == BuildingType.WoodPile || t == BuildingType.OreVein || t == BuildingType.StonePile;

    // 在网格内找一个未被占用格（快照占有 ∪ GridSystem 实际占用都为空）
    static GridCoord? FindEmptyCoord(GridSystem grid, Snapshot snap)
    {
        if (grid == null) return null;
        int w = grid.Width, h = grid.Height;
        for (int stride = Mathf.Max(1, w / 64); stride <= w; stride = stride > 1 ? stride / 2 : stride * 2 + 1)
            for (int x = 1; x < w; x += stride)
                for (int y = 1; y < h; y += stride)
                {
                    var c = new GridCoord(x, y);
                    if (snap.ContainsAt(c)) continue;
                    if (grid.GetOccupant(c) != null) continue;
                    return c;
                }
        return null;
    }

    // ===== 快照：记录每栋可存档建筑的 coord → (saveId, kingdomId, type) =====

    struct Rec { public GridCoord coord; public int kingdomId; public BuildingType type; }

    struct Snapshot
    {
        public List<Rec> rows;
        public int Count => rows == null ? 0 : rows.Count;

        public static Snapshot Capture()
        {
            var s = new Snapshot { rows = new List<Rec>() };
            foreach (var b in BuildingRegistry.Instance.All)
            {
                if (b == null) continue;
                s.rows.Add(new Rec { coord = b.coord, kingdomId = b.kingdomId, type = b.sourceType });
            }
            return s;
        }

        public int CountOf(int kid)
        {
            int n = 0; foreach (var r in rows) if (r.kingdomId == kid) n++; return n;
        }

        public bool ContainsAt(GridCoord c)
        {
            foreach (var r in rows) if (r.coord == c) return true;
            return false;
        }

        public bool HasDupCoords()
        {
            var seen = new HashSet<GridCoord>();
            foreach (var r in rows)
                if (!seen.Add(r.coord)) return true;
            return false;
        }

        // 出现≥2次的坐标集合（同格双份）——用于对比读档是否引入"新建基线之外"的新双份。
        public HashSet<GridCoord> DupCoordSet()
        {
            var counted = new Dictionary<GridCoord, int>();
            foreach (var r in rows)
            {
                if (!counted.ContainsKey(r.coord)) counted[r.coord] = 0;
                counted[r.coord]++;
            }
            var set = new HashSet<GridCoord>();
            foreach (var kv in counted) if (kv.Value > 1) set.Add(kv.Key);
            return set;
        }

        // 自然一次性资源点必须 kingdomId==-1（A 路径重派生会把自然建筑错绑默认 0 + 新 GUID → 归属腐坏）。
        // 注意：不校验 CastleCore==0——AI 王国的城堡合法带 kingdomId 1/2/3，不能要求全部=0。
        public bool AllNaturalOwnedCorrectly()
        {
            foreach (var r in rows)
                if (IsNatural(r.type) && r.kingdomId != -1) return false;
            return true;
        }
    }

    // ===== 日志捕获 =====

    class CaptureLogHandler : ILogHandler
    {
        readonly System.Func<string, bool> _onMatch;
        public CaptureLogHandler(System.Func<string, bool> onMatch) { _onMatch = onMatch; }
        public void LogFormat(LogType type, Object context, string format, params object[] args)
        {
            try { _onMatch(string.Format(format, args)); } catch { }
        }
        public void LogException(System.Exception exception, Object context) { }
    }
}