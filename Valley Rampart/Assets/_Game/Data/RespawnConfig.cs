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

    // ==========================================================================
    //  【HH.294 片 6-2 · B-1（`D778` 裁「甲」）】一次性资源点转纯数据后的采集耗时
    //  ⭐ 逐型保留原值 ⇒ **零行为变更**：改前耗时取自各 `BuildingDef.gatherSeconds`
    //    （`ore_vein.asset:65=8` / `stone_pile.asset:65=4` / `wood_pile.asset:65=2`，实体退役后无 def 可读 ⇒ 落本处）。
    //   不用单值（任何单值都会改 ≥2 型耗时 ⇒ 属平衡调整，非本批「双写收敛」范围）。
    //  ⛔ 不用按类数组（与 `MapGenRules` 的 kind 序耦合·多一层隐式约束）。
    //  入包量：沿用既有单值口径 `TaskScheduler.gatherAmount`（=5，已核无冲突）。
    // ==========================================================================

    [Header("一次性资源点（数据寻址·逐型原值）")]
    [Tooltip("矿脉采集耗时（秒）·改前＝ BuildingDef ore_vein.gatherSeconds = 8")]
    public float oreVeinGatherSeconds = 8f;
    [Tooltip("石堆采集耗时（秒）·改前＝ BuildingDef stone_pile.gatherSeconds = 4")]
    public float stonePileGatherSeconds = 4f;
    [Tooltip("木堆采集耗时（秒）·改前＝ BuildingDef wood_pile.gatherSeconds = 2")]
    public float woodPileGatherSeconds = 2f;

    /// <summary>按 **feature** 取采集耗时（B-1 裁的查表 helper·供 `WorldGatherSource`／`ResourceRespawnSystem` 调）。
    /// `Tree` ⇒ 既有树字段；三型 ⇒ 各自具名字段（逐型＝改前实盘值）；其余（含 `Mine` 锚点／非资源）⇒ 0（调用方判否）。</summary>
    public float GatherSecondsOf(FeatureType feature)
    {
        switch (feature)
        {
            case FeatureType.Tree: return treeGatherSeconds;
            case FeatureType.OreVein: return oreVeinGatherSeconds;
            case FeatureType.StonePile: return stonePileGatherSeconds;
            case FeatureType.WoodPile: return woodPileGatherSeconds;
            default: return 0f;
        }
    }
}