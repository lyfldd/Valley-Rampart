# HH.248 Unity MCP 会话连接频繁中断 — 跨域上报（执行端 → 主策划端）

> 取号：HH.248（账本水位线 247→248）
> 类型：跨域上报（工具/基础设施类·非游戏缺陷）
> 状态：🟡 待策划端排查处置
> 关联：HH.244（A+ 口径修正探针微批）· HH.247（开工回执）

---

## 〇、一句话结论

**Unity MCP（`mcp_unityMCP`/`mcp_unity-bridge`）在本会话启动时注入失败（工具 not available），导致 HH.244 收尾（编译验证＋正门跑局）无法在本会话完成；这是**反复发生的已知问题**（此前多个执行端/训练师会话同样因 MCP 失效而被迫开新会话续跑），请策划端牵头排查根因。**

---

## 一、现象（本次实证）

- 本会话（HH.244/HH.247 执行端会话）：`mcp_unityMCP` 与 `mcp_unity-bridge` 两套 Unity MCP **均未注入**——`DeferExecuteTool refresh_unity`/`read_console`/`execute_csharp_script`/`unity_console` 等全部报 `not available in the current environment or configuration`。
- 已排查：MCP 配置目录存在且**为最新**（`c:\Users\trs\.trae-cn\mcps\s_Valley_Rampart-25828998\solo_agent\` 下 `mcp_unityMCP/`、`mcp_unity-bridge/` 均含完整 schema，`LastWriteTime=13:01:36`＝本会话启动时生成）；`SERVER_METADATA.json` 正常（server_name=`mcp_unityMCP`）。**配置与 schema 均无损坏，纯「注入未生效」**。
- 用户侧确认：这是一直存在的老问题——MCP 带**会话唯一 ID**，会话开久后该 ID 失效，重启当前会话无效，**开新会话后 MCP 恢复可用**（用户已实测）。

## 二、影响面

1. **阻塞 Unity 侧一切 MCP 操作**：编译验证（`refresh_unity`＋`read_console`）、跑局正门、资产/场景/SO 操作全部不可用——均为 `unity-mcp-first` 硬纪律所要求的正门路径。
2. **直接后果（本次）**：HH.244 件1 代码已落盘但**未经编译验证**、件2 探针**未跑**——红线（编译 0 error、正门进局）无法满足，交付被迫中断。已写 `HH.247 附·会话中断接续指引`，新会话可无缝续跑。
3. **历史损失面**：用户反馈此前多次被迫「开新会话重试才能用 MCP」，每次中断都伴随上下文/进度重载成本（本项目已有 `agent-handoff` 纪律兜底，但根因未除，反复消耗）。

## 三、疑似根因（供排查参考·非结论）

- MCP server 注入与**会话 ID 绑定**（`s_Valley_Rampart-25828998` 目录结构可见）；会话生命周期内该绑定失效（可能是 IDE 侧 MCP client 连接超时/心跳丢失/会话复用 bug）。
- 本环境 `mcp-for-unity` server 走 **stdio transport**（本地 `localhost:6500` HTTP 未监听；进程 `uvx`/`python` 运行正常）——stdio 长连接更易因 IDE 焦点/休眠/线程池变化而断，且断开后无自动重连路径。
- 与 Unity 编辑器本体无关（编辑器进程 `Tuanjie` 正常，`mcp-for-unity` 插件进程 live）。

## 四、建议排查方向（策划端牵头）

1. **查 Trae IDE 侧 MCP client 配置**：`mcp_unityMCP`/`mcp_unity-bridge` 是否注册进当前工作区配置；会话 ID 续期机制。
2. **查 `uvx` server 进程存活与重连日志**：`mcp-for-unity` 是否有断线自动重连（IDE 端 server 重启策略）。
3. **评估 stdio → HTTP transport 迁移**：README 显示支持 `Start Local HTTP Server`（端口 6500），HTTP 长连通常更稳，可作候选根治方案。
4. **建立「MCP 失效即开新会话」的 SOP**：在 `agent-handoff` 或 `unity-mcp-first` 内登记处置动作（本次已在 HH.247 落接续指引作为实例）。

## 五、对 HH.244 的处置（本端已就绪）

- 件1 代码改动（2 文件）落盘在磁盘、未 commit；开工回执+接续指引已 commit（`52a1094`/`8561cdb`）。
- 新会话（MCP 可用）按 H​​H.247「附·接续指引」四步收尾：编译验证→跑探针（seed73621/槽 `chain_probe2`/D60）→出 DZ-148 结论→交付报告（预计 HH.249）。
- 本上报不阻塞该链路，仅请策划端知悉根因并安排排查。

---

> 红线自检：本上报零业务代码改动；未 push；`Assets/_Game/**` 只读；AI.Core 零触碰（前会话开工回执已核）。