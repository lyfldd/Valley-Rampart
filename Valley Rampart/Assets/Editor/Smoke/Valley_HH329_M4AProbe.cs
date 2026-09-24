#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>`HH.329` `M4-A` 验证探针（Editor · ⛔ 不写资产 · 只读 def）：
///  **Part 1（静态面 · Edit/Play 均可）**：逐 def 对照「改前 9 处 if 推出的组件类型序列」vs
///        「改后 `def.components` 数据行 + 键表推出的类型序列」⇒ 验**数据行迁移**等价；
///        并自检数据行里的每个键都在 `BuildingComponentRegistry` 已登记。
///  **Part 1b**：运行时来源 = `BuildingType.None`（玩家建造 `Building.Init` / 调试路径）下的同款对照。
///  **Part 2（运行面 · ⚠️ 需 Play 模式）**：真实 `BuildingFactory.AttachComponents` 挂载 ⇒
///        「改前 if 链实挂集合」vs「改后数据行实挂集合」逐栋对照；＋ `_tickInterval` 读值
///        ＋ `ProducerComponent` 逐 Tick 产水（水井免工路径）。
/// 公共口 `L`／`FindDef`／`InvokeTickAll` 亦供 `Valley_HH329_TickTestProbe`（判据 2 一次性测试件）复用。</summary>
public static class Valley_HH329_M4AProbe
{
    private static StringBuilder _sb = new StringBuilder();
    private static string _log;

    /// <summary>键 → 组件类名（**探针侧**映射；Part 2 以真实挂载结果为准交叉验）。</summary>
    private static readonly Dictionary<string, string> KeyType = new Dictionary<string, string>
    {
        { "comp.storage",         "StorageComponent" },
        { "comp.producer",        "ProducerComponent" },
        { "comp.blacksmith",      "BlacksmithBuilding" },
        { "comp.siege_workshop",  "SiegeWorkshopBuilding" },
        { "comp.mine_byproduct",  "MineByproductComponent" },
        { "comp.combat",          "CombatComponent" },
        { "comp.pickup",          "PickupComponent" },
        { "comp.rift",            "RiftComponent" },
        { "comp.castle_core",     "CastleCoreComponent" },
    };

    [MenuItem("Valley/验证/HH329 M4-A 数据化等价 + tick")]
    public static void Run()
    {
        _sb = new StringBuilder();
        var root = Directory.GetParent(Application.dataPath).Parent.FullName;
        _log = Path.Combine(root, "Logs", "hh329_m4a_probe.log");
        L("# HH.329 M4-A 探针 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
          + " · isPlaying=" + Application.isPlaying);
        L("键表：探针侧 " + KeyType.Count + " 键／生产侧 BuildingComponentRegistry.Count = "
          + BuildingComponentRegistry.Count);

        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        Array.Sort(defs, (x, y) => string.CompareOrdinal(x != null ? x.id : "", y != null ? y.id : ""));
        L("Resources/Buildings 载入 def 数 = " + defs.Length);

        L("");
        L("=== Part 1 数据面（改前 if 链推出的类型序列 vs 改后数据行推出的类型序列）===");
        int same = 0, diff = 0, unknownKey = 0;
        var diffIds = new List<string>();
        var seqOnly = new List<string>();
        foreach (var def in defs)
        {
            if (def == null) continue;
            var before = OldTypes(def, def.sourceType);
            var after = RowTypes(def, def.sourceType, ref unknownKey);
            bool eqSet = SameSet(before, after);
            bool eqSeq = SameSeq(before, after);
            if (eqSet) same++; else { diff++; diffIds.Add(def.id); }
            if (eqSet && !eqSeq) seqOnly.Add(def.id);
            L(string.Format("  {0,-18} src={1,-2} 行=[{2}]  改前=[{3}]  改后=[{4}]  {5}",
                def.id, (int)def.sourceType,
                def.components != null ? string.Join(",", def.components) : "",
                string.Join(", ", before), string.Join(", ", after),
                !eqSet ? "❌ 不同" : (eqSeq ? "同 ✅" : "⚠️ 集合同·顺序不同")));
        }
        L(string.Format("  —— 合计 {0} 栋：相同 {1} ／ 不同 {2} {3}{4}；未知键 {5}",
            same + diff, same, diff, diff == 0 ? " ✅" : " ❌ [" + string.Join(", ", diffIds) + "]",
            seqOnly.Count == 0 ? "；顺序亦逐栋相同 ✅" : "；仅顺序不同：" + string.Join(", ", seqOnly),
            unknownKey));

        L("");
        L("=== Part 1b 运行时来源 = BuildingType.None（玩家建造 / 调试路径）===");
        foreach (var id in new[] { "castle", "rift", "portal", "mine", "Warehouse", "SiegeWorkshop", "Well", "Blacksmith" })
        {
            var d = BuildingFactory.FindDefById(id);
            if (d == null) { L("  " + id + " 未找到"); continue; }
            var before = OldTypes(d, BuildingType.None);
            var after = RowTypes(d, BuildingType.None, ref unknownKey);
            bool eqSet = SameSet(before, after);
            if (eqSet) same++; else { diff++; diffIds.Add(id + "@None"); }
            L(string.Format("  {0,-16} src=None 改前=[{1}]  改后=[{2}]  {3}", id,
                string.Join(", ", before), string.Join(", ", after),
                eqSet ? "同 ✅" : "❌ 不同"));
        }

        L("");
        if (!Application.isPlaying)
        {
            L("=== Part 2 跳过（运行面需 Play 模式：`Singleton.Instance` 隐式创建在 Edit 模式会抛 DontDestroyOnLoad）===");
        }
        else
        {
            L("=== Part 2 运行面（真实 AttachComponents 挂载 · 逐 def 实挂集合对照）===");
            int rSame = 0, rDiff = 0;
            var rDiffIds = new List<string>();
            var rSeq = new List<string>();
            foreach (var def in defs)
            {
                if (def == null) continue;
                var before = RunOldChain(def, def.sourceType);
                var after = RunNewPath(def, def.sourceType);
                bool eqSet = SameSet(before, after);
                bool eqSeq = SameSeq(before, after);
                if (eqSet) rSame++; else { rDiff++; rDiffIds.Add(def.id); }
                if (eqSet && !eqSeq) rSeq.Add(def.id);
                L(string.Format("  {0,-18} src={1,-2} 实挂改前=[{2}]  实挂改后=[{3}]  {4}",
                    def.id, (int)def.sourceType, string.Join(", ", before), string.Join(", ", after),
                    !eqSet ? "❌ 不同" : (eqSeq ? "同 ✅" : "⚠️ 集合同·顺序不同")));
            }
            L(string.Format("  —— 合计 {0} 栋：相同 {1} ／ 不同 {2} {3}{4}",
                rSame + rDiff, rSame, rDiff, rDiff == 0 ? " ✅" : " ❌ [" + string.Join(", ", rDiffIds) + "]",
                rSeq.Count == 0 ? "；顺序亦逐栋相同 ✅" : "；仅顺序不同：" + string.Join(", ", rSeq)));

            L("");
            L("=== Part 2b `_tickInterval` ＋ 逐 Tick 产水（水井 · `_isWell` 免工自产）===");
            var ps = ProductionSystem.Instance;
            var fld = typeof(ProductionSystem).GetField("_tickInterval", BindingFlags.NonPublic | BindingFlags.Instance);
            L("  `_tickInterval` = " + (fld != null ? fld.GetValue(ps) : "n/a") + "（本批未动 `Update`/该字段 ⇒ 与改前同值）");
            var well = BuildingFactory.FindDefById("Well");
            var go = new GameObject("HH329_Well");
            var b = go.AddComponent<Building>();
            b.def = well; b.sourceType = well.sourceType; b.state = BuildingState.Active;
            b.coord = new GridCoord(0, 0); b.footprint = Vector2Int.one;
            BuildingFactory.Instance.AttachComponents(b, well);
            var st = go.GetComponent<StorageComponent>();
            var prod = go.GetComponent<ProducerComponent>();
            L("  Well 行=[" + string.Join(",", well.components) + "] ⇒ Storage=" + (st != null)
              + " Producer=" + (prod != null) + " 仓容=" + (st != null ? st.capacity.ToString() : "n/a"));
            if (st != null)
            {
                BuildingRegistry.Instance.Register(b);
                int c0 = st.GetAmount(ResourceType.Water);
                InvokeTickAll(); int c1 = st.GetAmount(ResourceType.Water);
                InvokeTickAll(); int c2 = st.GetAmount(ResourceType.Water);
                L(string.Format("  Water：初始 {0} → 1×TickAll {1}（Δ{2}）→ 2×TickAll {3}（Δ{4}）",
                    c0, c1, c1 - c0, c2, c2 - c1));
                BuildingRegistry.Instance.Unregister(b);
            }
            UnityEngine.Object.DestroyImmediate(go);
        }

        File.WriteAllText(_log, _sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[HH329] 探针完成 · 读数落盘 " + _log);
        AssetDatabase.Refresh();
    }

    // ===== 公共口（供判据 2 测试件复用）=====

    public static void L(string s) { if (_sb == null) _sb = new StringBuilder(); _sb.AppendLine(s); }
    public static string LogPath => _log;
    public static BuildingDef FindDef(string id) => BuildingFactory.FindDefById(id);

    /// <summary>反射调 `ProductionSystem.TickAll()`（⛔ 不改生产码 · 与 `Update` 内同一条路径）。</summary>
    public static void InvokeTickAll()
    {
        var ps = ProductionSystem.Instance;
        if (ps == null) { L("  ⛔ ProductionSystem.Instance 为空"); return; }
        var m = typeof(ProductionSystem).GetMethod("TickAll", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { L("  ⛔ 未找到 TickAll"); return; }
        m.Invoke(ps, null);
    }

    // ===== 对照用：改前/改后 类型序列 =====

    /// <summary>改前 9 处 if 的**纯逻辑推导**（⛔ 不挂组件 · 无副作用）。</summary>
    private static List<string> OldTypes(BuildingDef def, BuildingType src)
    {
        var l = new List<string>();
        bool econStorageOnly = def.producer.rate <= 0f && def.producer.capacity > 0
            && def.role == BuildingRole.Economy
            && def.outputResource != ResourceType.Gold;
        if (econStorageOnly) l.Add("StorageComponent");
        if (def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource && !def.isResourceNode)
        {
            if (def.isSiegeWorkshop) l.Add("SiegeWorkshopBuilding");
            else
            {
                l.Add("StorageComponent");
                l.Add(def.isBlacksmith ? "BlacksmithBuilding" : "ProducerComponent");
            }
        }
        if (def.isMineByproduct) l.Add("MineByproductComponent");
        if (def.combat.attack > 0) l.Add("CombatComponent");
        if (def.isConsumable) l.Add("PickupComponent");
        if (src == BuildingType.Rift) l.Add("RiftComponent");
        if (src == BuildingType.CastleCore) l.Add("CastleCoreComponent");
        return l;
    }

    /// <summary>改后：由 `def.components` 数据行（＋ 两个来源守卫）推出的类型序列。</summary>
    private static List<string> RowTypes(BuildingDef def, BuildingType src, ref int unknownKey)
    {
        var l = new List<string>();
        if (def.components == null) return l;
        for (int i = 0; i < def.components.Length; i++)
        {
            var k = def.components[i];
            if (string.IsNullOrEmpty(k)) continue;
            if (k == BuildingComponentRegistry.Rift && src != BuildingType.Rift) continue;
            if (k == BuildingComponentRegistry.CastleCore && src != BuildingType.CastleCore) continue;
            if (KeyType.TryGetValue(k, out var t)) l.Add(t);
            else { l.Add("?未知键:" + k); unknownKey++; }
        }
        return l;
    }

    /// <summary>改前 9 处 if 的**实挂复刻**（仅 Play 模式跑 · ⛔ 非生产码）。</summary>
    private static List<string> RunOldChain(BuildingDef def, BuildingType src)
    {
        var go = new GameObject("HH329_old_" + def.id);
        var b = go.AddComponent<Building>();
        b.def = def; b.sourceType = src; b.state = BuildingState.Active;
        b.coord = new GridCoord(0, 0); b.footprint = Vector2Int.one;

        bool econStorageOnly = def.producer.rate <= 0f && def.producer.capacity > 0
            && def.role == BuildingRole.Economy
            && def.outputResource != ResourceType.Gold;
        if (econStorageOnly) b.gameObject.AddComponent<StorageComponent>()?.Init(b);
        if (def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource && !def.isResourceNode)
        {
            if (def.isSiegeWorkshop) b.gameObject.AddComponent<SiegeWorkshopBuilding>()?.Init(b);
            else
            {
                b.gameObject.AddComponent<StorageComponent>()?.Init(b);
                if (def.isBlacksmith) b.gameObject.AddComponent<BlacksmithBuilding>()?.Init(b);
                else b.gameObject.AddComponent<ProducerComponent>()?.Init(b);
            }
        }
        if (def.isMineByproduct) b.gameObject.AddComponent<MineByproductComponent>()?.Init(b);
        if (def.combat.attack > 0) b.gameObject.AddComponent<CombatComponent>()?.Init(b);
        if (def.isConsumable) b.gameObject.AddComponent<PickupComponent>()?.Init(b);
        if (src == BuildingType.Rift) b.gameObject.AddComponent<RiftComponent>()?.Init(b);
        if (src == BuildingType.CastleCore) b.gameObject.AddComponent<CastleCoreComponent>()?.Init(b);

        var list = Collect(go);
        UnityEngine.Object.DestroyImmediate(go);
        return list;
    }

    /// <summary>改后路径：走生产码 `AttachComponents`（读 `def.components` ⇒ 查键表挂载）。</summary>
    private static List<string> RunNewPath(BuildingDef def, BuildingType src)
    {
        var go = new GameObject("HH329_new_" + def.id);
        var b = go.AddComponent<Building>();
        b.def = def; b.sourceType = src; b.state = BuildingState.Active;
        b.coord = new GridCoord(0, 0); b.footprint = Vector2Int.one;
        BuildingFactory.Instance.AttachComponents(b, def);
        var list = Collect(go);
        UnityEngine.Object.DestroyImmediate(go);
        return list;
    }

    private static List<string> Collect(GameObject go)
    {
        var comps = go.GetComponents<IBuildingComponent>();
        var list = new List<string>();
        for (int i = 0; i < comps.Length; i++)
            if (comps[i] != null) list.Add(comps[i].GetType().Name);
        return list;
    }

    private static bool SameSet(List<string> a, List<string> b)
    {
        var x = new List<string>(a); x.Sort(StringComparer.Ordinal);
        var y = new List<string>(b); y.Sort(StringComparer.Ordinal);
        return SameSeq(x, y);
    }

    private static bool SameSeq(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
#endif
