using UnityEngine;

/// <summary>
/// 箱子实体化配置（2_12 步骤7C + 步骤11 / D222/D148/D269）。
/// 倒地箱子（D142 统一资源容器）生命周期与堆积上限。
/// 资产路径：Resources/Config/ChestConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/ChestConfig", fileName = "ChestConfig")]
public class ChestConfig : ScriptableObject
{
    private static ChestConfig _instance;
    public static ChestConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ChestConfig>("Config/ChestConfig");
            return _instance;
        }
    }

    [Tooltip("单格最多箱子数（D222 数量上限防堆积，超限后最早箱子消失）。D269 占位=4")]
    public int chestMaxPerCell = 4;

    [Tooltip("倒地箱子存活天数（D148 时限消失防堆积）。D269 占位=3 天")]
    public float expireDays = 3f;
}