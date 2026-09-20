using System;
using System.Collections.Generic;

/// <summary>
/// 配方输出条目（`09` §7.3 第 1 步 ＋ §十三-2）：⭐ **支持概率输出**（每条 output 带几率／权重）
/// ⇒ 副产（矿洞水晶/火油）也进同一张配方表，⛔ 不另立机制。
/// </summary>
[Serializable]
public struct RecipeOutput
{
    public ResourceType type;
    public int amount;
    /// <summary>几率／权重（0~1；⇒ 0 视为 1.0 ＝ 必出）。</summary>
    public float chance;

    public RecipeOutput(ResourceType t, int a, float c = 1f) { type = t; amount = a; chance = c <= 0f ? 1f : c; }
}

/// <summary>
/// 配方（`09` §7.3）：**原料（标签＋数量）→ 产出（可概率）**。
/// ⭐ **与造价表同形**（`09#51`）：`inputs` 就是 <see cref="ResourceList"/>（＝造价用的同一个「资源量列表」），
/// ⛔ 两者不再各留一套结构。
/// ⚠️ **条件分两层**（§7.3 第 2 步）：**资源代价**（装进箱子 ⇒ 走 `inputs`）＋ **前置谓词**
///    （装不进箱子 ⇒ 走后缀字段，如要有人在岗／要白天／要建筑等级）。
/// </summary>
[Serializable]
public struct RecipeDef
{
    /// <summary>配方标识（调用方按 id 声明，例：铁匠铺＝`blacksmith`）。</summary>
    public string id;
    /// <summary>原料（资源量列表 · 与造价同形）。</summary>
    public ResourceList inputs;
    /// <summary>产出（可概率）。</summary>
    public RecipeOutput[] outputs;
    /// <summary>前置谓词：要有人在岗（§7.3 第 2 步 · 装不进箱子的一部分）。</summary>
    public bool requiresWorkers;
    /// <summary>前置谓词：单件耗时（秒）——「推进一次」的原子动作粒度（§7.3 语义一）。</summary>
    public float secondsPerUnit;

    public RecipeDef(string id, ResourceList inputs, RecipeOutput[] outputs,
                     bool requiresWorkers = true, float secondsPerUnit = 0f)
    {
        this.id = id; this.inputs = inputs; this.outputs = outputs;
        this.requiresWorkers = requiresWorkers; this.secondsPerUnit = secondsPerUnit;
    }
}

/// <summary>
/// 配方表（`09#46` · 结构见 `09` §7.3）。
/// ⭐ **本片（`M1-A`）只建表**：把"配方"从硬编码升级为一张静态数据表（结构 ＋ 查表 API）；
/// ⛔ **把 `StorageComponent.Transform` 改成走本表属 `M6-B`**（本片只改结构，⛔ 不动行为）。
///
/// ⏳ **条目留空（有据）**：`09` §十三-2 已裁「⭐ **配方条目**：配方输出支持概率输出…⚠️ **具体条目与数值 ⇒ 数值批**」
/// ⇒ 本表**不预填**任何条目。理由：现行唯一配方（`StorageComponent.Transform:99` 的 `Ore→Metal`）
/// 的**数值真源是 SO** `BlacksmithDef.oreToMetalRatio`（`:106`）⇒ 在此抄一份 2:1 ＝ **L-01 双真源家族**
/// ⇒ ⛔ 不抄。`M6-B` 接表时按数值批给出的条目填入，并把 SO 数值迁入（或显式保留 SO 为真源）。
/// </summary>
public static class RecipeCatalog
{
    /// <summary>配方表（一行一配方 · 同 `ResourceCatalog` 形态）。</summary>
    private static readonly RecipeDef[] Table = Array.Empty<RecipeDef>();

    private static readonly Dictionary<string, int> Index = BuildIndex();

    private static Dictionary<string, int> BuildIndex()
    {
        var d = new Dictionary<string, int>(Table.Length, StringComparer.Ordinal);
        for (int i = 0; i < Table.Length; i++) d[Table[i].id] = i;
        return d;
    }

    public static int Count => Table.Length;

    /// <summary>按 id 查配方（查无 ⇒ false）。</summary>
    public static bool TryGet(string recipeId, out RecipeDef recipe)
    {
        if (!string.IsNullOrEmpty(recipeId) && Index.TryGetValue(recipeId, out var i))
        {
            recipe = Table[i];
            return true;
        }
        recipe = default;
        return false;
    }

    /// <summary>全部配方（只读遍历）。</summary>
    public static IReadOnlyList<RecipeDef> All => Table;
}
