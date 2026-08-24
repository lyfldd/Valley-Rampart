using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 学院/工坊研究建筑组件（3.5 王国经营体系 §九 / 实施计划 §13.2；P1-22 + QQQ.2 Q4 完整落地）。
///
/// 研究规则（3.5 §九 研究系统：单项目队列，一个研究设施同时 1 个进行中项目）：
///   - 开始研究即扣金（project.cost，资源不足拒绝），排队等空位（队列先进先出）。
///   - 天数驱动推进（对齐 TrainingSystem）：当前项目完成（durationDays 天）→ 提升科技研究等级
///     （KingdomManager.ApplyResearch → Science 模块研究等级）→ 队首晋升。
///   - 研究建筑被摧毁：中断研究 + 清空队列（已投入资源不退，无学者 NPC）。
/// 数据驱动：研究项目列表 SO（Resources/Config/ResearchProjectList，消耗金+时长定值）。
/// 由 BuildingFactory 对 Science 模块建筑挂载（学院/工坊均为研究设施）。
/// </summary>
public class AcademyBuilding : MonoBehaviour, IBuildingComponent
{
    /// <summary>进行中研究项目（单项目队列头；null=空闲可开始新研究）。</summary>
    [Tooltip("进行中研究项目（单项目；null=空闲）")]
    public ResearchProject? currentResearch;

    [Tooltip("排队中研究项目（等待空位）")]
    public List<ResearchProject> queue = new List<ResearchProject>();

    private Building _building;
    private ResearchProjectList _projectList;
    private int _startDay;      // 进行中项目开始游戏天数（天数驱动，对齐 TrainingSystem）
    private int _lastDay;       // 防同一天重复推进

    /// <summary>研究完成事件（研究面板/增益表现订阅）。</summary>
    public event Action<ResearchProject> OnResearchCompleted;

    public IReadOnlyList<ResearchProject> Queue => queue;

    /// <summary>进行中项目剩余天数（UI 显示；null=空闲）。</summary>
    public int RemainingDays
    {
        get
        {
            if (currentResearch == null || TimeManager.Instance == null) return 0;
            int elapsed = Mathf.Max(0, TimeManager.Instance.CurrentDay - _startDay);
            return Mathf.Max(0, currentResearch.Value.durationDays - elapsed);
        }
    }

    public void Init(Building building)
    {
        _building = building;
        _projectList = Resources.Load<ResearchProjectList>("Config/ResearchProjectList");
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    /// <summary>是否空闲（无进行中项目）。</summary>
    public bool IsIdle => currentResearch == null;

    /// <summary>
    /// 请求开始研究（QQQ.2 Q4 完整版）：校验（Science 模块解锁 + 资源可付）→ 扣金 → 入队/立即开始。
    /// 返回是否接受请求；资源不足 / 模块未解锁返回 false。
    /// </summary>
    public bool TryEnqueueResearch(ResearchProject project)
    {
        if (project.id == null) return false;

        // 模块解锁门槛：研究等级 ≤ 科技模块等级（主城升级解锁 Science 模块）
        int moduleLevel = KingdomManager.Instance != null
            ? KingdomManager.Instance.GetModuleLevel(ModuleType.Science) : 0;
        if (moduleLevel < project.researchLevel)
        {
            Debug.LogWarning($"[AcademyBuilding] 研究被拒：{project.displayName} 需科技模块 Lv.{project.researchLevel}（当前 {moduleLevel}，先升级主城）");
            return false;
        }

        // 资源校验 + 扣费（开始研究即扣，中断不退——对齐训练规则）
        var ruler = RulerController.Instance;
        if (ruler == null || !ruler.CanAfford(project.cost))
        {
            Debug.LogWarning($"[AcademyBuilding] 研究被拒：资源不足（{project.displayName} 需金{project.cost.gold}）");
            return false;
        }
        ruler.Spend(project.cost);

        if (currentResearch == null)
        {
            currentResearch = project;
            _startDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
            _lastDay = _startDay;
            string buildingName = _building != null && _building.def != null ? _building.def.displayName : "学院";
            Debug.Log($"[AcademyBuilding] 开始研究：{project.displayName}（{buildingName}，{project.durationDays} 天）");
            return true;
        }
        queue.Add(project);
        Debug.Log($"[AcademyBuilding] 研究排队：{project.displayName}（当前进行中 {currentResearch.Value.displayName}，队列 #{queue.Count}）");
        return true;
    }

    /// <summary>取消当前研究 → 队首排队项目晋升为进行中（资源不退）。</summary>
    public void CancelCurrentResearch()
    {
        if (currentResearch == null) return;
        currentResearch = null;
        if (queue.Count > 0)
        {
            currentResearch = queue[0];
            queue.RemoveAt(0);
            _startDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
            _lastDay = _startDay;
        }
    }

    /// <summary>取消指定排队项目（从队列移除，资源不退）。</summary>
    public void CancelQueued(ResearchProject project)
    {
        queue.Remove(project);
    }

    /// <summary>
    /// 获取当前可研究项目列表（QQQ.2 Q4）。
    /// 规则：researchLevel > 当前科技研究等级（尚未研究）且 researchLevel ≤ 科技模块等级（研究等级 ≤ 模块等级）。
    /// 当前科技研究等级由 KingdomManager.GetResearchLevel(Science) 提供。
    /// </summary>
    public List<ResearchProject> GetAvailableProjects()
    {
        var result = new List<ResearchProject>();
        if (_projectList == null || _projectList.projects == null) return result;
        int techLevel = KingdomManager.Instance != null
            ? KingdomManager.Instance.GetResearchLevel(ModuleType.Science) : 0;
        int moduleLevel = KingdomManager.Instance != null
            ? KingdomManager.Instance.GetModuleLevel(ModuleType.Science) : int.MaxValue;
        for (int i = 0; i < _projectList.projects.Length; i++)
        {
            var p = _projectList.projects[i];
            if (p.researchLevel > techLevel && p.researchLevel <= moduleLevel)
                result.Add(p);
        }
        return result;
    }

    /// <summary>研究项目配置列表（面板显示全部项目用；null=未加载）。</summary>
    public ResearchProjectList ProjectList => _projectList;

    // ===== 天数驱动推进（对齐 TrainingSystem.Update）=====

    void Update()
    {
        if (currentResearch == null || TimeManager.Instance == null) return;
        int day = TimeManager.Instance.CurrentDay;
        if (day == _lastDay) return;   // 同一天只推进一次
        _lastDay = day;

        if (day - _startDay >= currentResearch.Value.durationDays)
        {
            var done = currentResearch.Value;
            currentResearch = null;

            // 完成效果：提升科技研究等级（3.5 研究系统闭环）
            if (KingdomManager.Instance != null)
                KingdomManager.Instance.ApplyResearch(done);

            OnResearchCompleted?.Invoke(done);
            if (_building != null)
                EventBus.Publish(new ResearchCompletedEvent(done, _building));

            // 队首晋升
            if (queue.Count > 0)
            {
                currentResearch = queue[0];
                queue.RemoveAt(0);
                _startDay = day;
            }
            Debug.Log($"[AcademyBuilding] 研究完成：{done.displayName}（科技研究等级提升）");
        }
    }

    /// <summary>研究建筑被摧毁：中断研究 + 清空队列（3.5.4 §8.6 对齐：资源不退，无学者 NPC）。</summary>
    void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit as Building != _building) return;
        if (currentResearch != null)
            Debug.Log($"[AcademyBuilding] 研究中断：{currentResearch.Value.displayName}（建筑被毁，资源不退）");
        currentResearch = null;
        queue.Clear();
    }
}

/// <summary>
/// 研究项目数据结构（3.5 §九；P1-22 预留 + QQQ.2 Q4 启用）。列表/消耗定值见 ResearchProjectList SO。
/// </summary>
[Serializable]
public struct ResearchProject
{
    public string id;              // 研究方向 id
    public string displayName;     // 显示名
    public int researchLevel;      // 研究方向等级（1/2/3 科技 Lv，匹配模块等级门槛）
    public int durationDays;       // 研究时长（天）
    public ResourcePack cost;      // 研究消耗（金+时长）

    // 2_12 步骤13（D224~D227）：科技"少量全增益+解锁新内容"数值。研究完成产生真实效果，SO 可调（so-data-driven）。
    // 默认 0 = 无该效果（聚合倍率时仅纳入 >0 项）；"解锁新内容"类（铁匠铺/弩塔/魔法塔）经 BuildingDef.requiredTechId 匹配本 id 门控可建。
    [Tooltip("研究后市场每日贸易额度倍率（0=无贸易效果）")]
    public float tradeQuotaMult;        // 贸易→图层：每日额度 ×本值
    [Tooltip("研究后建筑建造/升级效率倍率（0=无加速；0<x<1 缩短施工时长）")]
    public float buildEfficiencyMult;   // 建筑→建筑: 施工/升级时长 ×本值
    [Tooltip("研究后牧场容量倍率（0=无效果）")]
    public float ranchCapacityMult;     // 畜牧→牧场Lv2: 牧场容量 ×本值
}
