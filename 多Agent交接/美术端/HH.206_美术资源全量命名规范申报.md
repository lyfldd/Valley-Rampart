# HH.206 美术资源全量命名规范申报

> 类型：策划报告请求 / 待决策
> 状态：✅已裁决（D659，2026-09-11 主策划端）
> 日期：2026-09-11 · 发起端：美术端 · 关联文档：HH.103《美术资源接入映射表》、美术资源规范_等轴立方体瓦片、3.1.2 §0.6

## 一、背景（用户指令）

用户 2026-09-11 指令：**美术资源已产出/在产一大批（`C:\Users\trs\Desktop\美术资源文件夹`），要求统一规范命名**——文件夹现全部为中文名（人种/动画/建筑/资源点），需按项目规范转成工程内英文名，且**命名口径需策划端确认后登记进 HH.103 映射表**（策划端=该表单写者）。

## 二、命名依据（实盘锚定，非自创）

命名**不是我拍脑袋**，项目里已有两处硬依据：

| 依据 | 出处 | 内容 |
|------|------|------|
| A 文件命名铁律 | HH.103《美术资源接入映射表》§八 | ①今后凡入 Assets **一律英文名，禁中文文件名/路径** ②路径 `Assets/Art/{Portraits\|Units\|Buildings\|Machines}/{race\|category}/` ③PPU=100、pivot=底面中心 ④映射表外新文件须增量补表 |
| B artId / 命名分类 | 美术资源规范_等轴立方体瓦片 §4.1 / §8.4 | artId 前缀 `feat_`(自然)/`bld_`(人造)/`ground_`(地皮)/`mark_`(调试)/`unit_`(单位动画)；单位动画 `unit_{职业}_{朝向}_{状态}.png` |
| C NPC 动画命名 | 3.1.2 §0.6 | `{Occupation}_{Action}.png`（本文按 HH.103 §八 统一为全小写 snake + 族前缀） |

## 三、统一命名方案（八类）

> 总则：全英文语义名｜路径三段式 `Art/{类别}/{子类}/`｜单图 `snake_case.png`｜分级 `_lv{n}`｜坐骑人兽一体烘焙不拆。
> `race`缩写=**human/orc/dwarf/elf**；`occ`=Occupation 枚举小驼峰（见 HH.103 §二）。

| # | 类别 | 命名格式 | 目标路径 | 依据 |
|---|------|---------|---------|------|
| ① | 四族人种立绘（46） | `unit_{race}_{occ}.png` | `Art/Portraits/{race}/` | 映射表 §二 |
| ② | 单位动画（137 Sheet） | `unit_{race}_{occ}_{state}.png` | `Art/Units/{race}/{occ}/` | 映射表 §六 |
| ③ | 主城等级（28） | `castle_{race}_l{0..6}.png` | `Art/Buildings/{race}/castle/` | 映射表 §三 |
| ④ | 族特殊建筑（4） | `building_{race}_{excl}.png` | `Art/Buildings/{race}/exclusive/` | 映射表 §四 |
| ⑤ | 战争机器（8） | `machine_{race}_{machine}.png` | `Art/Machines/` | 映射表 §五 |
| ⑥ | 普通建筑（中性，约 46） | `building_{id}_lv{n}.png` | `Art/Buildings/neutral/` | 本信提案（§十 待登记） |
| ⑦ | 自然/资源（16） | `feat_{id}.png`（树带气候+序号） | `Art/Buildings/neutral/features/` | 美术规范 §4.1 前缀 |
| ⑧ | 脚手架（3） | `building_scaffold_{w}x{h}.png` | `Art/Buildings/neutral/` | 随⑥ |

### ② state 归一硬规则（映射表 §六）

`待机→idle`｜`走路/行走→walk`｜`攻击/射击/战斗/发射/治疗/建造/交互/冲锋→attack`｜`奔跑→run`（**仅坐骑系**：骑兵/狼骑兵/鹿骑，D602 例外 4 动作）

### 逐类映射摘录

**① 人种立绘**（族各 11~12）：
`人类普通士兵`→`unit_human_warrior`｜`人类弩手`→`_crossbowman`｜`人类盾卫`→`_shieldguard`｜`人类骑兵`→`_knight`
`兽人狂战士`→`unit_orc_berserker`｜`兽人狼骑兵`→`_wolfrider`
`矮人火枪手`→`unit_dwarf_musqueteer`｜`矮人磐石卫士`→`_bedrock`
`精灵游侠`→`unit_elf_ranger`｜`精灵风行者`→`_windwalker`｜`精灵鹿骑`→`_deerider`
（其余：`法师`=mage／`治疗师`=healer／`将军`=general／`居民`=resident／`工人`=worker／`流浪汉`=vagrant／`小孩/儿童`=child）

**② 单位动画**（示例）：`精灵\精灵动画\鹿骑\鹿骑奔跑_已抠图.png` → `unit_elf_deerider_run.png`
特化：工人「建筑，交互」/「建造」→`_attack`；弓箭手「射击」→`_attack`；治疗师「治疗」→`_attack`；火枪手「发射」→`_attack`；精灵战士「战斗」→`_attack`

**③ 主城**：`人类\建筑\零级~六级_裁剪.png` → `castle_human_l0.png`~`l6.png`（兽/矮/精同理）

**④ 族特殊建筑**：`人类特殊建筑.png`→`building_human_waracademy.png`｜`兽人特殊建筑.png`→`building_orc_waracademy.png`｜`矮人特殊建筑.png`→`building_dwarf_leyforge.png`｜`精灵特殊建筑.png`→`building_elf_archery.png`

**⑤ 战争机器**：`machine_human_ballista`／`machine_orc_ram`／`machine_dwarf_mortar`／`machine_elf_vinecatapult`（动画加 `_strip{n}`）

**⑥ 普通建筑**：`练兵场`→`building_barracks_lv{1,2,3}.png`；`农场`→`_farm_lv`；`铁匠铺`→`_blacksmith_lv`；`房屋`→`_house_lv`；`医院`→`_hospital_lv`；`教堂`→`_church_lv`；`市场`→`_market_lv`；`仓库`→`_warehouse_lv`；`矿洞`(矿场建筑)→`_mine_lv`；`战争机器工坊`→`_siegeworkshop_lv`；`牧场`→`_ranch_lv`；`水井`→`_well_lv`；`城门`→`building_gate_closed/open.png`；`桥`→`building_bridge.png`；`箭塔`→`_arrowtower`；`弩塔`→`_crossbowtower`；`魔法塔`→`_magictower`；`城墙段`→`building_wall_seg{01..11}.png`；`传送门`→`building_portal.png`

**⑦ 自然/资源**：`树木\热带1,2,3`→`feat_tree_tropical_{1,2,3}.png`（亚热带=subtropical/温带=temperate/寒带=cold）｜`矿脉`→`feat_orevein.png`｜`石堆`→`feat_stone_pile.png`｜`枯木`→`feat_deadwood.png`（⚠见待决策②）｜`未开采矿洞_矿山`→`feat_mine.png`｜`流浪汉营地`→`feat_vagrant_camp.png`

**⑧ 脚手架**：`1乘1`→`building_scaffold_1x1.png`｜`2乘2`→`_2x2.png`｜`3乘3`→`_3x3.png`

## 四、待决策事项（每项：选项 + 推荐 + 影响）

**决策 1：分级后缀统一——主城 `_l{0..6}` vs 普通建筑 `_lv{1..3}`**
- A（推荐）：**统一用 `_lv{n}`**，主城改 `castle_{race}_lv{0..6}`——单一后缀，减少美术/程序记忆负担
- B：维持现状两套（主城 `_l`、建筑 `_lv`）——理由：主城 0 级起步、普通建筑 1 级起步，语义可区分
- C：统一用 `_l{n}`，普通建筑改 `building_{id}_l{1..3}`——更短
- 影响：仅命名，零代码逻辑影响；若不统一，接入批映射表易写错

**决策 2：枯木英文名 `feat_deadwood` vs 沿用 `feat_wood_pile`**
- A（推荐）：文件名+代码 artId 一起改 `feat_deadwood`——"枯木"语义准确（用户改名本意=更像自然生成资源），与 HH.188 §八 一并落
- B：文件用 `feat_deadwood`、代码键沿用 `feat_wood_pile`——零代码改动
- 影响：A 需同步改 `PlaceholderSprites.cs` 一处键 + 任何引用；B 名实不符埋坑

**决策 3：人类弓箭手动画缺 `待机`**
- 实盘：`美术资源文件夹\人类\人类动画\弓箭手\` 只有 `_攻击_`+`_走路_` 两张，**缺 `_待机_`**（其余人类角色三态齐）
- 请确认：是**漏交付**（需补画 1 张）还是**另存他处**（请美术端自查）——若确缺，归批5 补图清单

**决策 4：本方案是否登记进 HH.103 映射表 §十 增补待办**
- 推荐：**是**——由策划端（映射表单写者）把①~⑧ 的「源中文名→目标英文名→代码键」全量行补入映射表 §十，作为接入批施工查表
- 影响：不登记则接入批施工时无权威对照，易各写各的

## 五、下一步建议

1. 策划端裁决四决策 → 回写本信「策划裁决」节
2. 策划端把命名对照全量行补入 HH.103《美术资源接入映射表》§十
3. 美术端按最终命名**批量重命名**本地文件夹（如需，美术端可生成重命名脚本）
4. 接入批（HH.103）施工时按映射表挂接

---

## 策划裁决（策划端回写）

> **D659**（2026-09-11，主策划端）。**判据三直读已过**：①设计稿＝HH.103《映射表》§一~§十 逐段＋美术规范 §4.1/§8.4＋3.1.2 §0.6＋HH.188 全文；②代码 file:line＝`Rendering/PlaceholderSprites.cs:37-67`（artId 表）／`MapGenRules.cs:22/43/552-555`＋`ResourceRespawnSystem.cs:166`＋`MapRenderService.cs:316/376`（`FeatureType.WoodPile` 消费链）／`UnitData.cs:49`（`Occupation.DeerRider`）／`BuildingFactory.FindDefById("VagrantCamp")`；③字段直读＝artId 字面量、`BuildingDef.levels`（主城 7 档）、实盘 LS 用户文件夹。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 决策 1：分级后缀统一 | **采纳 A**：统一 `_lv{n}` —— 主城改 `castle_{race}_lv{0..6}`、普通建筑 `building_{id}_lv{1..3}` | 单一后缀减记忆负担；`_lv` 优于 `_l`（`_l0`/`_l1` 易与数字混淆）。**纯文件命名、零代码影响**（artId=`bld_castle` 单键＋等级态，代码不依赖后缀）。策划端已同步修订映射表 §三 |
| 决策 2：枯木英文名 | **采纳 A**：文件名＋artId 键统一 **`feat_deadwood`** | 实盘：artId `feat_wood_pile` **全仓零消费**（仅 `PlaceholderSprites.cs:46` 定义；地图特征走 `FeatureType.WoodPile` 枚举）⇒ 改名成本≈0、零风险；B 制造永久名实不符（L-15 家族）。**代码侧键改名＝执行端在 HH.103 接入批搭车**（非美术端职责）；`FeatureType.WoodPile`／def id `wood_pile` 内部枚举/id **不改**（显示「枯木」走 def displayName）。与 HH.188 §八 一并落 |
| 决策 3：弓箭手缺待机 | **非漏交付**（实盘 LS：`人类\人类动画\弓箭手\` **3 张全在场**，待机名＝`弓箭手_待机_已抠图_原32x34排.png`）⇒ **不需补画**；接入批按归一名 `unit_human_archer_idle.png` 取该文件。**附**：后缀 `_原32x34排` 提示尺寸/版式备注 ⇒ 请美术端**复核该图是否达标**（标准基底 32×32，D28~D30）；若为旧版式需重出则归批5 | 
| 决策 4：登记映射表 §十 | **采纳**（是）：策划端（单写者）补登 §十「命名规范总表」＋ ⑥⑦⑧ 待接入行（源→目标→代码键）；①~⑤ 逐项映射已在 §二~§六，**不重复**（防双源 L-15）。**另立**：§十 明确「**文件名前缀**（`unit_`/`castle_`/`building_`/`machine_`/`feat_`/`ground_`）」与「**artId 键**（`feat_`/`bld_`/`ground_`/`mark_`）」**两个命名空间**，防混 | 

**勘正 1 笔**：HH.206 §三① 中精灵鹿骑 `_deerider` → **`_deerrider`**（对齐映射表 §二 约定＝Occupation 去驼峰连写，同 `wolfrider`/`windwalker`；代码 `Occupation.DeerRider`＝Deer+Rider）——本信两处（§三①、§三②示例 `unit_elf_deerider_run.png`）同勘正。

**实盘澄清（D659 勘正①）**：`feat_vagrant_camp`（§三⑦ 流浪汉营地）——**设计与实体均在场**（`VagrantCamp.asset` def：displayName 流浪汉营地／描述含招募机制／role=4／sourceType=12；`VagrantCampSystem`：生成＋补员＋招募＋HomePoint；`MapGenRules` 生成；文档 `3.5.1 §4.1` 决策11/13＋§9.1）。**真缺口＝美术「占位/键」层**：渲染走 `BuildingVisual.GetPlaceholderKey`→`Core/PlaceholderSprites.cs` 1D key 表，**该表与等轴 artId 表均无 camp 条目**⇒当前走兜底占位。**用户裁定（2026-09-11）**：营地＝**与矿洞锚点 `feat_mine` 同性质的地图预设生成物 ⇒ 前缀归 `feat_`**（`feat_vagrant_camp` 成立）；**美术素材已就绪**，接入批挂接即可，**占位/兜底不立债**。

### 衍生产物
- **更新文档**：HH.103《美术资源接入映射表》§三（后缀 `_l`→`_lv`）＋ §十（命名规范总表 + ⑥普通建筑/⑦自然资源/⑧脚手架 待接入行）
- **派生执行任务（执行端，非美术端）**：`PlaceholderSprites.cs:46` 键 `feat_wood_pile`→`feat_deadwood`（HH.103 接入批搭车，1 行、零消费键、零风险）
- **勘正**：HH.206 `deerider`→`deerrider`（本信回写已勘正）
- **待美术端核**：决策3 待机图 `_原32x34排` 尺寸口径
