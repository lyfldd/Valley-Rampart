using UnityEngine;

/// <summary>
/// T6 三档夹具档位表（HH.92/D549；2026-09-07 策划核准版=HH.93 §九，gold=1500 维持）。
/// 唯一真源=Resources/Config/TestHarness/TestFixtureTiers.asset；代码草案表已删（D552 授权迁 SO）。
/// 仅测试链消费（TestFixtureApi Editor-only），正式局零引用。
/// v2 数值依据（THD 实测）：①每 House 容量=3，houses 保证 houseCapacity>population（T9/M8 生育门槛）；
/// ②军事期战士 6 起步=留 ⑦招战士缺口 needA=8 的 +2 扩军空间。
/// </summary>
[CreateAssetMenu(fileName = "TestFixtureTiers", menuName = "Valley/Config/Test Fixture Tiers")]
public class TestFixtureTiersConfig : ScriptableObject
{
    [System.Serializable]
    public class TierEntry
    {
        public string label;
        public int workers;
        public int warriors;
        public int houses;
        public string[] buildings;   // castle 外的建筑清单（Well 在前保供水；farm 保粮链）
        public int gold, stone, wood, food, metal;
        public int warehouseFill;    // 仓库 StorageComponent 注入量（按其 resourceType）
    }

    [Tooltip("三档：开局态/中期态/军事期（索引对齐 FixtureTier 枚举 0/1/2）")]
    public TierEntry[] tiers;
}
