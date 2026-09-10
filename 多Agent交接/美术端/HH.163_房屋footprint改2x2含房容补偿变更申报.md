# HH.163 房屋 footprint 1×1→2×2 变更申报（含房容补偿，用户拍板）

> 类型：变更申报+影响面分析（用户 2026-09-10 拍板：房屋 1×1 美术效果不好→2×2，并**上调房容补偿人口密度**）
> 状态：✅已裁决（D614，2026-09-10；四决策全清=①采纳 2×2 ②房容 ×4=12/20/32 ③房容 SO 化入 KingdomConfig ④并入 HH.159 件2）
> 日期：2026-09-10 · 发起端：美术端 · 关联：HH.155 §1.3 / **D613（矿洞 2×2 先例，三决策全采纳）** / 总表 v2 §1.1 / 3.1.3 §六 / HH.159 件2 / HappinessSystem.cs:293 / PopulationSystem / UtilityScorer / House.asset / TestFixtureTiersConfig

## 一、事由

用户确认房屋 1×1（128×64）**美术效果不好**（房屋要素多：屋顶/门/窗/烟囱/墙体，1×1 挤）；拍板 **1×1→2×2**，并要求**上调房容补偿人口密度**。与 **D613（矿洞 2×2）**同型，且房屋原也列于 HH.155 §1.3「维持不改（1×1 系）」。

## 二、⚠️ 与矿洞的关键差异（提请策划重点注意）

| 维度 | mine 矿洞（D613 已批） | House 房屋（本笔） |
|------|----------------------|-------------------|
| 建造频次 | 少而大（1~数座/局） | **多而密（AI 连轴建造几十座）** |
| 2×2 密度压力 | 无 | **有**——每栋占 4 倍地 |
| 系统角色 | 多产物产能 | **人口生育门槛**（PopulationSystem：`houseCapacity > population` 才生育）+ 幸福因子 |
| 补偿需求 | 无 | **房容上调**（用户拍板） |

- AI 行动池 `BuildHouse`（[UtilityActionConfig.cs:24](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L24)，`NeedKind.HouseGap`）按房容缺口**连轴建房**；房容是人口增长的硬前置。
- 1×1→2×2 = 每栋 4 倍地 → 同人口村庄占地约 4 倍（例：养 ~100 人口约需 30+ 栋，1×1=30 格 vs 2×2=120+ 格）→ 故需房容补偿。

## 三、影响面（代码实盘核查 2026-09-10）

### 3.1 资产（执行端，1 笔）
- `Assets/Resources/Buildings/House.asset`：`footprint: {x:1,y:1}` → `{x:2,y:2}`
- 并入 HH.159 件2（footprint 变更 11→**12 笔**）

### 3.2 ⚠️ 代码（★本笔非零代码改动，与矿洞不同）
- **[HappinessSystem.cs:293](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/HappinessSystem.cs#L293)**：`GetHouseCapacity(int level) => level >= 3 ? 8 : level >= 2 ? 5 : 3;` ——**房容硬编码**，上调需改此处
- 顺带：此处为 **SO 数据驱动违规点**（`so-data-driven` 原则：可调数值应 SO 化）；建议本次借机 SO 化（数值入配置）
- `House.asset` description「人口容量3」+ BuildingPanel 显示（走读口，自动）同步

### 3.3 无其他代码改动（读口统一，自动生效）
- `PopulationSystem.cs:273/378`、`UtilityScorer.cs:157`、`BuildingPanel.cs:414` 均调用 `GetHouseCapacity` / `GetHouseCapacityByKingdom` → 改硬编码即全链生效

### 3.4 文档（策划端）
- 总表 v2 §1.1：House 从「维持不改（1×1 系）」移入变更表
- 3.1.3 §六：House 归 2×2 画布 256×128
- HH.155 §1.3：加勘误注

### 3.5 ⚠️ 测试夹具复核（列报）
- `TestFixtureTiersConfig.cs:7` 注释「每 House 容量=3，houses 保证 houseCapacity>population（T9/M8 生育门槛）」——房容上调后**该夹具假设需复核**（否则测试局生育门槛失真）

### 3.6 AI 预置链 / 选址回归
- House 在六模板 `baseBuildingDefIds` 内（如 Kingdom_Bedrock L34）；2×2 多占格 → 预置链核对 + 选址回归（HH.159 件2 T2.13/T2.14 面）

### 3.7 美术侧口径
- House 三档（Lv1~Lv3）画布 128×64 → **256×128**（2×2 基面）；三档递进锚不变（体量/层数/装饰递增）

## 四、待决策（请策划端裁）

| # | 决策项 | 选项 | 说明 |
|---|--------|------|------|
| 1 | 是否采纳 House 1×1→2×2 | 采纳（用户已拍板）/ 维持 1×1 | 用户拍板采纳 |
| 2 | **房容目标数值**（密度补偿） | ①×4 保密度（3/5/8→**12/20/32**）②×2（→**6/10/16**，接受密度略降换美术）③策划定 | 数值归策划（平衡面） |
| 3 | 是否顺带 SO 化房容 | SO 化 / 维持硬编码 | 建议 SO 化（治违规点） |
| 4 | 承接方式 | 并入 HH.159 件2（同 mine，D613 先例）/ 独立微批 | 建议并入 |

## 五、证据（实盘）

- House.asset `footprint: {x:1,y:1}` / levels statScale 1.67/1.6 / description「人口容量3」
- HappinessSystem.cs:293（房容硬编码 3/5/8）+ L275-313（GetTotalHouseCapacity/GetHouseCapacityByKingdom）
- PopulationSystem.cs:273/378（生育门槛 houseCapacity>population）、UtilityScorer.cs:154-157（HouseGap）、BuildingPanel.cs:411-414（显示）
- UtilityActionConfig.cs:24（BuildHouse 连轴）
- TestFixtureTiersConfig.cs:7（夹具假设）
- 总表 v2 §1.1（House 列于 1×1 维持不改）/ §1 民生行（房容 3→5→8 已实装）
- D613（矿洞 2×2 先例，三决策全采纳，承接=HH.159 件2）

---

## 策划裁决（策划端回写）

> 裁决日期 2026-09-10 · 裁决号 **D614**（0.6 §一百四十三）· 实盘复核：信中证据零失实（仅 HappinessSystem 行号 :293→**:297** 微偏）

| 项 | 裁决 |
|----|------|
| 决策 1：是否采纳 House 2×2 | **采纳**（用户已拍板；与 D613 矿洞同型；零返工窗口=HH.159 件2 未开跑） |
| 决策 2：房容目标数值 | **①×4 = 12/20/32（保密度）**——依据：a) 唯一保密度选项（12÷4格=3人/格=旧 3÷1格，村庄占地不变）；b) 等级递进比逐位吻合 House.asset `levels.statScale` 1.67/1.6（3→5=×1.667、5→8=×1.6，×4 后 12→20/20→32 仍吻合）；c) AI 建房数 ÷4=连轴建房压力大减。**代价披露**：生育硬前置（houseCapacity>population）明显放松，早期 1 栋房即可触及 12 人上限，人口节奏更多由粮/幸福决定 |
| 决策 3：是否 SO 化 | **SO 化（本批做）**——3/5/8 硬编码为 so-data-driven 违规点；数值入 KingdomConfig（与 happinessHouseWeight 同源，唯一真源 Resources/Config/KingdomConfig.asset）；读口统一（GetHouseCapacity/GetHouseCapacityByKingdom）→ 全链自动生效 |
| 决策 4：承接方式 | **并入 HH.159 件2**——footprint 变更 11→**12 笔**（+House 1×1→2×2）；件2 由「预计零代码」升为「**★含代码**」，原 T2.12 传送门/T2.13 预置链/T2.14 选址回归顺延为 T2.13/T2.14/T2.15，新增 T2.16 房容 ×4+SO 化 / T2.17 测试夹具重算+HH81 P1+TestFixtureApi 读数复核（TestFixtureTiersConfig「每 House=3」失效→须重算 houses 数，否则生育门槛测不出）；**任务计数 45→48** |

**补证（信中未提，策划端补）**：①消费面全在 Unity 侧（PopulationSystem 闸门 :273/:378 + HappinessSystem houseFactor :203 + UtilityScorer HouseGap :156 + BuildingPanel :414 + 测试读口）；②**决策核/态势层不受影响**——`DrivePopPressure` 用 `(工人+战士)/20` 不读房容（KingdomBrain.cs:392）→ 预计无 sim 镜像（建议执行端在训练仓兜底 grep `HouseCapacity`）。
