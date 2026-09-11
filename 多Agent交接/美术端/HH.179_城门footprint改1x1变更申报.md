# HH.179 美术端：城门 footprint 2×1→1×1 变更申报（用户反馈 2×1 美术表现不好）

> 类型：变更申报+连锁影响上报（用户 2026-09-11 反馈实测 2×1 城门美术表现不好，拟改 1×1；**连锁改 D641 脚手架决策**）
> 状态：✅已裁决（D642，2026-09-11 策划端；四决策全裁）
> 日期：2026-09-11 · 发起端：美术端 · 关联：总表 v2 §1.1（城门=唯一保留 2×1 特例）/ 3.1.3 §六 / **D641（HH.177 脚手架四张裁决）** / HH.155 §1.3 / PlacementValidator.cs / KingdomFoundry.cs:228 / Building.cs:67-68

## 一、事由

用户实测城门 2×1 **美术表现不好**（横条门体在等轴菱形上比例别扭），拟改 **1×1**。城门原列于总表 v2 §1.1「维持不改」唯一理由=「嵌墙线语义：门占两段墙的门口」（HH.155 §1.3）。

## 二、实盘核查（★结论：1×1 可行，破坏面极小）

### 2.1 城门朝向不依赖 footprint（关键）
- [PlacementValidator.InferGateOrientation](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/PlacementValidator.cs#L179-L188)：**检测落点相邻格墙走向**推朝向（`IsWallAt(左/右)` → 横向；`IsWallAt(上/下)` → 纵向），**与门自身尺寸无关**
- [Building.GateOrientation](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L67-L68)（`footprint.x >= footprint.y ? 横 : 竖`）——**全库无消费点**（grep `.GateOrientation` 仅命中定义处）＝**死属性**；1×1 时 x==y 恒判横向，但无消费者，无影响（可顺手清）
- [城门拐角校验](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/PlacementValidator.cs#L125-L130)（横竖两侧都有墙→禁放）用 `origin` **单格**推断，**不依赖尺寸**

### 2.2 AI 要塞城门运行时本来就是 1×1（强证据）
- [KingdomFoundry.cs:228](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs#L228)：围墙环预置 `var fp = new Vector2Int(1, 1)`，城门（L246 PlaceAt）**也传这个 fp**
- ⇒ **游戏内 AI 城门口实际已按 1 格落地**，1×1 是**已被运行验证的形态**（非新风险）

### 2.3 开关/占格逻辑不受阻
- [Building.SetGateBlocking](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L75-L80)：`MarkOccupiedFootprint(coord, max(1,fp.x), max(1,fp.y))`——1×1 正常（阻断单格）
- [GateController](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/GateController.cs)：昼夜开关+玩家覆盖，**不读 footprint**，1×1 无影响

### 2.4 无其他消费者
- 城门无 `outputResource`/产能/训练语义；`rotatable=1` 在 1×1 下旋转无意义（w=h=1，无害）
- 玩家建造 R 键旋转（BuildController）——1×1 下旋转视觉无差异，可留可去

## 三、★连锁影响：D641 脚手架决策需修订（4→3 张）

城门改 1×1 后，**全建筑 footprint 彻底正方形化**（不再有 2×1 档），则 **D641 刚裁的「脚手架四张」中 2×1 档作废**：

| 脚手架 artId | D641 口径 | 城门 1×1 后 |
|---|---|---|
| bld_scaffold_1x1 | ✅ | ✅ 保留 |
| **bld_scaffold_2x1** | ✅（D641 为城门加） | ❌ **作废** |
| bld_scaffold_2x2 | ✅ | ✅ 保留 |
| bld_scaffold_3x3 | ✅（主城） | ✅ 保留 |

→ 脚手架 **4 → 3 张**（恰回到用户最初提的「三张」）；又因城门归 1×1，**1×1 脚手架同时覆盖城门**。请策划端在裁决时一并修订 D641 口径。

## 四、影响面

### 4.1 资产（执行端，1 笔）
- `gate.asset`：`footprint: {x:2, y:1}` → `{x:1, y:1}`
- 并入 HH.159 件2（footprint 变更 12→**13 笔**）——**注**：HH.159 件2 已执行完毕（D628 验收销号），本笔属**追加变更**，承接方式请策划定（新微批 or 随 HH.103）

### 4.2 文档（策划端）
- 总表 v2 §1.1：gate 从「维持不改（唯一 2×1）」移出 → 全建筑正方形化达成；footprint 档位由「1×1/2×1/2×2 三档+3×3」收敛为「**1×1/2×2 两档+3×3**」
- 3.1.3 §六：删「2×1 城门（192×96）」画布档；城门归 1×1（128×64）
- 城门画布：192×96 → **128×64**；开/关 **2 张**不变

### 4.3 代码（预计零改动，1 处可清）
- 朝向/开关/拐角/预置全不依赖尺寸（见 §二）→ **预计零代码改动**
- 可顺手清：`Building.GateOrientation` 死属性（全库无消费）——建议随 HH.103 或后续清理批

### 4.4 AI 预置链/选址
- 围墙环城门本就用 fp=(1,1) → **零变化**；预置链核对/选址回归随 HH.159 件2 已覆盖面复核

## 五、待决策（请策划端裁）

| # | 决策项 | 选项 | 美术端建议 |
|---|--------|------|-----------|
| 1 | 是否采纳 gate 2×1→1×1 | 采纳（用户拍板）/ 维持 2×1 | 采纳（美术表现 + 运行时已验证 + 破坏面极小） |
| 2 | 连带修订 D641 脚手架 4→3 张 | 修订 / 维持四张 | 修订（2×1 档作废） |
| 3 | 承接方式 | 随 HH.103 / 新微批 | 建议随 HH.103（含脚手架选图代码面） |
| 4 | 是否清 `Building.GateOrientation` 死属性 | 清 / 留 | 清（顺手，防误导） |

## 六、证据（实盘）

- gate.asset `footprint: {x:2, y:1}` / isGate=1 / rotatable=1 / heightLayer=1
- PlacementValidator.cs:125-130（拐角单格推断）/ :179-188（InferGateOrientation 邻格墙检测）
- Building.cs:67-68（GateOrientation 死属性）/ :75-80（SetGateBlocking）
- KingdomFoundry.cs:228（fp=(1,1)）/ :243-246（城门 PlaceAt）
- GateController.cs（全文件不读 footprint）
- 总表 v2 §1.1（gate 唯一 2×1 特例）
- D641（脚手架四张=含 2×1 城门档）

---

## 策划裁决（策划端回写，**D642**，2026-09-11）

> **判据三直读＝已过**（①设计稿全文＝本信 §一~§六 逐字＋**被推翻依据** `HH.155 §1.3`「gate 2×1（嵌墙线语义…**用户问答拍板维持**）」＋`总表 v2 §1.1` L104 逐字；②代码实读＝`PlacementValidator.cs:125-130/179-188`（拐角/朝向**均用 `origin` 单格**）／`Building.cs:67-68`（`GateOrientation`）＋**ripgrep 全库 `\.GateOrientation` 零命中**＝死属性属实／`Building.cs:726-728`（`IsFortification` 只看 `isGate`）／`KingdomFoundry.cs:228/246`（AI 围墙环 `fp=(1,1)`＋城门同 fp）／`Valley Rampart/Assets/Resources/Buildings/gate.asset` **实读 `footprint: {x:2, y:1}`**；③档位直读＝`gate.asset` `isGate:1/rotatable:1/heightLayer:1` 字面量）。

| 项 | 裁决 |
|----|------|
| 决策 1：城门 2×1→1×1 | ✅**采纳**——用户 09-11 实测拍板 **> 09-10 `HH.155 §1.3` 的「用户问答拍板维持」（用户自我推翻，最新意志为准）**；**破坏面独立复核＝"极小"属实**（朝向/拐角用 `origin` 单格、开关不读 footprint、`IsFortification` 只看 `isGate`；**AI 要塞城门运行时本就 1×1**[`KingdomFoundry.cs:228` fp=(1,1)]＝**已被运行验证的形态**）；**旧档**＝`Building.cs:655-657` 存档 footprintW/H 优先于 def，未发布不迁移（D547 先例） |
| 决策 2：D641 脚手架 4→3 | ✅**修订**——gate 归 1×1 ⇒ 全建筑正方形化（1×1/2×2/3×3）⇒ **2×1 脚手架档作废** ⇒ **D641「脚手架四张」修订为三张**（1×1 兼覆城门；恰回到用户最初"三张"）；**已于 0.6 §一百七十 加修订注记** |
| 决策 3：承接方式 | ✅**采纳＝随 HH.103 美术接入批**（gate.asset footprint 1 笔＋脚手架选图代码面＋删 2×1 档 同域）；**HH.103 任务书须补三笔**：①`gate.asset` `{2,1}→{1,1}` ②删 `bld_scaffold_2x1` ③清 `Building.GateOrientation` 死属性 |
| 决策 4：清 GateOrientation 死属性 | ✅**清**——**独立复核**：ripgrep 全库 `\.GateOrientation` **零命中**（仅 `Building.cs:67-68` 定义处）＝死属性属实；随 HH.103 清（防误导；1×1 时 x==y 恒判横向，无消费者但语义已废） |

**验收三问（钩子2，前置＝判据三直读已过）**：①**发生**＝gate 2×1 系 `HH.155`（09-10）**用户「问答拍板维持」但仅凭文档口径（未出图/未进局实测）** → 次日（09-11）用户实测目视否决 ⇒ **连带 D641（同日更早）「脚手架四张」的 2×1 档立即作废**；②**定性＝策划端流程漏洞**（**美术尺寸/表现类口径未实测即定稿 ＋ 派生决策依赖该未稳定输入**）；③**教训核查**＝**新增条目 `L-27`**（美术表现类口径须实测后定；派生决策须核依赖稳定性）。

**嘉奖**＝美术端**不擅改文档 ＋ 主动上报"连锁改 D641"**（跨自身范围提示上游连带影响）＋ 全链 `file:line`（正面样本）。

**边界**＝策划端**零代码/零资产动**（仅裁决＋口径文档锚点）；**美术端下串**＝按本裁更新 3.1.2/3.1.3 清单与画布档，随 **HH.103** 推进。

| 项 | 裁决 |
|----|------|
| 决策 1：城门 1×1 | ✅ 采纳（用户 09-11 拍板；破坏面极小，AI 运行时本就 1×1） |
| 决策 2：D641 脚手架 4→3 | ✅ 修订（全建筑正方形化 ⇒ 2×1 档作废） |
| 决策 3：承接方式 | ✅ 随 HH.103 接入批 |
| 决策 4：清死属性 | ✅ 清（`\.GateOrientation` 全库零消费，复核属实） |
