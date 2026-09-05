# Bridge 反馈：Unity MCP 三缺陷打包（转交 bridge 维护方）

- **来源**：河谷防线项目（团结引擎 1.8.5，Unity 2022.3.62t7 兼容）· Codely Unity bridge MCP 实测
- **整理**：策划端 2026-09-05（HH.46 §三-3/4 · HH.49 §五-1/2 · HH.65 §三#4 · D523/D530 挂账汇总）
- **状态**：unity_scene 已复发 2 次并升级为项目常设纪律（禁用该工具）；unity_input/rightButton 为一次性复现记录

---

## 缺陷 1：unity_scene save 在团结引擎下产出重复场景文件（复发 2 次）

- **现象**：bridge `unity_scene save` 执行后，工程内出现 `GameScene.scene`（.scene 扩展名）与既有 `GameScene.unity` 并存的重复文件；工具回显 "saved to GameScene.scene" 自报成功。
- **危害**：重复文件入工程会被 Unity/团结当作新资产处理，存在场景引用混乱风险；两次均靠人工比对 GUID（Build Settings 引用 2cda990e… 完好）+确认 `.unity` 零改动（git diff 空）+isDirty=false 后删除 `.scene`+`.meta` 就地修复，零数据损伤。
- **复发记录**：HH.46（2026-09-01，首次）→ HH.65 段B#4（2026-09-04，第 2 次）。
- **项目侧已采取措施**：常设纪律=执行端禁用 bridge `unity_scene save`，场景保存一律 execute_code `EditorSceneManager.SaveOpenScenes()` 或编辑器手动保存（HH.65/D523 批准）。
- **请求**：排查团结引擎（非原版 Unity）下 save 实现的文件扩展名/资产数据库路径逻辑。

## 缺陷 2：unity_input mouse button 校验矛盾

- **现象**：unity_input 工具对鼠标按键的校验逻辑存在自相矛盾（同一按键状态在不同校验分支判定不一致），导致合法注入请求被拒。
- **记录**：HH.49 §五-1（2026-09-02）。
- **请求**：校验分支对齐（mouse button 枚举口径统一）。

## 缺陷 3：虚拟设备 rightButton 注入异常

- **现象**：`QueueStateEvent` 注入 `buttons=2`（右键）后，Input System 侧 `isPressed=False`——右键按下态未生效；同法注入左键（buttons=1）正常。
- **影响**：依赖右键按下的交互测试（如框选/右键菜单）无法用虚拟设备自动化，只能物理鼠标人工点验。
- **记录**：HH.49 §五-2（2026-09-02）。
- **请求**：排查 QueueStateEvent 右键位的 Input System 传播链（是否需配合 enabled 状态或 Vector2 位置前置）。

---

*项目侧联系人：策划端（河谷防线）。三缺陷均已有项目侧 workaround，不阻塞开发；反馈目的=bridge 对团结引擎兼容性改进。*
