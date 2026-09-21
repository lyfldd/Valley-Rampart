using UnityEngine;

/// <summary>
/// 任务终点类型（QQQ.2 §10.1）。派发时由调度器动态解析，不硬编码坐标。
/// </summary>
public enum KingdomDestType
{
    None,             // 无终点（原地劳作）
    Treasury,         // 国库
    NearestWarehouse, // 最近可用仓库（无则回退国库）
    // ⭐ `M1-F` 起**退役**（`D807` Q6）：水不再有"网"这种容器 ⇒ 本值保留**占位**（序列化稳定面 · ⛔ 不删不重编号），
    //   代码侧不再写入/解析；搬水任务改走 `SpecificBuilding`（终点＝农场）。
    WaterNetwork,
    SpecificBuilding, // 指定建筑
    // ===== 2_12 步骤9 弹药（D207~D212，HH.19 A×4；末尾追加保持序列化稳定）=====
    UnitMagazine      // 单位弹仓（战争机器/塔本体；装填任务到达即把背包弹药写入 Ammo* 字段）
}

/// <summary>
/// 通用任务抽象（QQQ.2 §10.1，DR-16）。任务带 destType 不硬编码终点；
/// 派发时调度器按 destType 实时解析 destPos。
/// 任务被 NPC.currentTask 引用即视为占用（幂等，DR-17）。
/// </summary>
public class KingdomTask
{
    public KingdomTaskType type;      // 任务类型
    public ITaskSource source;        // 来源对象（建筑/资源点），提供 sourcePos
    public KingdomDestType destType;  // 终点类型
    public Vector2 destPos;           // 派发时由调度器动态解析，非硬编码
    public object args;               // 任务参数（产出量、目标物等）
    public float intensity;           // 刺激强度

    public KingdomTask(KingdomTaskType type, ITaskSource source, float intensity = 1f)
    {
        this.type = type;
        this.source = source;
        this.intensity = intensity;
        this.destType = KingdomDestType.None;
    }

    /// <summary>任务源世界坐标（无源返回 zero）。</summary>
    public Vector2 SourcePos => source != null ? source.SourcePos : Vector2.zero;
}

/// <summary>
/// 任务源接口（QQQ.2 §10.1/§10.3，DR-16）。建筑/资源点实现此接口按需"声明"任务。
/// 生命周期挂钩：OnRegister/OnUnregister 由调度器在注册表维护时回调。
/// </summary>
public interface ITaskSource
{
    /// <summary>任务源是否仍有效（尚未被摧毁/失效）。NPCBrain 访问 currentTask 前必须校验（QQQ.3 B1-3 / NPC-A6）。</summary>
    bool IsValid { get; }

    /// <summary>任务源世界坐标（调度器分派用距离排序）。</summary>
    Vector2 SourcePos { get; }

    /// <summary>尝试发布一个任务（按优先级/条件）。无条件发布返回 false。</summary>
    bool TryAdvertiseTask(out KingdomTask task);

    /// <summary>注册到调度器时回调（Building.OnSpawn 调 Register 时触发）。</summary>
    void OnRegister();

    /// <summary>从调度器注销时回调（Building.Die 调 Unregister 时触发）。</summary>
    void OnUnregister();
}

/// <summary>装填任务参数（2_12 步骤9，HH.19 A×4）：该源（塔/机器）需要补的弹种与数量。</summary>
public class ReloadAmmoArgs
{
    public ResourceType ammoType;   // 需要的弹种（StoneAmmo/FireballAmmo/MagicAmmo）
    public int amount;              // 需要数量（缺口量，≤单次搬运携带量由调度器 clamp）
}

/// <summary>
/// ⭐ 搬料任务参数（`09` §16.1 ② · `M1-C` 件1）：**仓库 → 工地仓** 的投料任务。
///
/// 语义（⛔ 不新增 `KingdomTaskType` 枚举 —— 裁决收口 1「搬料任务须可区分语义」）：
///   · 任务类型沿用 `KingdomTaskType.Build`（原死值 · 生产码零调用方），**靠本 args 类型区分语义**；
///   · 任务源 ＝ `ConstructionSiteStore`（工地仓）⇒ `SourcePos` ＝ **取料仓位置**（第一段位移）；
///   · `destPos` ＝ **工地位置**（第二段位移 ＝ 卸料点）；
///   · `need` ＝ 本次搬运的**缺口量**（阈值上限 · `09` §16.1-2 阈值拦截）。
/// </summary>
public class HaulToSiteArgs
{
    public ConstructionSiteStore site;   // 卸料落点（工地仓 · 阈值拦截在此生效）
    public ResourceType resourceType;    // 本次搬的资源
    public StorageComponent pickup;      // 取料仓（解析后的最近同国仓；装载段读它）
    public int need;                     // 缺口量（装载上限 · ⛔ 不多搬）
}

/// <summary>
/// ⭐ 搬水任务参数（`M1-F` 件4 · `09#44`「水按资源处理」）：**水井仓 →（工人搬）→ 农场仓** 两段式任务。
///
/// 语义（⛔ 不新增 `KingdomTaskType` 枚举 —— 沿用 `WaterHaul`，靠本 args 类型区分语义）：
///   · 任务源（`task.source`）＝ **水井**（`Building`）⇒ `SourcePos` ＝ 水井位置（**第一段位移 ＝ 去水井取水**）；
///   · `destType = SpecificBuilding` ＋ `destPos` ＝ **农场位置**（第二段位移 ＝ 去农场卸水）；
///   · `target` ＝ 农场仓（卸水落点）；`need` ＝ 缺口量（装载上限 · ⛔ 不多搬）。
/// ⚠️ ⛔ 不复用 `HaulToSiteArgs`：其 `site` 字段类型是 `ConstructionSiteStore`（工地仓专用），
///    而水的卸水落点是**普通仓**（`StorageComponent`）⇒ 类型不兼容（照 `M1-C` 件1 先例形制另立）。
/// </summary>
public class HaulWaterArgs
{
    public StorageComponent target;   // 卸水落点（农场仓 · `M1-F` 件3 的耗水仓）
    public int need;                  // 缺口量（装载上限 · ⛔ 不多搬）
}

/// <summary>
/// ⭐ 拆除任务参数（`09` §16.3-3 · `M1-C` 件4）：拆除**要耗时与工人**（与建造对称）。
/// 任务源 ＝ 拆除中的 `Building` 本体；`destType = None`（原地劳作 · 第一段位移即工地）。
/// 工人到场 Working 期间推进 `Building` 的拆除进度（`09` §16.3.1「工人侧按进度推进」）。
/// </summary>
public class DemolishTaskArgs
{
    public Building target;   // 被拆建筑
}