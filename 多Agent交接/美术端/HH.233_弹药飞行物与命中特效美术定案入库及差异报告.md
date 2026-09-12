# HH.233 弹药飞行物与命中特效美术定案入库 · 交付报告（含 2 处代码-文档差异列报）

> 类型：进度同步＋待决策（差异② Musket 弹种需策划裁决；差异①与美术定案待知悉存档）
> 状态：✅已对齐（域内回写·美术接入域 2026-09-12·见文末 §五）
> 日期：2026-09-12 · 发起端：美术端 · 关联：HH.206（D659）· 3.1.1 §11.1/§11.3 · 3.1.3 批8 · AmmoDef/ProjectileManager/SiegeWorkshopBuilding

## 一、做了什么（带证据）

1. **传送门怪物 3 素材入库**（15 张）→ `美术资源文件夹\Units\monster\{type}\unit_monster_{type}_{state}.png`（state=idle/walk/attack/loot/icon；Raider/Slinger/Brute）
2. **地块 5 张入库** → `美术资源文件夹\Ground\ground_{cold|river|tropical|temperate|subtropical}.png`
   - 含**整块水河流地块 `ground_river`**（用户拍板替代废弃河道；对应 2_10 步骤6 地皮真图口径）
3. **弹药飞行本体 6 张入库** → `美术资源文件夹\Ammo\`：
   `ammo_arrow`（弓弩通用 0.5KB）`ammo_heavybolt`（战争机器专用 1.2KB）`ammo_stone`（1.2KB）`ammo_fireball`（1.8KB）`ammo_magic`（2KB）`ammo_mage_barrage`（法师弹幕专用 1.1KB）
4. **命中特效 3 张入库** → `美术资源文件夹\Effects\hit\fx_hit_{stone|fireball|magic}.png`
5. **用户拍板生效**：通用命中特效 5 类全砍；箭矢系命中动画不画；命中动画仅保留弹类 3 种（石/火/魔）——与代码对齐（弹类砸地需爆开，穿透类命中走代码）

## 二、差异列报（用户指令：与代码/文档不一致须报告）

### 差异①：法师弹幕（用户已拍板，只需知悉存档）
- **用户拍板**：法师发射弹幕用独立贴图 `ammo_mage_barrage`（**法师专用**）；弩炮消耗用 `ammo_magic`（魔法蛋）——同 `ProjectileType.Magic` 弹型，**视觉差异化仅美术层，代码零改动**
- **代码实况**：无 Barrage 弹种；法师 `SelectAmmo`＝默认 Magic、高价值目标切 Fireball（`UnitController.cs:854-872`）⇒ 法师弹幕＝Magic 弹的独立视觉形态。美术层做法师专用图，不影响运作

### 差异②：Ammo_Musket 无独立弹种（**需策划裁决**）
- **实况**：`Ammo_Musket.asset` ammoType=1 ⇒ 矮人火枪手（Musqueteer）发射**复用 Bolt 弩箭弹型**；3.1.1 §11.1 弹药表（6 种）未列 Musket ⇒ 火枪开火视觉＝弩箭，与 2_20.1 火枪手职业语义不符（弹丸 vs 弩矢）
- **选项**：
  - **A（零改动）**：维持复用 Bolt 视觉，火枪出弹＝弩箭贴图
  - **B（对齐语义）**：新增 `ProjectileType.Musket`＋独立 Ammo 资产＋美术补 `ammo_musket.png`（铅弹/弹丸）——涉及代码弹种扩展，需策划裁决后另立实施
- **影响面**：A 零成本但语义错位；B 一弹种扩展，符合 3.6 AmmoDef 解耦原则 3「改动局部化」（改 Ammo 资产即可，生产/装备引用 id 不动）

## 三、验收口径

- Ammo 6 张 + Effects 3 张 + Ground 5 张 + monster 15 张均按 D659 风格命名（类别前缀＋小写＋下划线）入库
- `美术资源文件夹\_待整理` 已清空（暂存区保留待后续）
- 新建目录 `Ammo/` `Ground/` `Effects/` 为映射表外新增类别，待策划确认归目录口径（或并入既有类别）

## 四、下一步建议

- 差异②裁决后决定是否补 Musket 弹（A/B 及其实施）
- 新目录类别口径（Ammo/Ground/Effects）待策划确认后追记《美术资源接入映射表》

---

## 五、域内回写（美术接入域·2026-09-12，依 D686/D689 对齐·无新裁决点）

| 事项 | 裁示 |
|---|---|
| 差异② Musket | 按 **D686 定稿**走 B 路线（Musket 新增独立弹种）；`ammo_musket.png` **已在盘**（实盘核 2026-09-12）。**接线挂 3.6.1 弹种转正批**（`ProjectileType` 6→9 在 `AI.Core/Config/ProjectileTypes.cs`＝sim-sync 红线域，美术接入批禁触）——本批（HH.239）只入库 |
| 差异① 法师弹幕 | 知悉存档成立：`ammo_mage_barrage.png` 已在盘，作 Magic 弹法师专用视觉形态；接线同挂 3.6.1 批 |
| 新目录口径 | `Ammo/`、`Effects/`、`Ground/` **已追记映射表 §十一**（本日落）；Ground 6 张实盘吻合（ocean 在盘） |
| monster 15 | 用户裁示**并入接入批 HH.239**（映射表 §十一 ⑫ 行已落） |
| 下一步 | 弹种接线时点随 3.6.1 实施批；城门 open 图（用户补抽中）与弓箭手待机重出归批5/用户美术流程 |