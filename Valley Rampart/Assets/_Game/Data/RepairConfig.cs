using UnityEngine;

/// <summary>
/// 建筑修复/废墟重建配置（2_12 步骤7 / D155）。
/// 修复成本 = 累计投入 × repairCostRatio（废墟重建）；受损修复同此比例。
/// 资产：Resources/Config/RepairConfig.asset（Instance Resources.Load 懒加载，so-data-driven 铁律）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/RepairConfig", fileName = "RepairConfig")]
public class RepairConfig : ScriptableObject
{
    private static RepairConfig _instance;

    public static RepairConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<RepairConfig>("Config/RepairConfig");
            return _instance;
        }
    }

    [Tooltip("修复/废墟重建成本 = 累计投入 × 此比例（D155）。0.5=半价重建，0=免费（调试）")]
    public float repairCostRatio = 0.5f;
}