// ============================================================================
//  统一国情快照（2_22 P0 批A / A1，D528 三块结构 + D517 态势层军事块）
//  纯 C# 零 UnityEngine 引用（验收硬条款；D459 AbstractEconomySettler 同款防假同步模式）。
//  文件住 Systems/AI/KingdomBrain/ 域；日 tick 全量重建（纯函数聚合），事件只置脏标记
//  不即时改快照（KingdomBrain 侧守卫，A2）。
//
//  三块结构（D528，2_23 §2.1 权威）：
//    ① 军事态势块=本批真值（D517 五件聚合+D589 内源节拍时间场）
//    ② 经济诊断块=占位（真值随 2_23 资源 P0 R-A1 填充，零二次改造）
//    ③ 人口盘点块=占位（真值随 2_23 资源 P1 填充）
//
//  内源节拍地基（D589 列报2 归域兑现）：peaceDays=自最近一次外部接触（被攻/本国单位
//  战死）以来的天数——纯内源时间场，无事件日自然递推；供评分域军事缺口消费
//  （A4：缺口评分不依赖外部威胁信号，威胁=0 时缺口依然评分）。
//  六考设计输入（D589）：袭扰段 73311（D6 即扩张=外部刺激驱动样本）vs 正门段 69496
//  （117 日阶段零推进=无刺激死滞样本）——本字段为「无袭扰环境阶段推进不得恒死滞」
//  的态势层侧数据地基。
// ============================================================================

/// <summary>威胁分布条目：一个邻接王国的兵力快照（D517①；邻接口径=A5 AreKingdomsAdjacent）。</summary>
public struct ThreatEntry
{
    public int KingdomId;      // 邻接王国 id
    public int WarriorCount;   // 该国战士数（昨日结存口径，与 D348 分子同源）
}

/// <summary>损毁清单条目（D517④；窗口 TTL=SituationConfig.lossTtlDays 滚动清除）。</summary>
public struct LossEntry
{
    public int Day;            // 损毁发生日（游戏日历）
    public bool IsBuilding;    // true=建筑损毁，false=单位死亡
    public int OccupationId;   // 单位职业 int（IsBuilding=false 时有效；建筑面批A 计数不计类）
}

/// <summary>兵种表现统计骨架条目（D517⑤；窗口内 per 职业死亡计数——批A 统计地基，
/// 存活率/交换比表现分=批B B5 局内环自整定战斗结算域扩展，本批零行为消费）。</summary>
public struct UnitPerfStat
{
    public int OccupationId;   // 职业（Occupation 枚举 int 序列化稳定）
    public int Deaths;         // 窗口内死亡数（Cause=Killed 才计，Demolished=拆除不计）
}

/// <summary>
/// 统一国情快照（D528）。日 tick 全量重建，无持久态不入档（2_22 §3.6 八格：
/// 诞生=随日 tick 重建）。
/// </summary>
public class SituationSnapshot
{
    // ===== 身份 =====
    public int KingdomId;      // 快照所属王国
    public int Day;            // 重建日（游戏日历；子步序断言锚：快照 Day==当前日）

    // ===== ① 军事态势块（D517 五件聚合，本批真值）=====

    /// <summary>军力现状：本国战士数（含机器战力口径位——B8 机器落地前 MachineCount 恒 0=零行为差异，D570）。</summary>
    public int OwnWarriorCount;
    /// <summary>战争机器数（B8 建机器链落地前恒 0；军力现状口径预留，D570）。</summary>
    public int MachineCount;

    /// <summary>威胁分布：per 邻接王国（D517①；非邻接国不入表=A5 邻接修正同口径）。</summary>
    public System.Collections.Generic.List<ThreatEntry> Threats;

    /// <summary>边境接触面：邻接王国数（D517② 批A 口径=邻接国计数；边境格级计数 P1 按需扩展）。</summary>
    public int BorderContactCount;

    /// <summary>损毁清单：窗口内本国单位死亡+建筑损毁（D517④；TTL 滚动）。</summary>
    public System.Collections.Generic.List<LossEntry> Losses;

    /// <summary>兵种表现统计骨架：窗口内 per 职业死亡计数（D517⑤；确定性窗口平均，B5 扩展表现分）。</summary>
    public System.Collections.Generic.List<UnitPerfStat> UnitPerformance;

    // ===== 军事维度缺口真值（A4 缺口函数读快照的数据源；D589 内源节拍=缺口即评分不乘威胁门控）=====

    /// <summary>本国将军数（GeneralGap 消费；口径对齐 TrainingSystem.CanTrainGeneral 按 kingdomId 扫 UnitRegistry）。</summary>
    public int GeneralCount;

    /// <summary>本国编队数（FormationGap 消费；FormationManager 注册表按编队归属国过滤）。</summary>
    public int FormationCount;

    /// <summary>本国在场战斗职业去重清单（UnitTypeGap 消费；确定性升序——可训域多样性缺口的「拥有」侧）。</summary>
    public System.Collections.Generic.List<int> OwnedCombatOccupations;

    // ===== 内源势能输入（D590 增补节②：经济盈余率/人口压力/仓储水位三连续输入——评分域军事行动
    //      内源节拍项消费；0~1 归一。数据源=BuildSituation 从 KingdomState 现算（轻量口径）；
    //      R-A1 经济诊断块落地后由真值块接替（交接关系见 2_23 §2.1）。）=====
    /// <summary>经济盈余率 0~1（国库 gold 相对基线水位；现算口径=gold/100 clamp01，R-A1 后接替）。</summary>
    public float DriveEconomic;
    /// <summary>人口压力 0~1（工人对住房容量占比=capacityCount/10 clamp01；现算口径，资源 P1 后接替）。</summary>
    public float DrivePopPressure;
    /// <summary>仓储水位 0~1（粮/储备容量占比 clamp01；现算口径，R-A1 后接替）。</summary>
    public float DriveStorage;

    // ===== 内源节拍时间场（D589 列报2；批A 地基）=====

    /// <summary>和平持续天数：自最近一次外部接触（被攻/本国单位被击杀）以来的天数；
    /// 无接触日日 tick 递推，接触当日清零。-1=开局尚无接触记录（首日=未接触态）。</summary>
    public int PeaceDays;

    /// <summary>脏标记（A2 负探针锚）：日 tick 消费后清零；事件到达置位但不改本快照任何聚合值。</summary>
    public bool Dirty;

    // ===== ② 经济诊断块（占位——真值随 2_23 资源 P0 R-A1 填充，零二次改造 D528）=====

    /// <summary>经济诊断块占位：R-A1 四件真值（五元收支/产能盘点/断链检测/储备水位）落此域。
    /// 批A 只预留挂点不预造字段（防超前返工=清单范围声明），结构扩展权归 2_23 清单。</summary>
    public bool EconomyBlockPlaceholder;

    // ===== ③ 人口盘点块（占位——真值随 2_23 资源 P1 填充）=====

    /// <summary>人口盘点块占位：资源 P1 ⑲人口调控/职业网状真值落此域（同上占位口径）。</summary>
    public bool PopulationBlockPlaceholder;
}

/// <summary>
/// 态势快照中枢（A4 读快照的取数面）：KingdomBrain 日 tick 重建后 Put，
/// UtilityScorer 军事缺口函数经 Get 消费——不改 ScoreTop/NeedScore 签名（既有经济维度
/// 调用方零改动）。纯 C# 静态槽；快照不入档，读档后首个日 tick 自动覆盖（八格无持久态）。
/// </summary>
public static class SituationHub
{
    private static readonly System.Collections.Generic.Dictionary<int, SituationSnapshot> _map
        = new System.Collections.Generic.Dictionary<int, SituationSnapshot>();

    /// <summary>写入/覆盖某国快照（日 tick 重建后调用）。</summary>
    public static void Put(int kingdomId, SituationSnapshot snapshot) => _map[kingdomId] = snapshot;

    /// <summary>读取某国快照（无则 false——评分侧缺口函数回退 0 分防 NRE）。</summary>
    public static bool TryGet(int kingdomId, out SituationSnapshot snapshot) => _map.TryGetValue(kingdomId, out snapshot);

    /// <summary>王国灭亡/退订时移除（随 KingdomBrain.Unsubscribe 调用）。</summary>
    public static void Remove(int kingdomId) => _map.Remove(kingdomId);

    /// <summary>全清（harness 两轮间/新开局归零用，对齐 KingdomBrain.ResetDispatchStats 先例）。</summary>
    public static void Clear() => _map.Clear();
}

/// <summary>
/// 军事职业判定（D519① 特征清单域=策划规则，非可调数值；枚举 switch 无数据面）。
/// 供 UnitTypeGap 缺口函数判定「军事训练域兵种」（A4）；批B B6 选招可训域过滤可复用。
/// </summary>
public static class MilitaryProfessions
{
    /// <summary>是否战斗职业（2_20 M7 专属兵种段 28~37 段内尾插，枚举 int 稳定性锚 UnitData.cs）。</summary>
    public static bool IsCombat(Occupation o)
    {
        switch (o)
        {
            case Occupation.General:
            case Occupation.Warrior:
            case Occupation.Archer:
            case Occupation.Mage:
            case Occupation.Healer:
            case Occupation.Crossbowman:
            case Occupation.HeavyWarrior:
            case Occupation.Bishop:
            case Occupation.ShieldGuard:
            case Occupation.Archmage:
            case Occupation.Cavalry:
            // 2_20 M7 四族专属兵种+专属机器（尾插段 D490~D497，UnitData.cs L43~52 全段十枚举）
            case Occupation.Berserker:
            case Occupation.WolfRider:
            case Occupation.Musqueteer:
            case Occupation.Bedrock:
            case Occupation.Ranger:
            case Occupation.Windwalker:
            case Occupation.DeerRider:
            case Occupation.Mortar:
            case Occupation.VineCatapult:
            case Occupation.Ram:
                return true;
            default:
                return false;
        }
    }
}
