using UnityEngine;

/// <summary>
/// 2_12 步骤6 全资源刷新配置（HH.10 裁决三/五：SO 周期按类型分键，D61"树短石矿长"）。
/// 资产：Resources/Config/RespawnConfig.asset（Instance Resources.Load 懒加载，与 VisionConfig 同模式）。
///
/// **HH.294 片 5（`03` §6.7 · `D762`）**：⭐ 重生节奏改「资源池模型」——
/// 时间基准＝**游戏天**（`TimeManager.CurrentDay`，**不再有第二日历**）＋ 每天推进一次 ＋ 分帧分摊；
/// 节奏由池子曲线（`03` §6.7.2）决定 ⇒ ⛔ **删 `daySeconds`（第二日历）与四类重生天数**
/// （`03` §6.7「旧数值 树7/木4/石10/矿12 天 **作废**」）。
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

    [Header("Tree（数据格采集，非实体，防 A+ 复辟）")]
    [Tooltip("工人砍一棵树的耗时（裁决'树≈2s 档'）")]
    public float treeGatherSeconds = 2f;
    [Tooltip("砍一次入背包的木量")]
    public int treeGatherAmount = 5;
}