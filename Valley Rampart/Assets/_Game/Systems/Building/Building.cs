using UnityEngine;

/// <summary>
/// 运行时建筑实例（骨架）。3.3.1 P1 提供 GridSystem 占用层所需的最小字段；
/// 3.3 主体将扩充 BuildingDef 引用、HP、等级、产能、ISaveable、IInteractable 等。
///
/// 地图预置建筑（树/矿/裂隙/主城）由 BuildingFactory 实例化，isPlayerBuilt=false；
/// 玩家建造由 BuildController 实例化，isPlayerBuilt=true。
/// </summary>
public class Building : MonoBehaviour
{
    [Header("占位")]
    [Tooltip("所在小区块坐标（左下角）")]
    public GridCoord coord;
    [Tooltip("占几个小区块（默认1，城堡=2）")]
    public int cellWidth = 1;
    [Tooltip("是否阻挡移动/寻路")]
    public bool isObstacle = false;

    [Header("来源")]
    [Tooltip("地图预置类型（玩家建造=None）")]
    public BuildingType sourceType = BuildingType.None;
    [Tooltip("false=地图预置（不可拆/不可移）")]
    public bool isPlayerBuilt = true;

    // ===== 3.3 主体将补充 =====
    // public BuildingDef def;
    // public int level = 1;
    // public int hp;
    // public Faction faction;
    // 实现 IInteractable, ISaveable
}
