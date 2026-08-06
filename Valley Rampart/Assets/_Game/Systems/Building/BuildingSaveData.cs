using UnityEngine;

/// <summary>
/// 建筑存档数据（3.3.4 批次10 接口预留）。
/// 字段定义就位，序列化/反序列化逻辑后续阶段实现。
/// Building 实现 ISaveable 时用此结构（含组件存档：StorageComponent.storedAmount）。
///
/// 存档范围（见 3.3.4 §10.5）：
/// - Building 核心：type/coord/level/hp/faction/state/footprint/rotation
/// - StorageComponent：storedAmount
/// - ProducerComponent：无需存档（每秒重算）
/// - BuildingFactory 重建：根据 defId 重新挂组件 + 恢复 storedAmount
/// </summary>
[System.Serializable]
public struct BuildingSaveData
{
    public string defId;        // BuildingDef.id（重建时查映射表）
    public int coordX;          // GridCoord.x
    public int level;
    public int hp;
    public int maxHp;
    public int faction;         // (int)Faction
    public int state;           // (int)BuildingState
    public int cellWidth;
    public int sourceType;      // (int)BuildingType
    public int storedAmount;    // StorageComponent.storedAmount（无则 0）
    // 3.5 步骤6：矿洞副产（水晶/火油）本地存储（无则 0）。旧档缺字段 → 默认 0，向前兼容。
    public int byproductType;   // (int)ResourceType 副产类型（0=Gold 无副产）
    public int byproductAmount; // 副产已存数量
}
