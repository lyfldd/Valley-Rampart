using System;
using System.Collections.Generic;

/// <summary>
/// 存档摘要。存档槽 UI 显示用，只含展示需要的字段。
/// 由 SaveManager 在 Save 时收集。
/// </summary>
[Serializable]
public class GameSaveSummary
{
    public string rulerName;       // 统治者名字
    public int currentDay;         // 第几天
    public int currentSeason;      // 季节
    public int difficulty;         // 难度
}

/// <summary>
/// 存档根容器。一个存档槽 = 一个 GameSaveRoot 实例 = 一个 JSON 文件。
/// </summary>
[Serializable]
public class GameSaveRoot
{
    // —— 元数据 ——
    public int saveVersion = 1;       // 全局存档格式版本
    public string saveTime;           // 时间戳（yyyy-MM-dd HH:mm:ss）
    public string slotName;

    /// <summary>是否已结束（GameOver 后标记为 true）。存档槽 UI 据此显示"已结束"状态。</summary>
    public bool isFinished;

    // —— 存档摘要（供存档槽 UI 快速读取，避免反序列化所有模块）——
    public GameSaveSummary summary = new GameSaveSummary();

    // —— 模块数据（List 而非 Dictionary，因为 JsonUtility 不支持 Dictionary）——
    public List<ModuleSaveEntry> modules = new List<ModuleSaveEntry>();
}
