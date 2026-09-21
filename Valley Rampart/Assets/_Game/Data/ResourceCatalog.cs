using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 资源目录（`09_资源与仓库.md` §四 资源表 · `M1-A` `09#35`）。
/// ⭐ 形态＝<b>C# 静态表 ＋ ResourceType 当键</b>（§4.1）：一行一资源、全部同一处；
/// ⛔ 不散落到 SO／JSON，⛔ 不新增第二套字符串 ID 体系。
/// ⭐ 加一个资源 ＝ <b>两处</b>：①`ResourceType` 枚举加一项 ②本表加一行 —— ⛔ 且只有这两处。
///
/// 三条基本判据（§二）：可搬运性＝资源；带标签＋容量字段＝仓库；守恒只作用于微观搬运动作。
/// 标签＝路径（§三）：`前缀1_前缀2_…_前缀N . 后缀`，仓库声明的路径逐段是资源路径的前缀 ⟺ 能装。
/// </summary>
public static class ResourceCatalog
{
    /// <summary>一条资源（§4.2 最小字段集＝4 个：Type／Paths／Volume／DisplayName）。</summary>
    public readonly struct Entry
    {
        public readonly ResourceType Type;
        /// <summary>标签（路径 · 可多条 —— §3.3 多路径解决"一个东西属于两类"）。</summary>
        public readonly string[] Paths;
        /// <summary>体积＝一个该资源占几格（整数 · §4.2）；体积＝0 ⇒ 不占容量。</summary>
        public readonly int Volume;
        public readonly string DisplayName;

        public Entry(ResourceType type, string[] paths, int volume, string displayName)
        {
            Type = type; Paths = paths; Volume = volume; DisplayName = displayName;
        }
    }

    // ===== 资源表（A 组 13 项 ＋ D 组水 1 项 ＝ 14 项，与 ResourceType 枚举现值 1:1）=====
    // ⚠️ A 组「第 14 项 回血包 res_consumable.medkit」**尚未落码**、归 M6（09 §4.3 A 组末行 ＋ D789 裁 A）
    //    ⇒ 现在落＝孤儿资源，会被 §十-1 启动自检报警。
    // ✅ D 组（水 · 枚举第 14 项）**已落**（`M1-F` 件1 · `D807` Q3 定案 `res_fluid.water` · 体积 1）。
    // ⚠️ B 组（人口）标签仍在表外 —— 其枚举项尚不存在，落码归 `#43`（人口账本读口 · 已独立挂账）。
    // ⭐ 体积初值（§4.2 · 已定 2026-09-18）：一切资源＝1，仅金币＝0（不占容量）；数值批可调。
    private static readonly Entry[] Table =
    {
        new Entry(ResourceType.Gold,         new[] { "res_currency.gold" },                            0, "金币"),
        new Entry(ResourceType.Stone,        new[] { "res_material.stone" },                           1, "石材"),
        new Entry(ResourceType.Wood,         new[] { "res_material.wood" },                            1, "木材"),
        new Entry(ResourceType.Ore,          new[] { "res_material.ore" },                             1, "矿石"),
        new Entry(ResourceType.Metal,        new[] { "res_material.metal" },                           1, "金属"),
        new Entry(ResourceType.Crystal,      new[] { "res_material.crystal" },                         1, "水晶"),
        // 火油＝多路径示例（§3.3「既是材料，又是弹药原料」）—— ⚠️ 弹药原料路径的**字面值 09 未给**
        //   ⇒ 只落已给定的 `res_material.fireoil`，第二条路径待文档补字面后加（⛔ 不自行编造）。
        new Entry(ResourceType.FireOil,      new[] { "res_material.fireoil" },                         1, "火油"),
        new Entry(ResourceType.Food,         new[] { "res_food.grain" },                               1, "食物"),
        new Entry(ResourceType.SpecialFood,  new[] { "res_food.special", "res_processed.special" },    1, "特殊食物"),
        new Entry(ResourceType.Meat,         new[] { "res_food.meat" },                                1, "肉"),
        new Entry(ResourceType.StoneAmmo,    new[] { "res_ammo.stone" },                               1, "石头弹"),
        new Entry(ResourceType.FireballAmmo, new[] { "res_ammo.fireball" },                            1, "火弹"),
        new Entry(ResourceType.MagicAmmo,    new[] { "res_ammo.magic" },                               1, "魔弹"),
        // ⭐ D 组（水 · `M1-F` 件1 · `09#44`）：水按资源处理（普通仓 ＋ 工人搬 ＋ 整数）。
        //   体积 1（`D807` Q2：保"水井仓满 100 ⇒ 停产"语义，⛔ 非 0）；标签 `res_fluid.water`（`D807` Q3 定案）。
        new Entry(ResourceType.Water,        new[] { "res_fluid.water" },                             1, "水"),
    };

    private static readonly Dictionary<ResourceType, Entry> Index = BuildIndex();

    private static Dictionary<ResourceType, Entry> BuildIndex()
    {
        var d = new Dictionary<ResourceType, Entry>(Table.Length);
        for (int i = 0; i < Table.Length; i++) d[Table[i].Type] = Table[i];
        return d;
    }

    /// <summary>是否在资源表内（表外枚举值＝未落码资源）。</summary>
    public static bool Contains(ResourceType type) => Index.ContainsKey(type);

    public static Entry Get(ResourceType type)
        => Index.TryGetValue(type, out var e) ? e : default;

    /// <summary>体积（§4.2 · 整数；查无 ⇒ 1，与"默认一切资源＝1"一致）。</summary>
    public static int VolumeOf(ResourceType type)
        => Index.TryGetValue(type, out var e) ? e.Volume : 1;

    public static string DisplayNameOf(ResourceType type)
        => Index.TryGetValue(type, out var e) && !string.IsNullOrEmpty(e.DisplayName)
            ? e.DisplayName : type.ToString();

    public static string[] PathsOf(ResourceType type)
        => Index.TryGetValue(type, out var e) ? (e.Paths ?? Array.Empty<string>()) : Array.Empty<string>();

    /// <summary>主路径（第一条；无 ⇒ 空串）。</summary>
    public static string PrimaryPathOf(ResourceType type)
    {
        var p = PathsOf(type);
        return p.Length > 0 ? p[0] : string.Empty;
    }

    /// <summary>全部已落码资源类型（表序）。</summary>
    public static IEnumerable<ResourceType> AllTypes
    {
        get { for (int i = 0; i < Table.Length; i++) yield return Table[i].Type; }
    }

    // ===== 标签＝路径 匹配（§三）=====

    /// <summary>
    /// 路径前缀匹配（§3.2）：仓库声明 `declaredPath` 逐段是资源路径 `resourcePath` 的前缀 ⟺ 能装。
    /// 段边界＝`_`（前缀分段）或 `.`（条目名）；例：`res`／`res_material` 命中 `res_material.stone`，
    /// 而 `res_mat` 不命中（段未对齐）。
    /// </summary>
    public static bool PathMatches(string declaredPath, string resourcePath)
    {
        if (string.IsNullOrEmpty(declaredPath) || string.IsNullOrEmpty(resourcePath)) return false;
        if (resourcePath == declaredPath) return true;
        if (!resourcePath.StartsWith(declaredPath, StringComparison.Ordinal)) return false;
        if (resourcePath.Length <= declaredPath.Length) return true;
        char next = resourcePath[declaredPath.Length];
        return next == '_' || next == '.';
    }

    /// <summary>该仓声明（单条路径）能否收该资源（资源可挂多条路径 ⇒ 任一条命中即可 · §3.3）。</summary>
    public static bool Accepts(string declaredPath, ResourceType type)
    {
        var paths = PathsOf(type);
        for (int i = 0; i < paths.Length; i++)
            if (PathMatches(declaredPath, paths[i])) return true;
        return false;
    }

    /// <summary>
    /// 该仓声明（多条路径）能否收该资源 ⟺ 存在一对（声明, 资源路径）命中。
    /// ⭐ 通用仓／分类仓／专属仓 ＝ 同一个机制的三种写法（§3.2）。
    /// ⚠️ `declaredPaths` 为空 ⇒ 视为**未声明**（返回 false）；「空声明＝通用仓」的兜底由
    /// <see cref="WarehousePaths.Normalize"/> 统一给出，⛔ 不在此处隐式兜底（防两处语义）。
    /// </summary>
    public static bool Accepts(IReadOnlyList<string> declaredPaths, ResourceType type)
    {
        if (declaredPaths == null || declaredPaths.Count == 0) return false;
        var paths = PathsOf(type);
        for (int i = 0; i < declaredPaths.Count; i++)
        {
            var decl = declaredPaths[i];
            if (string.IsNullOrEmpty(decl)) continue;
            for (int j = 0; j < paths.Length; j++)
                if (PathMatches(decl, paths[j])) return true;
        }
        return false;
    }

    /// <summary>按完整路径反查资源（§3.2「路径 ＋ 条目名」精确匹配；查无 ⇒ null）。</summary>
    public static ResourceType? FindByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        for (int i = 0; i < Table.Length; i++)
        {
            var ps = Table[i].Paths;
            for (int j = 0; ps != null && j < ps.Length; j++)
                if (ps[j] == fullPath) return Table[i].Type;
        }
        return null;
    }
}

/// <summary>仓库声明路径的规范化（§3.2 通用仓写法）。</summary>
public static class WarehousePaths
{
    /// <summary>通用仓声明（`res` ＝ 全部资源 · §3.2 表首行）。</summary>
    public const string All = "res";

    private static readonly string[] AllOnly = { All };

    /// <summary>
    /// 声明规范化：空/全空串 ⇒ 通用仓 `res`（§3.2「`res` ＝ 全部资源」）。
    /// ⚠️ 兜底只此一处（⛔ 不在匹配函数里隐式兜底），且空声明**不再等于**"空标签仓"告警口径（§十-1）。
    /// </summary>
    public static string[] Normalize(string[] declared)
    {
        if (declared == null || declared.Length == 0) return AllOnly;
        bool any = false;
        for (int i = 0; i < declared.Length; i++)
            if (!string.IsNullOrEmpty(declared[i])) { any = true; break; }
        return any ? declared : AllOnly;
    }
}

/// <summary>
/// 资源条目：<b>资源（键）＋ 量</b>（§七 统一条目）。
/// ⭐ 一处定义、三处复用（§五 仓库查询／§7.3 配方／§十六 造价 ≡ 配方同形）：
/// 仓库存量条目、造价条目、配方条目**同一个类型**，⛔ 不再各立一份。
/// </summary>
[Serializable]
public struct ResourceAmount
{
    public ResourceType type;
    public int amount;

    public ResourceAmount(ResourceType t, int a) { type = t; amount = a; }
}

/// <summary>
/// 资源量列表（<b>`ResourcePack` 的接班者</b> · `09#50`／`09#51`）。
/// ⭐ 语义＝「若干资源（键）＋ 各自量」的**列表**（⛔ 非固定桶）⇒ 资源表加项即自动可表达。
/// ⭐ 同形共用（§7.3）：**造价表 ≡ 配方表 ≡ 箱子／搬运内容物** 全部用本类型。
/// ⚠️ 用 <b>struct ＋ copy-on-write</b>：值语义（与旧 `ResourcePack` 一致，赋/传参不被共享改写），
///    写操作返回新数组，⛔ 不产生"改了副本、原件跟着变"的别名 bug。
/// </summary>
[Serializable]
public struct ResourceList
{
    /// <summary>条目数组（Unity 可序列化；空列表＝空数组，⛔ 非 null）。</summary>
    public ResourceAmount[] items;

    public static ResourceList Empty => new ResourceList { items = Array.Empty<ResourceAmount>() };

    /// <summary>条目数。</summary>
    public int Count => items != null ? items.Length : 0;

    /// <summary>是否为空/全 0（§5.1 造价判空；旧 `ResourcePack.IsZero` 的接班人）。</summary>
    public bool IsZero
    {
        get
        {
            if (items == null || items.Length == 0) return true;
            for (int i = 0; i < items.Length; i++) if (items[i].amount != 0) return false;
            return true;
        }
    }

    /// <summary>件数合计（§7.5-④「件数」口径）。</summary>
    public int TotalCount
    {
        get
        {
            int sum = 0;
            if (items != null) for (int i = 0; i < items.Length; i++) sum += items[i].amount;
            return sum;
        }
    }

    /// <summary>某资源量（查无 ⇒ 0）。</summary>
    public int Get(ResourceType type)
    {
        if (items == null) return 0;
        for (int i = 0; i < items.Length; i++) if (items[i].type == type) return items[i].amount;
        return 0;
    }

    public bool Has(ResourceType type) => Get(type) != 0;

    /// <summary>设某资源量（0 ⇒ 移除该条目）；copy-on-write。</summary>
    public ResourceList Set(ResourceType type, int amount)
    {
        int n = Count;
        int found = -1;
        for (int i = 0; i < n; i++) if (items[i].type == type) { found = i; break; }

        if (found < 0)
        {
            if (amount == 0) return this;                       // 无需新增 0 条目
            var grown = new ResourceAmount[n + 1];
            if (n > 0) Array.Copy(items, grown, n);
            grown[n] = new ResourceAmount(type, amount);
            return new ResourceList { items = grown };
        }

        if (items[found].amount == amount) return this;         // 无变化
        if (amount == 0)
        {
            var shrunk = new ResourceAmount[n - 1];
            for (int i = 0, k = 0; i < n; i++) if (i != found) shrunk[k++] = items[i];
            return new ResourceList { items = shrunk };
        }
        var copy = (ResourceAmount[])items.Clone();
        copy[found] = new ResourceAmount(type, amount);
        return new ResourceList { items = copy };
    }

    /// <summary>加量（负数＝减量；减到 0 ⇒ 移除条目）；copy-on-write。</summary>
    public ResourceList Add(ResourceType type, int amount)
    {
        if (amount == 0) return this;
        int cur = Get(type);
        int next = cur + amount;
        return Set(type, next < 0 ? 0 : next);
    }

    // ===== 旧 ResourcePack 的算子接班人（语义逐条对齐，保迁移期读数一致）=====

    public static ResourceList operator +(ResourceList a, ResourceList b)
    {
        var r = a;
        if (b.items == null) return r;
        for (int i = 0; i < b.items.Length; i++) r = r.Add(b.items[i].type, b.items[i].amount);
        return r;
    }

    /// <summary>按比例缩放（拆除退款 ratio 等；⭐ 用 Mathf.RoundToInt 保旧口径逐值一致）。</summary>
    public static ResourceList operator *(ResourceList a, float scale)
    {
        var r = Empty;
        if (a.items == null) return r;
        for (int i = 0; i < a.items.Length; i++)
            r = r.Set(a.items[i].type, Mathf.RoundToInt(a.items[i].amount * scale));
        return r;
    }

    public ResourceList Clone() => new ResourceList
    {
        items = items == null ? Array.Empty<ResourceAmount>() : (ResourceAmount[])items.Clone()
    };

    /// <summary>构造（迁移工具／测试用）。</summary>
    public static ResourceList Of(params ResourceAmount[] entries)
        => new ResourceList { items = entries ?? Array.Empty<ResourceAmount>() };

    /// <summary>调试用文本（例：`Stone×10, Wood×4`）。</summary>
    public override string ToString()
    {
        if (items == null || items.Length == 0) return "(空)";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(ResourceCatalog.DisplayNameOf(items[i].type)).Append('×').Append(items[i].amount);
        }
        return sb.ToString();
    }
}
