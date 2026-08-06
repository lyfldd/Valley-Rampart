using UnityEngine;

/// <summary>
/// 建筑行为组件接口（3.3.4 批次4 组件化架构）。
/// Building 类只管基础状态 + 交互 + 生命周期，具体行为拆成独立 Component 挂在同一 GameObject。
/// BuildingFactory 按 BuildingDef 配置决定挂哪些组件；组件从 def 读配置，不自己持有持久状态。
///
/// 本轮实现：ProducerComponent + StorageComponent（批次5）。
/// 留接口空壳：Pickup/Spawner/Combat/Rift/CastleCore（定义类 + 挂载判断，具体逻辑后续阶段）。
/// </summary>
public interface IBuildingComponent
{
    /// <summary>组件初始化（BuildingFactory 实例化后调用，传入宿主 Building）。</summary>
    void Init(Building building);
}

// ===== 留接口空壳组件（3.3.4 批次4：定义类 + 挂载判断，具体逻辑后续阶段）=====

/// <summary>一次性采集组件（宝箱/木头堆/石头堆）。依赖：无。后续阶段实现采集逻辑。</summary>
public class PickupComponent : MonoBehaviour, IBuildingComponent
{
    public void Init(Building building) { }
}

/// <summary>产兵组件（兵营）。依赖：单位系统。后续阶段实现。</summary>
public class SpawnerComponent : MonoBehaviour, IBuildingComponent
{
    public void Init(Building building) { }
}

/// <summary>战斗组件（箭塔/投石机/魔法塔）。依赖：3.4 伤害管线 / 3.5 防御建筑。后续阶段实现。</summary>
public class CombatComponent : MonoBehaviour, IBuildingComponent
{
    private Building _building;

    /// <summary>是否可开火（工人操作解锁：Catapult 等 crewRequired>0 建筑需工人操作才可发射，改动②）。</summary>
    public bool IsOperational => _building != null && _building.HasEnoughCrew();

    public void Init(Building building)
    {
        _building = building;
        // 预留：建筑攻击驱动（射程内最近敌 -> DamageSystem.RegisterAttack）在此实现时，
        // 发射前必须 gating on IsOperational——工人不足停火停机（对齐 sim CrewMachineThinkCore）。
        // 现状：建筑战斗仍在"后续阶段实现"，本组件仅落地 crew 解锁接口 + 说明落点。
    }
}

/// <summary>裂隙组件（出怪口）。依赖：3.7 波次系统。后续阶段实现。</summary>
public class RiftComponent : MonoBehaviour, IBuildingComponent
{
    public void Init(Building building) { }
}

/// <summary>主城核心组件（HQ 面板 / 科技解锁 / 失败条件）。批次7 做最小实现支撑主城流程。</summary>
public class CastleCoreComponent : MonoBehaviour, IBuildingComponent
{
    public void Init(Building building) { }
}
