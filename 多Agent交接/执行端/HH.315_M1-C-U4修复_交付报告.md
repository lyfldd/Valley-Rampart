# HH.315 · M1-C · U-4 修复 · 交付报告

- **取号**：HH.315（U-4 修复片 · 承 HH.315 U-1 修复交付 `75b5f3c4` ⇒ D796 验收）
- **日期**：2026-09-20
- **执行端**：TraeCode
- **任务来源**：策划端 D796 验收报告「判据 1·2·3·5·6·7 逐值复算通过，U-1／U-3 确认修好；⭐ 新发现 U-4（本片新引入 · 阻塞 M1-C 销号）」＋ 用户施工任务书
- **状态**：✅ 全件完成（件1/件2/件3 落码 ＋ 编译门禁 ＋ 判据 8′ 实测 ＋ 收尾三态核验）

---

## 一、背景与缺陷定位（承 D796）

`FinishDemolish()`（[Building.cs:867-879](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L867-L879)）中：

- `:869 refundPack = BuildRefundPack()` 在 `_awaitingMaterials`（升级投料中）时**已含** `_siteStore.Contents`（原 `:899`）；
- `:877 DropSiteStoreToChest()` 又取**同一批料**掉一次箱（`:994` · 并 `Clear`）；
- `SpawnChest` 不去重 ⇒ **同批料进两个箱子**。
- 逐值（House 升级投料中 Stone 3/16）：落箱合计 {Wood:4, Stone:6} ＝ **Stone 多退 3**。
- `:878 Die` 再掉 ⇒ 已清空 ⇒ 幂等（无问题，实测确认）。

## 二、件1 · 修法裁 (a)（Building.cs · LF）

**改动**：删除 `BuildRefundPack()` 内 `if (_siteStore != null) pack = pack + _siteStore.Contents;` 一行；`GoldOnlyOf(CurrentStageCost())` **保留**（金是下单即扣的在投阶段已付部分，必须退）。

**职责分离口径（注释同步落码）**：
- `BuildRefundPack` ＝ 退还「**已支付造价**」（`PaidStageCost()` ＋ 在投阶段已付金）；
- `DropSiteStoreToChest` ＝ 工地仓「**内容物**」掉箱，**唯一掉箱口**（`FinishDemolish:877` 行注释已标注 ⭐ U-4）；
- ⚠️ `DropSiteStoreToChest` 必须保留：`Die` 路径（工地被打毁）靠它 ⇒ 材料不丢；
- ⛔ 不改签名；掉箱 `Faction.None` 不变。

改后 [BuildRefundPack](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L894-L915) 全文见源码（注释含 U-1 旧口径退役说明 ＋ U-4 双计机理 ＋ 职责分离声明）。

## 三、件2 · N-2 准补（BuildingFactory.cs · CRLF · 1 行）

`:346` 陈注释「（D155 修复成本基数 / D162 拆除返还基数）」⇒ 勘正为：

> `2_12 步骤7 / D155：累计投入件数恢复（⚠️ M1-C · U-1 后**备而未用** ⇒ 仅存档往返保真，⛔ 不入算式）。旧档缺字段 → 兜底按 def.cost。`

（CRLF 文件按行尾纪律以 python 二进制按行替换落码，hit=1，行尾守恒 381/381。）

## 四、件3 · N-3 准补（Building.cs · 零行为）

[LoadState](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1068-L1080) 新增防御性 summary：`state` 由创建时 `initialState` 置入（真实读档路径 `BuildingFactory.SpawnFromSave`：`InstantiateFromDef(..., (BuildingState)data.state)` ⇒ `BuildingFactory:155`）⇒ ⛔ 勿直接对已存在实例调本方法（否则 `state` 沿用旧值，实测：升级料齐未完工建筑读档后 `state` 退化为 `Active`，绕过 `OnConstructionComplete` 收尾）。

## 五、件4 · 判据 8′「落箱总量」实测 ⭐

### 5.0 鉴别力声明（前置）

> **若 `:899` 未删，本判据应读成 Stone:6；删对则 Stone:3。**

样本 A（House 升级投料中 Stone 3/16 · awaiting=True）落箱总量在两版代码下取值不同 ⇒ **有鉴别力**；
样本 B（未投料 Active 直接拆）两版同值（合计＝累计造价 {Wood:4}）⇒ **无鉴别力**（如实标注，仅作零值对照）。

### 5.1 实测读数（探针实录 · 逐值）

| 读数项 | 实测值 | 判定 |
|---|---|---|
| 样本 A 投料 `A_contents` | `石材×3` | ✅ Deposit(Stone,3) 入工地仓 |
| **判据 5（修后 BuildRefundPack 实读）** | **`木材×4`** | ✅ 不再含 Stone（＝「已支付造价」口径） |
| 旧 `:899` refund **复刻**（newRefund＋contents） | `木材×4, 石材×3` | —（算术复刻，非旧代码实跑） |
| **改前落箱总量复刻**（oldRefund＋contents） | **`木材×4, 石材×6`** | ✅ ＝双计（与任务书逐值一致） |
| **修后落箱实测**（FinishDemolish 后 FillChestsInCellRect 汇总） | `newChests=2 · total=木材×4, 石材×3`（两箱分立：refund 箱 木材×4 ＋ 工地仓箱 石材×3 · 同格 (500,500) 共存） | ✅ **＝任务书「修后 {Wood:4, Stone:3}（正确）」逐值命中** |
| 样本 B（空对照）refund | `木材×4` | ✅ ＝累计造价 |
| 样本 B 落箱实测 | `newChests=1 · total=木材×4` | ✅ 零值对照（无鉴别力 · 已声明） |

**结论**：`Stone:3`（非 `Stone:6`）⇒ 按 5.0 鉴别力声明，**:899 叠加已删对**；判据 8′ 通过。

### 5.2 判据 5 口径同步更新

修 (a) 后 `BuildRefundPack()` 单独返回 **{Wood:4}**（不再是 {Wood:4, Stone:3}）——实测 `J5_newRefund=木材×4` 确认；原判据 5 若列旧值即「口径已过时」，本片起以「落箱总量（判据 8′）＋ refund 返回值（判据 5 新口径）」双读数为验收面。

## 六、探针取证方法学声明（如实列报）

- **环境**：编辑态（`isPlaying=False` · GameScene · roots=50）＋ 直连 HTTP MCP（`127.0.0.1:8080/health`＝200 · v10.2.0 · 本会话 MCP 工具未注入，按 HH.244 教训走 HTTP JSON-RPC，零 Unity 资产/场景落盘改动）。
- **种子单例方案**：FinishDemolish/Die/SpawnChest 路径触达的 `Singleton<T>` 全量 seed（ChestManager/BuildingFactory/TrainingSystem/KingdomRegistry/SaveManager＝新建 GO＋AddComponent＋反射 seed；GridSystem/BuildingRegistry/TaskScheduler/TimeManager＝复用场景真实实例）；⛔ 全程不裸触 `Instance` getter（防编辑态 DDOL 自动创建异常）。
- **如实列报 1（seed 说明）**：recon2 曾见场景 inactive ChestManager，本探针 `FindObjectsOfTypeAll` 未再寻获（编辑器会话间状态差异）⇒ 改新建 seed 实例（`created=True`）——测量在独立 `_chests` 上进行（基线 0 箱），读数有效性不受影响。
- **如实列报 2（复刻口径）**：`J8p_oldRefund/oldTotal` 为**算术复刻**（newRefund＋contents 逐步相加），非旧代码实跑——旧代码已删，复刻值仅用于呈现「改前应然」。
- **收尾三态**：箱子 Remove＋DestroyImmediate（含 leftover 按坐标兜底扫）、建筑 DestroyImmediate、seed GO 销毁、9 个 `_instance` 快照全部复原；**残留=0**、roots 50→50、**isDirty=False**（新建→DestroyImmediate 不经 Undo ⇒ 不标脏 ⇒ 无需重开场景复原）。收尾读数 `chestCountAfter=-1` 系 seed GO 先行销毁后的 fake-null 判空瑕疵（读数链如实列出），以 `residual=0` 为准。
- **清理前遗留**：更早会话崩溃残留 4 个 `[Singleton]` GO（ChestManager/TerritorySystem/KingdomRegistry/SaveManager）已在本片 recon 阶段清零。

## 七、门禁与红线遵守声明

| 项 | 结果 |
|---|---|
| 编译门禁（refresh_unity force/scripts/request ＋ read_console） | ✅ 0 警告 0 错误 |
| 改动面 | ✅ 仅 `Building.cs`（LF）＋ `BuildingFactory.cs`（CRLF · python 二进制改）两文件，git diff --stat 核对在场（+18/-4 与 +1/-1） |
| ⛔ 不动清单（WarehousePanel / TreasureVault / 四档账本 / 美术 / pixel-forge / GameScene / Packages / 3.6·3.8 doc） | ✅ 未碰（工作树中他人/在飞改动一律未纳入本批提交） |
| push | ✅ 未 push（按要求保持本地） |
| 具名 add | ✅ 仅 `git add` 本批 3 个文件（两源码 ＋ 本报告） |
| 阻塞歧义 | 无（未触发停手报裁条款） |

## 八、验收请示

请策划端按任务书验收线复核：判据 8′（5.1 表逐值）＋ 判据 5 口径更新（5.2）＋ 件1/件2/件3 落码。验收通过后 M1-C 的 U-4 缺陷可销号（U-4 为 D796 判定的 M1-C 销号阻塞项）。
