using UnityEngine;

/// <summary>
/// 守卫部署全局配置（2_8 步骤6 / §5.2B D190~D195）。SO 资产：Resources/Config/GuardConfig.asset。
/// 守卫行为规则调参入口（警戒半径/每点派兵数/怪物掠夺时长）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/GuardConfig", fileName = "GuardConfig")]
public class GuardConfig : ScriptableObject
{
    private static GuardConfig _instance;

    /// <summary>懒加载（缺资产用类默认值兜底，需在 Resources/Config 放资产）。</summary>
    public static GuardConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GuardConfig>("Config/GuardConfig");
            return _instance;
        }
    }

    [Header("守卫部署（2_8 §三 GuardConfig / D190~D195）")]
    [Tooltip("守卫警戒半径（格，围绕高价值资源点，确定守卫区域矩形范围）")]
    public float guardWarnRadiusCells = 5f;

    [Tooltip("每资源点默认派兵数（D192/D116）")]
    public int guardCountPerPoint = 2;

    [Tooltip("怪物掠夺带走时长（秒，D194 对齐 2_14 §4.2）")]
    public float lootTakeDuration = 3f;
}