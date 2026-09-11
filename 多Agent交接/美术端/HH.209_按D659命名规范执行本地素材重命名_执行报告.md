# HH.209 按 D659 命名规范执行本地素材重命名 · 执行报告

> 类型：进度同步 / 交付报告（⚠ 含 1 项阻塞列报 + 2 项待裁决）
> 状态：⏳待裁决
> 日期：2026-09-11 · 发起端：美术端 · 关联：HH.206（D659）· HH.103《美术资源接入映射表》§十 · 美术资源规范 §4.1/§8.4/§8.2

## 〇、任务回执（T1~T4）

| 任务 | 状态 | 结论 |
|------|------|------|
| T1 全量重命名映射表 | ✅ | 296 条（八类），按 D659 定稿口径 |
| T2 生成并执行重命名脚本 | 🟡 | dry-run ✅ **296/296 零遗漏零重名**；**执行被沙箱拦**（详见 §三） |
| T3 核实两处实盘问题 | ✅ | ①弓箭手待机 **需重出** ②营地命名建议 **`building_vagrant_camp`** |
| T4 执行报告 | ✅ | 本信 |

---

## 一、T1 全量重命名映射表（按 D659 定稿）

**权威依据**：HH.206（D659 裁决）＋《美术资源接入映射表》§十（策划端已补登）＋美术规范 §4.1/§8.4。
**D659 定稿要点已落**：①分级后缀统一 **`_lv{n}`**（主城 `castle_{race}_lv{0..6}`）②鹿骑 **`unit_elf_deerrider`**（双 r）③枯木 **`feat_deadwood`**。

**八类规则**（race=human/orc/dwarf/elf）：

| # | 类别 | 文件名格式 | 目标路径 |
|---|------|-----------|---------|
| ① | 人种立绘 | `unit_{race}_{occ}.png` | `Portraits/{race}/` |
| ② | 单位动画 | `unit_{race}_{occ}_{state}.png` | `Units/{race}/{occ}/` |
| ③ | 主城 | `castle_{race}_lv{0..6}.png` | `Buildings/{race}/castle/` |
| ④ | 族特殊建筑 | `building_{race}_{excl}.png` | `Buildings/{race}/exclusive/` |
| ⑤ | 战争机器 | `machine_{race}_{machine}.png` | `Machines/` |
| ⑥ | 普通建筑 | `building_{id}_lv{n}.png` | `Buildings/neutral/` |
| ⑦ | 自然/资源 | `feat_{id}.png`（树带气候+序号） | `Buildings/neutral/features/` |
| ⑧ | 脚手架 | `building_scaffold_{w}x{h}.png` | `Buildings/neutral/` |

**state 归一**：待机→`idle`／走路·行走→`walk`／攻击·射击·战斗·发射·治疗·建造·交互·冲锋→`attack`／奔跑→`run`（仅坐骑系）。

**全量映射明细**：见固盘文件 `_art_rename\mapping_dryrun.txt`（301 行：表头 3 行 + 296 条映射）。摘录：

```
矮人\矮人人种\矮人法师.png        -> Portraits\dwarf\unit_dwarf_mage.png
人类\人类人种\人类盾卫.png        -> Portraits\human\unit_human_shieldguard.png
精灵\精灵动画\鹿骑\鹿骑奔跑_已抠图.png -> Units\elf\deerrider\unit_elf_deerrider_run.png
普通建筑\矿脉石堆枯木\枯木.png     -> Buildings\neutral\features\feat_deadwood.png
普通建筑\脚手架\1乘1.png          -> Buildings\neutral\building_scaffold_1x1.png
```

**量分布**：人种立绘 46｜单位动画 137｜主城 28｜族特殊建筑 4｜战争机器 8｜普通建筑 ~42｜自然/资源 16｜脚手架 3｜城墙段 11 = **296**。

---

## 二、T2 脚本执行

**脚本**：`_art_rename\rename_assets.ps1`（pwsh 7，`-Execute` 开关控 dry-run/执行）
**实现**：数据驱动映射（职业表 / 等级表 / 气候表）→ 统一 Move-Item → 执行后清理源空目录。

**dry-run 结果（实盘输出）**：

```
==== 映射条数: 296 / 实盘文件: 296 ====
（无「未覆盖文件」、无「目标重名」）
```

✅ **零遗漏**（296 实盘文件全部有映射）、✅ **零重名**（目标路径无冲突）。

---

## 三、⚠ 阻塞列报：执行被沙箱拦截（需用户/环境放行）

**现象**：`-Execute` 运行即被拦截，实盘报错：

```
TRAE Sandbox Error: hit restricted
  Not allow operate files: C:\Users\trs\Desktop\美术资源文件夹\矮人\矮人人种\矮人法师.png
  Hint: ... run this command outside the sandbox ...
```

**根因**：素材夹路径 `C:\Users\trs\Desktop\美术资源文件夹` **在工作目录 `c:\Users\trs\Desktop\Valley Rampart` 之外**，沙箱禁止对工作目录外文件做写操作（Move-Item/New-Item）。**这是环境级硬约束，脚本本身无缺陷。**

**影响评估（已实盘复核，无损）**：

- 两次执行均**卡在第一条 Move 即被拦**，未产生部分改名
- 实盘复核：**296 个原始文件全部完好、零丢失**
- 残留：`Portraits\`（空目录）+ `_rename\`（空夹，含 0 字节同名残留），**待清理**

**所需放行（三选一，请策划端转达用户）**：

1. **用户开启沙箱外运行权限**（Settings → Permission & Approval → Custom Configuration，放开 `C:\Users\trs\Desktop\美术资源文件夹`）后，美术端重跑 `-Execute` 即可一键完成（脚本已就绪）
2. **用户手工执行脚本**：在 pwsh7 中运行 `& '<工作目录>\_art_rename\rename_assets.ps1' -Execute`
3. **素材夹迁入工作目录内**（如 `Valley Rampart\_art_assets\`）后再执行

---

## 四、T3 实盘核实结论

### T3-① 弓箭手待机图版式——**非族内主流，需重出**

| 文件 | 尺寸 | 单元格式 | 判定 |
|------|------|---------|------|
| **人类弓箭手_待机**`_原32x34排`.png | **96×102** | 3×3，32×34/格 | ⚠ **异类** |
| 人类弓箭手_攻击 | 152×152 | 3×3，50.7/格 | 主流 |
| 人类弓箭手_走路 | 152×152 | 3×3，50.7/格 | 主流 |
| 族内其他待机（法师/将军/盾卫/弩手…） | **114×114** | 3×3，38/格 | 主流 |
| 族内其他攻击/走路 | 152×152 | 3×3，50.7/格 | 主流 |

**结论**：该图**文件名自注「原32x34排」**，实为**旧版式**（96×102，单元格 32×34），**显著小于**族内主流待机规格（114×114，单元格 38×38）。**判定＝不达标，需重出**，归批5 补图（占用批5 流程，非接入批）。

> 备注：D659 决策3 已澄清「非漏交付」（文件在场）——本结论是**尺寸达标性**判定，与「是否漏交付」不冲突。

### T3-② 流浪汉营地命名——建议 **`building_vagrant_camp`**

**实盘勘正**：`feat_vagrant_camp` 的定位**不是自然特征（`feat_`）**，而是**建筑**——代码内 `BuildingType.VagrantCamp`（`GridTypes.cs:163`，「3.5.1 实体化 E-S7 末尾追加」），且 `WorldManager` 以 **Building 实例**生成、有 def 资产 `VagrantCamp.asset`（id=`VagrantCamp`）。

| 项 | 值 |
|----|----|
| 类别枚举 | `BuildingType.VagrantCamp`（**建筑**，非 `FeatureType`） |
| def id | `VagrantCamp` |
| 现有 artId 键 | **无**（代码内无对应 artId，D659 已标注 ⬜ 待建） |

**命名建议**：**`building_vagrant_camp`**（对齐 ⑥ 普通建筑 `building_` 前缀 + 建筑类别语义），**而非** HH.206 §三⑦ 原写的 `feat_vagrant_camp`。**请策划端裁决**（映射表 §十.2 现登记为 `feat_vagrant_camp`，若采纳本建议需改归 §十.1 普通建筑类）。

---

## 五、待决策事项

**决策 1：T2 执行放行方式**（阻塞级）
- A（推荐）：用户开启沙箱外权限 → 美术端重跑 `-Execute`（脚本就绪，一键完成）
- B：用户手工执行脚本
- C：素材夹迁入工作目录内再执行
- 影响：不放行则重命名无法完成，接入批（HH.103）无英文名素材可接

**决策 2：弓箭手待机重出归属**
- A（推荐）：归**批5 补图**（与族内主流规格对齐，重出 114×114 或按批5 统一规格）
- B：接入批接受现状（96×102 异类，接入时按 pivot 缩放——但会与族内像素密度不一致）
- 影响：影响人类弓箭手 idle 观感一致性

**决策 3：营地命名 `building_vagrant_camp`**
- A（推荐）：采纳（建筑类别，building_ 前缀；映射表 §十 相应从 §十.2 移至 §十.1）
- B：维持 HH.206 原写 `feat_vagrant_camp`（按自然特征处理）
- 影响：命名空间归属（`building_` vs `feat_`）

**决策 4：残留空目录清理**
- `Portraits\`（空）+ `_rename\`（含 0 字节残留文件）
- 推荐：随执行放行后用脚本自带清理逻辑一并处理（或美术端手工删）

---

## 六、下一步

1. 策划端裁决 4 项 → 回写本信
2. 用户/环境放行沙箱 → 美术端重跑执行（脚本已就绪）
3. 执行后美术端 `Get-ChildItem` 复核零遗漏/零重名/零中文残留 + 更新报告
4. 接入批（HH.103）按英文名素材接入

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 决策 1：T2 执行放行方式 | | |
| 决策 2：弓箭手待机重出归属 | | |
| 决策 3：营地命名 | | |
| 决策 4：残留空目录清理 | | |
