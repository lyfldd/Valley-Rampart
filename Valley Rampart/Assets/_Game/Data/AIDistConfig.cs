using UnityEngine;

/// <summary>
/// 距离口径配置（2_7 §三 AIDistConfig）。距离统一到「格单位」域（不再 ×cellSize 转世界）。
/// useGridUnits 开关：true=2D 格单位口径；false=回退 1D 轴距离（1D/2D 对照调参，步骤9）。
/// 资产：Resources/Config/AIDistConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/AIDistConfig", fileName = "AIDistConfig")]
public class AIDistConfig : ScriptableObject
{
    private static AIDistConfig _instance;

    public static AIDistConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<AIDistConfig>("Config/AIDistConfig");
            return _instance;
        }
    }

    [Header("距离口径（2_7 步骤1/9）")]
    [Tooltip("总开关：true=2D 格单位距离；false=回退 1D 轴距离（对照调参用）")]
    public bool useGridUnits = true;

    [Header("撤退距离（格单位）")]
    [Tooltip("撤退距离基数（格单位）")]
    public float baseRetreatCells = 6f;

    [Tooltip("撤退距离阶梯（格，按受击次数累加）")]
    public float stepRetreatCells = 1f;

    [Header("感知/攻击半径（格单位，不再 ×cellSize）")]
    [Tooltip("感知半径（格单位，职业默认在此覆盖）")]
    public float perceptionRadius = 10f;

    [Tooltip("攻击半径（格单位，职业默认在此覆盖）")]
    public float attackRange = 1f;
}