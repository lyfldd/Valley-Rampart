using UnityEngine;

/// <summary>
/// 逃逸点采样配置（2_7 §三 RetreatConfig，§5.3 逃逸点算法）。
/// 资产：Resources/Config/RetreatConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/RetreatConfig", fileName = "RetreatConfig")]
public class RetreatConfig : ScriptableObject
{
    private static RetreatConfig _instance;

    public static RetreatConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<RetreatConfig>("Config/RetreatConfig");
            return _instance;
        }
    }

    [Header("逃逸点采样（2_7 步骤4，§5.3）")]
    [Tooltip("采样圈数（撤退距离 R × {0.6,0.8,1.0}）")]
    public int sampleRings = 3;

    [Tooltip("每圈采样方向数")]
    public int directionsPerRing = 8;

    [Tooltip("撤退扇区半角（度，±60°）")]
    public float sectorHalfAngleDeg = 60f;

    [Tooltip("贴近理想撤退距离权重（score = costField + 此量×|dist-R|）")]
    public float distBiasWeight = 0.5f;
}