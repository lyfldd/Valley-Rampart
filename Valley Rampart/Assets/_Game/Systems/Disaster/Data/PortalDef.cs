using UnityEngine;

// 2_14 传送门属性 SO（设计 §2.3/§2.4）
[CreateAssetMenu(menuName = "ValleyRampart/Disaster/PortalDef")]
public class PortalDef : ScriptableObject
{
    [Header("属性（§2.3）")]
    public int[] hpByDifficulty = { 500, 1000, 1500 };  // HP（Easy/Normal/Hard）

    [Header("占格")]
    public Vector2Int footprint = new Vector2Int(2, 2); // 逻辑占格 2×2 小区块（视觉 3×3 归 2_10）

    [Header("召唤（§5.2）")]
    public float summonInterval = 30f;           // 召唤间隔（秒，正常）
    public float summonIntervalOnHit = 15f;      // 被攻击时召唤间隔（秒，反向强化 P0 二档）
    public int maxConcurrentMonsters = 30;       // 同时怪物上限

    [Header("烈度（§2.4）")]
    public float aftermathDecayRate = 0.8f;      // 未摧毁 → 每后一晚强度衰减倍率（-20%）

    /// <summary>按难度取基础 HP（难度档 1=Easy/2=Normal/3=Hard → 数组下标 0/1/2；越界取 Normal）。</summary>
    public int GetBaseHp(int difficulty)
    {
        if (hpByDifficulty == null || hpByDifficulty.Length == 0) return 1000;
        int idx = Mathf.Clamp(difficulty - 1, 0, hpByDifficulty.Length - 1);
        return hpByDifficulty[idx];
    }
}