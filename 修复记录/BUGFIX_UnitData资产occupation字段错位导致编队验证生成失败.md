# BUGFIX：UnitData 资产 occupation 字段错位导致编队验证生成失败

> 2026-08-01 编队 P0 场景实跑验证时触发。修复 5 个 .asset 的 occupation 字段错位。

## 现象

进 Play Mode 跑 `CombatTestSpawner` 验证 3.0.1_3 编队系统，Console 连报 10 条错误：

```
[UnitDataManager] 找不到数据: [Human_Player_Civilian]。可用: Human_Player_General, Human_Player_Warrior, Human_Player_Ruler, Human_Player_Archer, Undead_General, Undead_Archer
[UnitFactory] UnitData 为空，无法创建单位。
[3.0.1_3 验证] 生成失败: Human_Player_Civilian
（Undead_Warrior 同上 ×2 组）
[FormationController] 阵型查表失败：intent=Defense
```

调用链：`CombatTestSpawner.SpawnTestUnits` → `SpawnUnit(Faction, Occupation)` → `UnitFactory.SpawnUnit` → `UnitDataManager.GetData` 返回 null → UnitData 为空 → 生成失败。编队因成员招募不全，`ApplyFormation` 查表落空。

## 根因

**UnitData 资产的 occupation 字段错位**——文件名与字段实际值不符。`UnitDataManager` 的缓存 key 是 `{faction}_{occupation}`，用的是**字段值**而非文件名（[UnitDataManager.cs:41](../Valley%20Rampart/Assets/_Game/Systems/Unit/UnitDataManager.cs#L41)）。

用 Unity MCP `execute_code` dump 全部 7 个资产发现：

| 文件名（期望职业） | occupation 字段实际值 | walk | hp | atk | def | 数值符合哪个职业 |
|-------------------|---------------------|------|-----|-----|-----|----------------|
| Human_Player_Archer.asset | **General** ❌ | 3 | 80 | 8 | 10 | Archer（远程脆皮） |
| Human_Player_Civilian.asset | **Warrior** ❌ | 3 | 80 | 5 | 0 | Civilian（无防平民） |
| Human_Player_General.asset | General ✓ | 3 | 150 | 15 | 30 | General（肉盾） |
| Human_Player_Ruler.asset | Ruler ✓ | 25 | 100 | 10 | 5 | Ruler（君主骑马） |
| Human_Player_Warrior.asset | **Archer** ❌ | 3 | 100 | 10 | 20 | Warrior（近战） |
| Undead_Archer.asset | **General** ❌ | 3 | 80 | 8 | 10 | Archer |
| Undead_Warrior.asset | **Archer** ❌ | 3 | 100 | 10 | 20 | Warrior |

**关键**：数值字段（walkSpeed/maxHp/attack/defense）全部正确，符合各文件名对应的职业定位；**只有 occupation 字段错位**。推断为用 MCP 生成资产时按文件名填了正确数值，但 occupation 枚举赋值时填错（可能是循环变量串位或手误）。

**为何潜伏至今**：
- 3.0.1 注意力系统验证只用"工人+士兵双职业"。Spawner 查 `Human_Player_Warrior` 时，`Civilian.asset`（occupation=Warrior）冒名顶替，key 命中返回数据，于是"士兵"能生成——但用的是 Civilian 的数值（hp80/atk5/def0），而非真正的 Warrior（hp100/atk10/def20）。
- 直到 3.0.1_3 的 `CombatTestSpawner` 按 5 种职业生成（General/Warrior/Archer/Civilian + 敌方 Warrior），要查 `Human_Player_Civilian` 和 `Undead_Warrior` 这两个 key，缓存里根本没有（被顶替成 Warrior/Archer 了），才集中爆发。

## 修复点

5 个 `.asset` 文件的 `occupation` 字段（数值不动）：

| 资产 | 修改 |
|------|------|
| `Assets/Resources/UnitData/Human_Player_Archer.asset` | occupation: General → Archer |
| `Assets/Resources/UnitData/Human_Player_Civilian.asset` | occupation: Warrior → Civilian |
| `Assets/Resources/UnitData/Human_Player_Warrior.asset` | occupation: Archer → Warrior |
| `Assets/Resources/UnitData/Undead_Archer.asset` | occupation: General → Archer |
| `Assets/Resources/UnitData/Undead_Warrior.asset` | occupation: Archer → Warrior |

修复手段：Unity MCP `execute_code` 反射 `FieldInfo.SetValue` + `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets/Refresh`（遵循 `unity-mcp-first` skill，不写编辑器脚本）。

## 验证

1. MCP dump 确认 7 个资产的 `faction_occupation` key 全部等于文件名 ✓
2. `EditorApplication.isPlaying=False`（已退 Play Mode，下次进 Play 时 `UnitDataManager.LoadAll` 会重读到正确数据）
3. 清 Console，待用户重新进 Play Mode 跑 `CombatTestSpawner` 确认 0 错误 + 阵型查表成功

## 后续影响（需复核）

3.0.1 注意力系统之前用"工人+士兵"验证时，**士兵实际用的是 Civilian 数值**（hp80/atk5/def0，无防）。若当时的战斗节奏/击杀速度调参基于此错数据，修复后士兵变强（hp100/atk10/def20），可能需要复核 3.0.1 的注意力调参结论。但注意：当时工人也是被 Civilian.asset 顶替的——所以 3.0.1 测试里"士兵"和"工人"可能都用 Civilian 数值对打，相对关系未必失真。建议下次 3.0.1 复跑时留意。

## 教训

- **SO 资产的文件名只是标签，代码查的是字段值**。生成资产后必须验证 `key = faction_occupation` 是否等于文件名，而非只看文件名存在。
- 用 MCP 批量生成 SO 时，occupation 这类枚举若用循环变量索引，容易串位。生成后应立即 dump 全字段对照文件名验证。
- 已补进 `unity-mcp-first` skill 的"正模式"：创建 SO 后用 `execute_code` 读回关键字段验证。
