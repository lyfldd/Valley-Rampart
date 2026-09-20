using System;

/// <summary>
/// 王国状态存档数据（3.5 实施计划 §2.3；KingdomManager，Global）。
/// 字段与文档逐一对齐。
/// </summary>
[Serializable]
public class KingdomSaveData
{
    public int saveDataVersion = 1;
    public string kingdomName = "河谷王国"; // 王国名（2_13 取代君主名；存档显示用）
    public int castleLevel;                 // 主城等级（0=废墟未修复，1-6）
    public int[] moduleLevels;              // 6 模块等级 [土木,生产,民生,军事,商业,科技]
    public int currentDay;                  // 冗余天数（与 TimeManager 交叉校验）
    public int[] tradeQuotaRemaining;       // 各资源贸易剩余额度（索引=资源等级-1，7 档）
    public int[] tradeCooldownDays;         // 各资源额度刷新倒计时（索引=资源等级-1）
    public int[] researchLevels;            // 各研究方向等级（科技模块，P1 占位）
    public int waveProgress;                // 夜间波次进度（P2 占位）

    // ⭐ M1-A：原「国库非金资源持久化」（treasuryStone/Wood/Food/SpecialFood/Meat/Metal/Crystal/FireOil/Ore）
    // 整段退役 —— `09#38`：国库真源＝`TreasureVault` 的**单多资源容器**，随**建筑存档**存取
    // （`BuildingSaveData.treasuryContents`）；`KingdomManager.Treasury*` 读档缓存桥同步退役（判据 5）。
}