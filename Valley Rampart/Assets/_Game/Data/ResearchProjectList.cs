using UnityEngine;

/// <summary>
/// 研究项目列表配置（3.5 王国经营体系 §3.6 科技模块；P2-1 研究项目列表/消耗定值）。
/// 科技研究方向（全面小增益，不单点强化）的消耗定值：金 + 研究时长（进度条）。
/// 所有可调数值（研究 id/显示名/等级/时长/消耗）集中于此 SO，禁止硬编码（so-data-driven 铁律）。
/// 资产路径：Resources/Config/ResearchProjectList.asset，Play Mode 用 Resources.Load 加载。
/// SO 类独占同名文件（skill 教训 7：SO 类若与其他类/结构体混放，MonoScript 解析成首类型 → 资产加载失败）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/ResearchProjectList", fileName = "ResearchProjectList")]
public class ResearchProjectList : ScriptableObject
{
    [Tooltip("科技研究方向列表（索引无关，按 id / researchLevel 匹配）")]
    public ResearchProject[] projects;

    /// <summary>按 id 查询研究项目（找不到返回 default）。</summary>
    public ResearchProject GetById(string id)
    {
        if (projects == null || string.IsNullOrEmpty(id)) return default;
        for (int i = 0; i < projects.Length; i++)
            if (projects[i].id == id) return projects[i];
        return default;
    }

    /// <summary>按研究方向等级查询（1/2/3 科技 Lv；找不到返回 default）。</summary>
    public ResearchProject GetByLevel(int level)
    {
        if (projects == null) return default;
        for (int i = 0; i < projects.Length; i++)
            if (projects[i].researchLevel == level) return projects[i];
        return default;
    }
}