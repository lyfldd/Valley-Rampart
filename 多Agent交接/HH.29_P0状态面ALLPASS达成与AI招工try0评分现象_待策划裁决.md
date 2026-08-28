# HH.29 P0 状态面 ALL PASS 达成 + AI 招工 try0 评分现象报策划裁决

> 类型：待决策（P0 收编验收闭环 + AI⑥招工评分现象定性）→ **[2026-08-28 已裁决 + 份额式修正收口，见 §七/§八/§九]**
> 状态：✅已裁决（决策①判修·结构性锁死→份额式修正终收口/决策②收编验收成立）
> 日期：2026-08-28 · 发起端：执行端 · 关联：HH.27（P0 收官）/ HH.28（Faction 收编收口）
> 前置：HH.28 §五 策划四问全裁 `a23bc53a` + 路线② `b066ad2e` 分笔提交 → 手工 Play 干净 pump 复跑 P0

---

## 一、P0 状态面复跑结果：ALL PASS 达成（收编验收闭环 ✅）

策划 HH.28 裁决③「断言口径对齐已裁决验收形态」改造后，用户在手工 Play 干净 pump 复跑 `Valley2_17_Smoke_P0`，拿到 **`===== ALL PASS(状态面) =====`**，逐项证据：

| 判据 | 结果 | 说明 |
|---|---|---|
| RD2-①轮间清点 | OK | b=2684/2684/2684 u=22/22/22 三轮干净一致 |
| A3 确定性逐字节 | OK | 改动零污染、确定性无破坏 |
| A4 玩家零回归 | OK | 收编未伤玩家侧 |
| B1 玩家招募 | OK(pTrue/True) | 玩家招募通道通 |
| B2 供水抽象产出 | OK | 农产>0 |
| RD2-②存读v2门控 | OK(v2走重建) | loadVer=2 全权重建 |
| B4 剧本三段封顶 | OK | R1-/R2- 无军事（黄项改轮间一致后绿） |
| B5 派遣双证 | OK(build45/45) | 建造实体化通（黄项改后绿） |
| B3+C6 存读回环含脑态 | 黄旗挂2_11 | 独立卡不计 FAIL（HH.27 §二.3） |

**结论**：Faction 收编验收**正式成立**（四问全裁 + A3/A4/RD2 全绿 + 玩家/建造通道全通 + 通道零 faction 依赖已核）。

---

## 二、BUT：探针 B5 触发策划 HH.28 裁决①的「回来找我」分支 ⚠️

策划 HH.28 裁决①给了分叉判据：**「try=0 → 焦点从未选⑥ = 评分问题，不算让渡」**。本次复跑证据坐实了 try=0：

```
[P0完整局] B5黄旗: K1 trainTry=0 trainOk=0（try=0→焦点从未选⑥=评分问题；...）
[P0完整局] B1AItry黄旗: K1 try0/0 ok0/0 K2 try0/0 ok0/0 轮间一致=OK
```

**证据链（排除法已完整）**：
1. **[ExecuteRecruitWorker](KingdomBrain.cs L182-186) 无流浪汉也会 `Bump(train:true, ok:false)` → try+1**。故 try=0 的唯一解释 = **⑥从未被 `ScoreTop` 选为焦点**（ExecuteFocus 从未进 `case RecruitWorker`）。
2. **排除环境让渡**：池空只导致 try=1（进 case 但无候选 ok=0），不会 try=0。
3. **排除收编回归**：`ConvertVagrantsToWorkers`/`FindRecruitableVagrant` 零 faction 依赖已核（HH.28 §5.1）。
4. **已核实评分面**：[⑥ RecruitWorker axis=Expansion](UtilityActionConfig.cs L29)、need=0.6（工人4/目标10）**本应很高**；却被长期压制 → 最可能是 **K1/K2 国王 Expansion 轴偏低**（[ScoreTop 性格乘入](UtilityScorer.cs L81-83)，轴→0 该项出局）。

---

## 三、这正面回应了历史 `763c8fa0`「train0 归人工 Play」的真正机制

历史把 B1/B4/B5 的 train0 归因成「流浪汉池空=环境断层」。**本次 try 分层探针揭示真相**：不是「无流浪汉」，而是**「⑥ 评分长期不优于建造项（该 AI 性格低扩张）」** —— 评分层行为，非环境。这是策划①补探针的一个有效产出。

---

## 四、需策划裁决（两个决策点）

### 决策①：try0 评分现象定性（两种之一）

- **判「性格分化正常涌现」（收编按 ALL PASS 收口）**：⑥ need=0.6 高、被选是正常的；该局 K1/K2 只因 Expansion 轴低没选，是五个性格轴「不同 AI 国王偏好不同」的设计使然。判定无 bug，P0 收编验收收口，不改评分。
- **判「RecruitWorker 评分需调优」（走路线②审计）**：⑥ 是 AI 人口增长唯一通道（防卡死存活期，KingdomBrain 注释明言），need=0.6 应足够压过建造；长期 0 选说明评分偏弱，需实机录焦点线定位是「错轴」还是「needA=10 过高」还是「权重」，再调。

### 决策②：收编验收是否据此正式收口

- 收编验收证据已齐全（A3/A4/RD2 + 通道零归属依赖）。是否接受第一节结论、将 HH.28 的「待裁」状态翻为「已裁·收编验收成立」，由策划定夺。

---

## 五、关联待办（不阻塞本裁决）

- 路线② 效用补全代码 `b066ad2e` 已提交（⑦⑧⑨⑫+⑪⑮占位 + 冒烟#5 兵力目标随威胁上调全绿），待 P0 验收回填后跑路线② 全量手工验收。
- 决策①若判「需调优」，调优属路线② 范畴，与 Faction 收编验收解耦。

## 六、遗留盘点（HH.27 让渡项复述，仍有效）

- A1/A2/B2 经济产出闭环=走位驱动，pump 无帧不产 → 归人工 Play（本次已收时间线证据）。
- 玩家死亡/GameOver 链路（ThroneAnchor 被禁）留独立回归（HH.27 ②）。

---

## 七、策划裁决回写（2026-08-28 已裁决）

**决策①：try0 定性 = 结构性锁死（非"正常涌现"、非单纯"调参"）→ 判修。** 证据链四环（策划核码取证采纳）：
1. 数学实锤：⑥ need=(10-4)/10=0.6 全池最高且恒定（工人卡4→缺口永在），败因纯在轴乘入——六模板 expansion 轴实测 0.25~0.65（SnowRock 0.25最低），低扩张国 ⑥=0.6×0.25=0.15。
2. 自增强环：BuildHouse need=pop/10=0.4 恒定本身是人口卡死产物（建房赢→工人永涨→45建筑/4工人=鬼城奇观）。
3. 设计合同被击穿：KingdomBrain 自注释「⑥=AI人口增长唯一途径防卡死」+ 2_15「剧本保下限」双重失效；Develop→Expand worker≥8 永不可达=P0 验收「成长至扩张期」永假。
4. 非 pump 假象：数学在真实 Play 同样跑，黄旗「招工归人工」前提（链路通只差环境）已被推翻。

**修法（已实施，commit `3782482`）**：D322 常设底线扩充——人口底线（自造件，贴既有哲学）：
- `KingdomBrainConfig.popFloor`（SO 占位 **6**），`workerCount < popFloor` → 强制⑥焦点（`FocusRecruitWorker` 常量化），触发式、不评分、跳防抖，与粮底线/被攻击完全同构（粮→人口→被攻击三级底线序）。
- 不选调轴值（症状补丁）/不选 need 加权（破坏 D323 四因子纯度）。
- 阶段执行序实施：`FocusController.Update` 底线段插人口底线；`KingdomBrainConfig.asset` 落 popFloor=6；冒烟#5 追加 `PopFloorGuard` 配置探针（常量映射=⑥、popFloor 有效∈(0,RecruitWorker.needA]、初始4<6 触发）。

**决策②：收编验收成立，正式收口。** try0 与收编无关已证（通道零 faction 依赖 + 纯轮 A3 逐字节一致 + 玩家/建造链全通）。HH.28 翻「已裁·收编验收成立」。

**验收（∠）**：修后 P0 套件 K1/K2 `trainTry>0` / worker 突破 popFloor / 剧本时间线出现 E 段；冒烟加断言「最低扩张模板国（SnowRock 池）45 天内 worker≥popFloor」。
⚠️ **注：运行时验证归手工 Play**——MCP 环境下 Unity 编辑器后台文件监视失效，改动代码已入磁盘（源码编译 0 error、IDE 诊断零错），但新逻辑需 Unity 重新编译后手工跑 P0/冒烟验证（与 P0 权威验收同治理，历史已接受）。

**杂务**：追踪表补账（git-plan-sync 第2步）单列 docs commit；本 HH 回写随收口提交。

---

## 八、仲裁精确化（2026-08-28 二次裁决，覆盖 §七 修法）

**策划三问仲裁**：①推断认可但精确化；②③驳回「pump 环境限制 / 转真帧」——这是 **popFloor 配置踩线缺陷**，真帧救不了。

**证据链（策划核 KingdomFoundingConfig.asset）**：
- 错峰三档 `workerCount = 4/6/8`（帐篷/村落/要塞）；P0 `difficulty=2 Medium`=村落档 → **K1 初始工人恰=6=popFloor → 6<6=false → popAlarm 全程不触发**。非"≥6"模糊属性，是精确踩线。
- 后果链：村落 6 工人 → `developToExpand_workersMin=8` 不可达（招工被评分压制）→ 卡 Develop = 决策①"结构性锁死"在 Medium 档原样存活。popFloor=6 只救 Easy 帐篷档(4<6)，Hard 要塞自带 8、Medium 村落(6)被漏。真帧 Play 同样踩线 → 转真帧等的还是 try=0。
- **第③驳回"转真帧"**：此金线 pump 完全可验——⑥执行器 `ConvertVagrantsToWorkers` 直转无走位依赖，build45 证 pump 执行链通；只要 popAlarm 触发，try 必>0（无候选也 try+1）。pump 验不了的是 ok 侧（流浪汉在场性），非 try 侧。

**修法裁决（已实施 commit `eea8d10`，一处改动）**：
```csharp
bool popAlarm = kingdom.workerCount < Mathf.Max(brainCfg.popFloor, brainCfg.developToExpand_workersMin);
```
- 底线语义对齐升级合同："保增长下限"的下限 = 能升 Expand 的工人数，自动跟 SO 阈值联动（developToExpand 改了不用同步改 popFloor）。
- 三档错峰达成：帐篷4<8✓ / 村落6<8✓ / 要塞8<8 不触发（已达标，评分自由）✓。
- 冒烟#5 `PopFloorGuard` 同步改 max 联动断言（村落6<8✓触发、要塞8 不倒灌）。

**验收（pump 内闭环，不转真帧）**：重跑 P0 套件 → K1/K2 `trainTry>0`（金线）+ 冒烟断言"村落档 6 工人触发底线"。ok 侧维持归人工黄旗不变。

**现状**：代码已提交 `eea8d10`，冒烟#5 探针已 MCP 实测绿（`门槛=max(6,8)=8,村落6<8✓,要塞8<8=不倒灌`），**但 P0 重跑的 try>0 金线尚未回填**（待手工 Play 跑 P0 完整局确认 B5 trainTry>0）。决策①至此才算真收口。

---


## 九、份额式修正（2026-08-28 三次裁决，覆盖 §八 独占修法；HH.30 定性纠偏 + Yjy裁决）

**策划核码前置纠偏**：P0 复跑显示 try>0 达成（K1/K2 try=45）但 **B5 build=0 FAIL**——执行端一度按"环境让渡黄旗"上报，策划否决该定性：
- **这不是纯环境让渡，真帧同样饿死建造，只是死得慢一点。** 真帧有流浪汉→招满 8 人→popAlarm 释放没错；但触发到招满的真空窗内 ⑥**独占**焦点（FocusController 原 if(popAlarm) return 排他），评分/建造全停。流浪汉靠营地补员(D371=1/日)，招满 2 人缺口≈2+ 日全停建造；候选稀薄（流民游荡/被玩家抢招/被怪杀）⇒真空窗拉到周级 = AI 在"保增长"名义下冻结核经济。
- 违背 D322 底线设计本意（底线=保命兜底，不该吞正常经营），与决策①"⑥永久不被选"是**镜像缺陷**：一个永不被选、一个独占到饿死。

**裁决：产品修（小改），非断言放宽。** 底线语义从"独占"改"份额"：
- popAlarm 触发→⑥占 popAlarmFocusCapDays（SO 占位 **2**）日→第 popAlarmFocusCapDays+1 日**让位 1 轮给评分焦点**（含建造，强制切换跳过防抖）→下轮若仍 popAlarm 再回来（相位轮替）。与粮底线 grainReserveDaysFloor 时窗语义同构。
- 改后：真空窗内建造仅停 popAlarmFocusCapDays+1 日一轮，随后每 popAlarm 期至少 1 轮建造落地；⑥仍高频尝试（try 涨）。

**实施（协作已落盘，未提交）**：
- FocusController.cs：新增 _wasPopAlarm/_popWindowStartDay 窗口字段 + 份额相位 phase=day-windowStart; recruitedTurn = phase%(cap+1) < cap；⑥日强制并 return，让位日走评分（强制切换）。注释 HH.30 定性。
- KingdomBrainConfig.cs + .asset：新增 popAlarmFocusCapDays=2（SO 占位）。
- 冒烟#5 PopFloorGuard：追加份额轮替断言（2 周期 ⑥⑥B⑥⑥B 纯谓词）。

**验收（pump 三绿，已达成）**：
- 冒烟#5 ALL PASS（编辑态，含 份额轮替=OK(cap=2 周期=3 相位⑥…B轮替)，MCP 实测）。
- P0 完整局 ALL PASS(状态面)：K1/K2 	rainTry=30（⑥占 2/3 日，金线不回退）+ **B5 build=15/15 恢复硬断言绿**（建造占 1/3 回合）——30:15 精确比例印证 ⑥⑥B 轮替。
- **决策① 至此真收口。** ok 侧(流浪汉在场)/走位经济/GameOver 三条真帧黄旗维持不变。