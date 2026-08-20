using UnityEngine;

/// <summary>
/// 2_7 §3 VisionConfig 迷雾视野（D262）。渲染归 2_10、巡逻归 2_8，本篇只落"决策核只读视野内目标"。
/// 资产：Resources/Config/VisionConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/VisionConfig", fileName = "VisionConfig")]
public class VisionConfig : ScriptableObject
{
    private static VisionConfig _instance;

    public static VisionConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<VisionConfig>("Config/VisionConfig");
            return _instance;
        }
    }

    [Header("迷雾视野（2_7 步骤8，本篇只落决策核过滤；渲染 2_10）")]
    [Tooltip("总开关：false=全可见（回退基线，等同旧 1D 无迷雾行为）")]
    public bool enabled = true;

    [Tooltip("小区块探索标记上限（D173，128²=16384）")]
    public int maxExploredCells = 16384;
}