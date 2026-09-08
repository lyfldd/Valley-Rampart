// ============================================================================
//  专属建筑 id 常量表（2_20 M6 四族专属；HH.115 E-T6 字符串散点收口）
//  散点治理：HasExclusiveBuilding 调用面的建筑 id 字面量（"WarAcademy"/"LeyForge"/"WarCamp"）
//  收口本常量表——新增专属建筑/改 id 只动本文件（grep 零字符串散点残留=E-T6 验收锚）。
//  消费点：TrainingSystem（学院训练加速）/DamageSystem（战营击杀回复）/KingdomRace（熔炉采矿）
// ============================================================================

/// <summary>
/// 四族专属建筑 id（2_20 M6，D419：人类战争学院/兽人战营/矮人地脉熔炉/精灵射箭场）。
/// id 字符串=BuildingDef.buildingId 资产侧唯一键（UtilityActionConfig 同源），改动须三处同批。
/// </summary>
public static class BuildingIds
{
    public const string WarAcademy   = "WarAcademy";    // 人类专属（建战学院；训练时长全局加速）
    public const string WarCamp      = "WarCamp";       // 兽人专属（战营；击杀回复）
    public const string LeyForge     = "LeyForge";      // 矮人专属（地脉熔炉；采矿 +40%）
    public const string ArcheryRange = "ArcheryRange";  // 精灵专属（射箭场）
}
