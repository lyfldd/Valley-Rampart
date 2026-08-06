using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 学院/工坊研究建筑组件（3.5 王国经营体系 §九 / 实施计划 §13.2；P1-22）。
/// P1 只做「单项目队列」数据结构：一个研究设施同时 1 个进行中项目（currentResearch），
/// 其余请求排队（_queue）。研究项目列表/消耗金+时长定值属 P2（ResearchProject 字段预留）。
///
/// 决策 #22：研究并发 = 单项目队列（一个学院同时 1 个项目）。
/// 数据驱动：研究消耗/时长在 P2 以 ResearchProject 列表 SO 定值，本组件不硬编码。
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

    public IReadOnlyList<ResearchProject> Queue => queue;

    public void Init(Building building)
    {
        _building = building;
        // P1：仅数据结构落地。研究进度推进/完成判定在 P2 接入（ResearchProject.durationDays 天驱动，对齐训练队列）。
    }

    /// <summary>是否空闲（无进行中项目）。</summary>
    public bool IsIdle => currentResearch == null;

    /// <summary>
    /// 请求开始研究（P1 单项目队列）：无进行中项目则立即开始，否则排队。
    /// 返回是否接受请求（入队即接受；本项目不校验资源，资源校验/消耗在 P2 接 ResearchProject.cost）。
    /// </summary>
    public bool TryEnqueueResearch(ResearchProject project)
    {
        if (currentResearch == null)
        {
            currentResearch = project;
            string buildingName = _building != null && _building.def != null ? _building.def.displayName : "学院";
            Debug.Log($"[AcademyBuilding] 开始研究：{project.id}（{buildingName}）");
            return true;
        }
        queue.Add(project);
        Debug.Log($"[AcademyBuilding] 研究排队：{project.id}（当前进行中 {currentResearch.Value.id}，队列 #{queue.Count}）");
        return true;
    }

    /// <summary>取消当前研究 → 队首排队项目晋升为进行中（P1 数据层操作）。</summary>
    public void CancelCurrentResearch()
    {
        if (currentResearch == null) return;
        currentResearch = null;
        if (queue.Count > 0)
        {
            currentResearch = queue[0];
            queue.RemoveAt(0);
        }
    }

    /// <summary>取消指定排队项目（从队列移除）。</summary>
    public void CancelQueued(ResearchProject project)
    {
        queue.Remove(project);
    }
}

/// <summary>
/// 研究项目数据结构（3.5 §九；P1-22 预留）。列表/消耗定值属 P2（科技模块实施时定），
/// P1 仅声明字段供队列数据结构引用，不参与运行时数值决策。
/// </summary>
[Serializable]
public struct ResearchProject
{
    public string id;              // 研究方向 id（P2 定值）
    public string displayName;     // 显示名（P2 定值）
    public int durationDays;       // 研究时长（天，P2 定值）
    public ResourcePack cost;      // 研究消耗（金+时长，P2 定值）
}