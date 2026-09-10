using UnityEngine;

/// <summary>
/// 铁匠铺配置（2_12 步骤8 / 设计 §5.8，D199~D200；**D609/T1.3 原料由石改矿石**）。
/// 矿石→Metal 就地加工转化率 SO（占位 2:1，so-data-driven 铁律：可调数值外置，禁止硬编码）。
/// 资产路径：Resources/Config/BlacksmithDef.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/BlacksmithDef", fileName = "BlacksmithDef")]
public class BlacksmithDef : ScriptableObject
{
    [Tooltip("矿石→Metal 转化率（占位 2:1：N 矿 → 1 Metal，D609）")]
    public int oreToMetalRatio = 2;

    /// <summary>N 矿可转化出多少 Metal（≥0；ratio<=0 视为无转化）。</summary>
    public int MetalFrom(int oreAmount)
        => oreToMetalRatio > 0 ? Mathf.Max(0, oreAmount / oreToMetalRatio) : 0;
}