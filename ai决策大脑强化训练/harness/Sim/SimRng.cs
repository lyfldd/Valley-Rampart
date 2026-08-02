// ============================================================================
//  M2 Headless 模拟器 - SimRng 确定性随机数（IRngPort 端口实现）
//  04_模拟器规格.md §七：所有 RNG 走 IRngPort（种子=场景 seed）——全代码仅漫游+弹道误差 2 处。
//  Unity 实现：UnityEngine.Random.Range；模拟器实现：种子 System.Random。
//  System.Random 在 .NET 9 的实现为固定确定性算法（同 seed 同序列，跨平台一致），
//  满足 04 §七 确定性（M3 验收 JSONL 逐字节一致）。
// ============================================================================

/// <summary>
/// 确定性 RNG（IRngPort 实现）。
/// 注意：决策点 2 要求"布阵级 RNG 非决策可见"——SimWorld 持有两个实例：
///   FormationRng（seed = 场景seed + 局号，仅开局位置抖动用）
///   DecisionRng（seed = 场景seed，供 IRngPort 决策链路：漫游/弹道误差）
/// </summary>
public sealed class SimRng : IRngPort
{
    private readonly System.Random _rng;

    public SimRng(int seed)
    {
        _rng = new System.Random(seed);
    }

    /// <summary>IRngPort：返回 [min, max) 区间随机浮点（UnityEngine.Random.Range 语义）。</summary>
    public float Range(float min, float max)
    {
        // NextDouble() ∈ [0,1)，线性映射到 [min,max)，float 转换一次完成
        return min + (max - min) * (float)_rng.NextDouble();
    }
}
