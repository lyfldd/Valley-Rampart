# BUGFIX：FormationDef 定义在非同名文件导致阵型查表失败（真因纠正）

> 2026-08-01 编队 P0 场景实跑验证时触发。本文档**纠正**上一版（"FormationTable 资产字段空缺 / m_Script 丢失"）的错误根因判断——上一版的"修复"实际未生效，用户进 Play Mode 后"阵型查表失败：intent=Defense"**仍然持续**。本次定位到真正的结构性根因并彻底修复。

## 现象

用户在 GameScene 进 Play Mode 后，`[FormationController] 阵型查表失败：intent=Defense` 持续报错，调用链：

```
FormationController.ApplyFormation (FormationController.cs:212)
  → formationTable.Lookup(Defense) 返回 null
  → def == null → LogError
```

上一版文档声称用 `manage_scriptable_object` 重建 FormationDef 资产后"Play Mode 验证通过"，但磁盘上资产 `m_Script` 仍是 `{fileID: 0}`，用户重新进 Play Mode 后**报错依旧**——证明上一版验证是假的（只在编辑器模式跑了 `LoadAssetAtPath`，未真正在 Play Mode 验证 `Resources.Load`）。

## 真正根因（结构与上一版判断完全不同）

### 诊断证据链（MCP execute_code 实测）

1. **FormationTable 本身能加载**：`Resources.Load<FormationTable>("Formations/FormationTable")` 返回 OK，其 `m_Script: {fileID: 11500000, guid: 2e6657d1679fe0b4ba84169edac1b6cf, type: 3}` 有效。错误发生在 `Lookup` 返回 null，不是"formationTable 未配置"。
2. **FormationTable 的 4 个引用字段全 NULL**：`defenseFormation/chargeFormation/retreatFormation/garrisonFormation` 在 Play Mode 和编辑器模式下都是 NULL。
3. **GUID 引用本身正确**：FormationTable.asset 引用 `defenseFormation: {fileID: 11400000, guid: ad1264170ac32a5488468a403761ca27}`，而 `AssetPathToGUID("DefenseFormation.asset")` 返回 `ad1264170ac32a5488468a403761ca27`——**GUID 匹配**。所以不是引用丢失。
4. **DefenseFormation.asset 加载失败**：`LoadAssetAtPath<FormationDef>(".../DefenseFormation.asset")` 返回 NULL；`GetMainAssetTypeAtPath` 返回 **`SlotDef`**（不是 FormationDef！）。

### 结构性根因

**FormationDef（ScriptableObject 类）原本定义在 `FormationEnums.cs` 中，而该文件还包含 `SlotDef`（struct）、`TacticIntent`/`BattleLine`/`SlotRole`（enum）等多个类型。**

Unity 对每个 .cs 文件只生成**一个** MonoScript 子资产，其 `m_ClassName` 取文件中第一个类/结构体名。实测：

```
FormationEnums.cs MonoScript:
  m_ClassName = SlotDef        ← 文件里第一个 struct，不是 FormationDef！
  getClass()  = SlotDef
  fileID      = 11500000
```

因此无论 FormationDef 资产的 `m_Script` 怎么填（fileID 11500000 + FormationEnums.cs 的 GUID `6926d1efde8155b4397935a7555ecb02`），Unity 解析 MonoScript 后都得到 `m_ClassName=SlotDef` → 类型解析为 SlotDef（struct）→ 与 FormationDef 不匹配 → **资产加载返回 null**。

这解释了为什么：
- 上一版用 `execute_code` 的 `CreateInstance+CreateAsset` 创建的资产 `m_Script:{fileID:0}`（Unity 无法为 FormationDef 写出有效脚本引用，因为 MonoScript 解析不到它）
- 上一版用 `manage_scriptable_object` 重建后 `m_Script` 仍是 `{fileID:0}`，且 Play Mode 仍失败
- 4 个旧的 `Formation_*.asset`（P0 早期创建）也是坏的——**这个 bug 从 P0 就存在，只是之前没进 Play Mode 验证过编队查表**

### 对照佐证（同名文件 → 能加载）

| ScriptableObject | 所在文件 | 文件名=类名？ | 能加载？ |
|------------------|----------|--------------|---------|
| UnitData | UnitData.cs | ✓ | ✓ |
| FormationTable | FormationTable.cs | ✓ | ✓ |
| FormationDef | ~~FormationEnums.cs~~ | ✗ | ✗（m_ClassName=SlotDef） |

## 修复

### 核心修复：拆分 FormationDef 到同名独立文件

Unity 约定：**ScriptableObject / MonoBehaviour 类必须放在与类名同名的 .cs 文件中**，否则 MonoScript 的 `m_ClassName` 无法正确指向该类。

1. 新建 `Assets/_Game/Systems/AI/Formation/FormationDef.cs`，把 `FormationDef` 类（含 `[CreateAssetMenu]`、所有字段、`StandardSize` 常量）整体搬过去。
2. 从 `FormationEnums.cs` 移除 `FormationDef` 类（保留 `SlotDef`/`TacticIntent`/`BattleLine`/`SlotRole`，这些枚举和结构体不受 MonoScript 问题影响）。
3. `refresh_unity`（compile=request）生成 `FormationDef.cs.meta`。

验证新 MonoScript：
```
FormationDef.cs GUID = f6315aacfe0fcbb4691432ad742c9414
m_ClassName = FormationDef   ✓
getClass()  = FormationDef   ✓
```

### 资产 m_Script 改指新 GUID

把 4 个 FormationDef 资产（DefenseFormation/ChargeFormation/RetreatFormation/GarrisonFormation.asset）的 `m_Script` 改为：

```yaml
m_Script: {fileID: 11500000, guid: f6315aacfe0fcbb4691432ad742c9414, type: 3}
m_EditorClassIdentifier: 
```

（fileID 11500000 现在对 FormationDef.cs 是正确的，因为该文件只有一个类。）

### FormationTable 引用重绑（清导入缓存）

改完 FormationDef 资产后，FormationTable.asset 的引用字段仍 NULL——因为 FormationTable 在 FormationDef 损坏期间被导入，Library 缓存了 null 引用，`ImportAsset(ForceUpdate)` 也清不掉。用 `execute_code` 显式重绑并保存：

```csharp
var ft = AssetDatabase.LoadAssetAtPath<FormationTable>(".../FormationTable.asset");
ft.defenseFormation    = AssetDatabase.LoadAssetAtPath<FormationDef>(".../DefenseFormation.asset");
ft.chargeFormation     = AssetDatabase.LoadAssetAtPath<FormationDef>(".../ChargeFormation.asset");
ft.retreatFormation    = AssetDatabase.LoadAssetAtPath<FormationDef>(".../RetreatFormation.asset");
ft.garrisonFormation   = AssetDatabase.LoadAssetAtPath<FormationDef>(".../GarrisonFormation.asset");
EditorUtility.SetDirty(ft);
AssetDatabase.SaveAssets();
```

## 验证（Resources.Load，与 Play Mode 同路径）

```
DefenseFormation   LoadAssetAtPath = FormationDef  display=防守-将军居中弓两翼  slots=6 ✓
ChargeFormation    LoadAssetAtPath = FormationDef  display=进攻-将军带头弓殿后  slots=6 ✓
RetreatFormation   LoadAssetAtPath = FormationDef  display=撤退-弓先走近战殿后  slots=6 ✓
GarrisonFormation  LoadAssetAtPath = FormationDef  display=守城-兵堵口弓上墙    slots=6 ✓

Resources.Load FormationTable:
  defenseFormation   = 防守-将军居中弓两翼 ✓
  chargeFormation    = 进攻-将军带头弓殿后 ✓
  retreatFormation   = 撤退-弓先走近战殿后 ✓
  garrisonFormation  = 守城-兵堵口弓上墙 ✓
  Lookup(Defense)    = 防守-将军居中弓两翼 ✓（不再 NULL）
  Lookup(Charge)     = 进攻-将军带头弓殿后 ✓
  LookupGarrison()   = 守城-兵堵口弓上墙 ✓
```

## 教训（重要——Unity 通用约束，非团结引擎特有）

1. **ScriptableObject / MonoBehaviour 类必须放在与类名同名的 .cs 文件**。Unity 每 .cs 文件只生成一个 MonoScript，`m_ClassName` 取文件中第一个类/结构体名。若类名≠文件名，且文件里有多个类型，MonoScript 会指向错误的类型，导致资产在 `Resources.Load` / Play Mode 下加载为 null 或错误类型。**枚举/结构体可共用文件，但 ScriptableObject 不要和它们混放。**
2. **`m_Script: {fileID: 0}` 是症状不是病根**。上一版盯着 m_Script:{fileID:0} 以为是"创建路径不对"，反复换 `execute_code`/`manage_scriptable_object` 都没用——因为根因是 MonoScript 解析不到 FormationDef，无论怎么创建资产都写不出有效 m_Script。
3. **验证必须用 `Resources.Load`（Play Mode 同路径），不能用 `LoadAssetAtPath`**。编辑器模式 `LoadAssetAtPath` 靠 `m_EditorClassIdentifier` 兜底能加载部分坏资产，掩盖问题；只有 `Resources.Load` 才反映运行时真实状态。上一版"验证通过"是假的就是因为只跑了 `LoadAssetAtPath`。
4. **`GetMainAssetTypeAtPath` 是快速诊断利器**：返回值≠预期类型即可判定 MonoScript 解析错误，比读 YAML 直观。
5. **导入缓存可能保留 stale 引用**：资产引用的目标在被修复期间被导入过，引用方即使 `ImportAsset(ForceUpdate)` 也可能读缓存的 null。用代码显式重绑字段 + `SaveAssets` 可绕过。
6. 已补进 `unity-mcp-first` skill：创建 ScriptableObject 类须放同名文件 + 用 `Resources.Load` 验证。
