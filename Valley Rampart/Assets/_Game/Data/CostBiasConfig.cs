using UnityEngine;

/// <summary>
/// 成本偏好占位权重（2_7 §三 CostBiasConfig，D3）。SO 手配占位，2_9 入训后由训练产出替换。
/// 资产：Resources/Config/CostBiasConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/CostBiasConfig", fileName = "CostBiasConfig")]
public class CostBiasConfig : ScriptableObject
{
    private static CostBiasConfig _instance;

    public static CostBiasConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<CostBiasConfig>("Config/CostBiasConfig");
            return _instance;
        }
    }

    [Header("成本偏好权重（2_6 P1 成本场消费；未就绪时仍输出不消费，R6）")]
    [Tooltip("威胁带惩罚")]
    public float threatWeight = 2.0f;

    [Tooltip("安全区吸引")]
    public float safetyWeight = 0.5f;

    [Tooltip("编队位吸引")]
    public float formationWeight = 0.8f;

    [Tooltip("威胁方向扇区权重（步骤2：360° 来袭扇区内加权）")]
    public float directionSectorWeight = 1.0f;
}