using UnityEngine;

/// <summary>
/// 2_12 步骤6 全资源刷新配置（HH.10 裁决三/五：SO 周期按类型分键，D61"树短石矿长"）。
/// 资产：Resources/Config/RespawnConfig.asset（Instance Resources.Load 懒加载，与 VisionConfig 同模式）。
/// 数值为可冒烟执行的占位（裁决验收顺带：把 treeRespawnDays/daySeconds 调小即快验，别等真实周期）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/RespawnConfig", fileName = "RespawnConfig")]
public class RespawnConfig : ScriptableObject
{
    private static RespawnConfig _instance;

    public static RespawnConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<RespawnConfig>("Config/RespawnConfig");
            return _instance;
        }
    }

    [Header("全资源刷新总开关")]
    [Tooltip("false=禁用重生（回退基线；采完即永久消失）")]
    public bool enabled = true;

    [Tooltip("游戏一天对应的真实秒数。天数×daySeconds=真实重生倒计时；冒烟快验调小此值即加速。")]
    public float daySeconds = 3f;

    [Header("Tree（数据格采集，非实体，防 A+ 复辟）")]
    [Tooltip("工人砍一棵树的耗时（裁决'树≈2s 档'）")]
    public float treeGatherSeconds = 2f;
    [Tooltip("砍一次入背包的木量")]
    public int treeGatherAmount = 5;
    [Tooltip("树重生天数（D61：树短）")]
    public float treeRespawnDays = 7f;

    [Header("一次性实体重生周期（裁决'石矿长'；WoodPile 最短）")]
    [Tooltip("木堆实体重生天数（一次性资源，最短）")]
    public float woodRespawnDays = 4f;
    [Tooltip("石堆实体重生天数（较长）")]
    public float stoneRespawnDays = 10f;
    [Tooltip("矿脉实体重生天数（最长）")]
    public float oreRespawnDays = 12f;
}