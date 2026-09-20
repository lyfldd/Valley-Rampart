using UnityEngine;

/// <summary>
/// 建筑存档数据（3.3.4 批次10 接口预留）。
/// 字段定义就位，序列化/反序列化逻辑后续阶段实现。
/// Building 实现 ISaveable 时用此结构（含组件存档：StorageComponent 存量条目 / TreasureVault 存量条目）。
///
/// 存档范围（见 3.3.4 §10.5）：
/// - Building 核心：type/coord/level/hp/faction/state/footprint/rotation
/// - StorageComponent：`storageContents`（多资源 · M1-A）
/// - TreasureVault（国库容器）：`treasuryContents`（M1-A）
/// - ProducerComponent：无需存档（每秒重算）
/// - BuildingFactory 重建：根据 defId 重新挂组件 + 恢复仓内容
/// </summary>
[System.Serializable]
public struct BuildingSaveData
{
    public string defId;        // BuildingDef.id（重建时查映射表）
    public int coordX;          // GridCoord.x（footprint 左上格）
    public int coordY;          // GridCoord.y（2_2：1D -> 2D 纵轴；旧档缺字段 -> 0）
    public int footprintW;      // 占地宽（2_2；旧档缺字段 -> 0 由 SpawnFromSave 兜底 def.footprint.x）
    public int footprintH;      // 占地高（2_2；旧档缺字段 -> 0 由 SpawnFromSave 兜底 def.footprint.y）
    public int level;
    public int hp;
    public int maxHp;
    public int faction;         // (int)Faction
    public int state;           // (int)BuildingState
    public int sourceType;      // (int)BuildingType
    /// <summary>
    /// 本地仓内容（⭐ `M1-A`：`storedAmount`(int) ⇒ **资源量列表**（多资源容器 · `09#36`）；
    /// 旧档（单 int 版）**作废** —— `D788` §4：游戏未发布 ⇒ 不写存档迁移脚本）。
    /// </summary>
    public ResourceList storageContents;
    /// <summary>国库内容（⭐ `M1-A`：`TreasureVault` 的**单多资源容器**；非主城建筑恒为空列表）。
    /// 原「`KingdomManager.Treasury*` 读档缓存桥」退役后，国库改由本字段随建筑存档存取。</summary>
    public ResourceList treasuryContents;
    // DZ-072a（D562 / HH.107 件1）：矿洞副产组件（MineByproductComponent）双仓存量（无则 0）。
    // 旧档缺字段 → 默认 0 向前兼容，零 bump（M10）。
    public int byproductCrystalAmount; // 水晶副产子仓存量
    public int byproductFireOilAmount; // 火油副产子仓存量
    public int byproductOreAmount;     // 矿石伴生子仓存量（T1.4/D609；尾插零 bump，旧档缺→0）
    // QQQ.3 B8-5 / LC-B2：grade 入档（修复读档后产能建筑永久降贫瘠档 rate×0.7）。
    public int grade;           // (int)ResourceGrade 资源等级（仅资源点建筑有效；旧档缺字段→默认 0=Barren 但由 SpawnFromSave 兜底 Normal）
    // 2_12 步骤7 / D155：累计投入（修复成本基数 / 拆除返还基数）。旧档缺字段→默认 0（D155 兜底按 def.cost 算）。
    public int totalInvested;   // 建造+升级累加投入总量
    // 2_16 步骤2：王国归属（D329 门面）。旧档缺字段→默认 0（玩家），向后兼容。
    public int kingdomId;       // 王国归属 id（0=玩家；AI/动态王国=Registry id）
    // 【HH.294 片4·4-E】锚点消费记录（`03` §7.8 锚点返还对偶：拆了建筑要还锚点）。
    // 尾插 ＋ 旧档缺字段→默认 0/-1 向前兼容（-1 ＝ 无锚点，零 bump）。
    public int anchorCoordX;    // 消费掉的锚点格 x（-1 ＝ 无）
    public int anchorCoordY;    // 消费掉的锚点格 y（-1 ＝ 无）
    public int anchorFeature;   // (int)FeatureType（-1 ＝ 无）

    // ===== ⭐ `M1-C` 件1／件4：投料态与拆除态（判据 7：新档能存能读 · 尾插零 bump）=====

    /// <summary>本次投料需求（配方量；金已在下单时直扣 ⇒ ⛔ 不含金 · 裁决 2 金-A）。</summary>
    public ResourceList siteNeed;
    /// <summary>工地仓当前内容物（`ConstructionSiteStore.Contents`；非投料态恒空列表）。</summary>
    public ResourceList siteContents;
    /// <summary>是否处于「投料未齐」态（Constructing 但进度不推进）。</summary>
    public bool awaitingMaterials;
    /// <summary>是否正在拆除（`09` §16.3-3：拆除有耗时与工人 · 非瞬时）。</summary>
    public bool demolishing;
    /// <summary>拆除进度 0→1。</summary>
    public float demolishProgress;
}
