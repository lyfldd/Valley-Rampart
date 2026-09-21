using System;
using System.Collections.Generic;

/// <summary>
/// 箱子域存档载荷（DZ-074 Chest 全链入档 / D269 统一资源容器 / HH.109 件1）。
/// ChestManager.SaveState 序列化全部存活 ChestEntity（三类来源统一覆盖：溢出装箱 D223 /
/// 兽人战利品 TrySpawnOrcLoot / 怪物掉落——经 ChestManager.SpawnChest 唯一入口落箱，来源无关）。
/// 新增 payload 模块自治 version=1，全局 saveVersion 不动（M1 零 schema bump）。
/// 旧档无本条目 → SaveManager 分发跳过，LoadState 不被调用（零兼容负担）。
/// </summary>
[Serializable]
public class ChestSavePayload
{
    /// <summary>payload 版本（模块自治迁移用）。</summary>
    public int version = 1;

    /// <summary>全部存活箱子（读档重建顺序无关；LoadState 先 ClearAll 后重建=幂等）。</summary>
    public List<ChestSaveEntry> chests = new List<ChestSaveEntry>();
}

/// <summary>单箱存档条目（最小集：位置/内容物/生成天/来源阵营。无开启交互态字段——ChestEntity 无此态）。</summary>
[Serializable]
public class ChestSaveEntry
{
    /// <summary>挂格坐标平铺（GridCoord struct 拆 int，避免嵌套类型解析依赖）。</summary>
    public int cellX;
    public int cellY;

    /// <summary>生成绝对天数（过期扫描 curDay - bornDay >= expireDays 依赖，必须原值恢复）。</summary>
    public float bornDay;

    /// <summary>⚠️ `M1-D`/#57 后**语义已废**（新箱恒 `None`）—— 字段**保留仅存档保真**（⛔ 不动存档格式 · `D802` `Q7`）。</summary>
    public int ownerFaction;

    /// <summary>内容物（⭐ `M1-A`／`09#50`：资源量列表 `ResourceList`，任意资源均可承载；`JsonUtility` 直接序列化）。
    /// ⚠️ 旧档（`ResourcePack` 版）**作废**（`D788` §4：游戏未发布 ⇒ 不写存档迁移脚本）。</summary>
    public ResourceList contents;
}
