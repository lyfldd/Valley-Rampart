/// <summary>
/// 王国六大经营模块（3.5 §二）。
/// 与 ModuleDef.moduleId 字符串约定一致（Civil/Production/Livelihood/Military/Commerce/Science）。
/// 枚举值稳定，moduleLevels[6] 与 DotNet Name 映射见 KingdomManager。
/// </summary>
public enum ModuleType
{
    Civil,          // 土木（防御工事 + 建筑等级上限）
    Production,     // 生产（资源产出 + 加工 + 存储）
    Livelihood,     // 民生（人口容量 + 饱食 + 幸福）
    Military,       // 军事（兵种 + 装备 + 将军）
    Commerce,       // 商业（金来源：税 + 贸易 + 金矿）
    Science         // 科技（跨模块全面小增益）
}