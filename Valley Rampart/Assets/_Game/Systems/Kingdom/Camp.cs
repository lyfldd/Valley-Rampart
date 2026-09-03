using System.Collections.Generic;

/// <summary>
/// 营地聚落记录（2_16 P1 步骤9，D301/D313/D387 滞回带）。
/// 当某营地建筑周围未招募流浪汉聚到结营阈值（≥3）时，生成一条 Camp 记录，作为动态立国（步骤11 CampUpgrader）的前兆状态。
///
/// 语义（设计 §1.1 / D313 / D387）：
/// - 结营 ≥3：营地建筑半径内 ≥3 未招募流浪汉 → 建 Camp。
/// - 存续日不清零（D313）：日 tick persistenceDays+1；驱散/屠杀只减人数不重置——干预=拖延。
/// - 散营 <2（D387 修订，滞回带 [2,3)）：存续 ≥2 人且成员 <2 → 移除记录（营地建筑保留可再结营；再结营存续日从 0 起——杀散才是真正阻止）。
///
/// 持久化：只有 centerCell / persistenceDays 入档（CampListSaveData，见设计 §1.1"Camp 存续计数"）；
/// memberIds 每 tick 半径扫描自愈（对齐本系统无存档映射哲学），campBuildingId 为运行期标识、读档后由 centerCell 重挂。
/// </summary>
public class Camp
{
    /// <summary>营地建筑格（主键：运行期/读档重建时按格匹配建筑）。</summary>
    public GridCoord centerCell;

    /// <summary>营地 Building.GetInstanceID()（纯运行期标识，不入档；读档后由 centerCell 重挂）。</summary>
    public int campBuildingId;

    /// <summary>成员 npcId 列表（每 tick 半径扫描刷新，不入档；读档后扫描重建）。</summary>
    public List<int> memberIds = new List<int>();

    /// <summary>存续日计数（D313：驱散不清零，仅随日 tick 递增）。</summary>
    public int persistenceDays;

    /// <summary>占位：是否已触发建国（本步骤恒 false；步骤11 CampUpgrader 置 true）。</summary>
    public bool foundedFlag;

    /// <summary>
    /// D306 修订（D469，HH.51 批B）：营地被领土圈入但为异族营 → 不解散不转化，就地敌对野人营。
    /// 本旗只作"已宣告"去重（免日 tick 重复日志），运行期态不入档（读档后由 ScanCamps 自愈重建，重建后如仍异族会再宣告一次）。
    /// </summary>
    public bool wildAnnexDeclinedFlag;

    public Camp(GridCoord cell, int buildingId)
    {
        centerCell = cell;
        campBuildingId = buildingId;
    }
}