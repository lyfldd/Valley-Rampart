# HH.250 MCP 全失效·终端应急恢复（bridge 直连实测）跨域上报

> 取号：HH.250（账本水位线 248→250，**HH.249 保留给 HH.244 交付报告**，D640 #10）
> 发送方：执行端（TraeCode）→ 主策划端
> 性质：跨域上报（D697 后继 · HH.248 延伸）｜ 处置权：主策划端
> 日期：2026-09-13

---

## 〇、一句话

**在「MCP 工具注入全失效」时，Unity 其实没死，且两条 MCP 都能终端直调**：`mcp_unityMCP` 的 HTTP 服务活着且**长连可完整直调**（streamable HTTP/SSE，keep-alive 保会话，已实测 `read_console`＋`tools/list`）；`mcp_unity-bridge`（Tuanjie AI Bridge）可用**纯终端 TCP 协议直连接管全部 22 类命令**（已实测 `get_state`＋`execute_csharp_script` 通过）。据此建议在 `unity-mcp-first` 补「MCP 全失效·终端应急恢复」章节——**改 skill 由策划端执行**，本报告只提供实证与建议稿。

---

## 一、背景与动机

- HH.248（D697 纪律）已入册 `unity-mcp-first`：「先证明死活，再谈失效」，五步诊断 + 真/伪失效分流。
- 但 HH.248 的恢复路径总表把「真失效」基本导向**换新会话**（agent-handoff 兜底），未覆盖「终端直连接管」这条路。
- 本会话（续跑 HH.244）再次遭遇注入失效后，按用户指示做了**终端直连实测**，得到「不必换会话也能干 Unity 活」的完整路径。

---

## 二、实测证据（全部本会话终端实测，眼见为实）

### ① `mcp_unityMCP`（mcp-for-unity）— **终端长连可完整直调**（streamable HTTP/SSE，会话随连接）

| 探测 | 结果 |
|---|---|
| `Invoke-WebRequest http://127.0.0.1:8080/health` | **200** `{"status":"healthy","message":"MCP for Unity server is running","version":"10.2.0"}` |
| `POST /mcp` initialize（`Accept: application/json, text/event-stream`） | **200**，完整返回 `mcp-for-unity-server 3.4.7`（tools/prompts/resources 全在线） |
| ⚠️ 短连接续发 `tools/list`（`Invoke-WebRequest` 每次新 TCP） | **404 / "Session not found"**（session 绑定连接，连接关即销毁） |
| ✅ **长连接**（Node `http.Agent({keepAlive:true})` 复用同一 TCP）同 session 续发 | **initialize→notify(202)→`read_console`(200 读回 5 条日志)→`tools/list`(200·`refresh_unity` 在册) 全通** |

- 监听进程：`PID 32184`（父 27584）＝`uv python ...\mcp-for-unity.exe --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools --pidfile <proj>\Library\MCPForUnity\RunState\mcp_http_8080.pid --unity-instance-token e8d91d16...`
- **结论**：服务本体健康、HTTP transport 正常；**session 绑定 TCP 连接**——`Invoke-WebRequest`/短连接每次新建 TCP ⇒ session 找不到（伪"不可直调"）；**保持 keep-alive 长连即可完整直调**（见下模板）。
- 这也解释了 Trae 侧注入失效：IDE 客户端的长连断掉后，新工具调用路由到已失效的会话 ⇒ "工具不可用"但服务还活着。

> ⚠️ **勘正 HH.248**：HH.248 记「mcp-for-unity 走 stdio（localhost:6500 未监听）」——本日实测为 **HTTP 8080 形态**（进程命令行硬证）。两者不矛盾：可能服务形态随环境变化（stdio↔HTTP）或并存，报告以**实盘为准**（L-02 家族）。

**终端直调模板（Node，保持 keep-alive 长连）**：

```javascript
// node 直调 mcp_for_unity（streamable HTTP，长连会话）
import http from 'node:http';
const agent = new http.Agent({ keepAlive: true });   // ← 关键：复用同一 TCP 连接
function mcpReq(body, sessionId) {
  return new Promise((res, rej) => {
    const data = JSON.stringify(body);
    const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream', 'Content-Length': Buffer.byteLength(data) };
    if (sessionId) headers['mcp-session-id'] = sessionId;
    const req = http.request('http://127.0.0.1:8080/mcp', { method: 'POST', headers, agent },
      (r) => { const c = []; r.on('data', x => c.push(x)); r.on('end', () => res({ status: r.statusCode, sid: r.headers['mcp-session-id'] || null, body: Buffer.concat(c).toString() })); });
    req.on('error', rej); req.write(data); req.end();
  });
}
const init = await mcpReq({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2024-11-05', capabilities: {}, clientInfo: { name: 'tty', version: '1.0' } } });
await mcpReq({ jsonrpc: '2.0', method: 'notifications/initialized' }, init.sid);
const r = await mcpReq({ jsonrpc: '2.0', id: 2, method: 'tools/call', params: { name: 'read_console', arguments: { action: 'get', types: ['error'], format: 'plain', count: '20' } } }, init.sid);
console.log(r.body);   // event: message\ndata: {...}
```

### ② `mcp_unity-bridge`（Codely / Tuanjie AI Bridge）— **终端可直连，全命令接管**（核心发现）

协议规格（还原自 codely.exe 客户端源码 + 本会话实测验证）：

```
端口发现  读 <proj>\Temp\.com-unity-codely.json → unity_port（动态，本次 57118；reason=ready）
握手      服务器先发一行: WELCOME UNITY-TCP 1 FRAMING=1 SERVER_VERSION=3 PROJECT_ROOT=<url编码>
          客户端回帧: CLIENT_VERSION=2      ← 必须帧化（裸行会被踢）
          再回帧:    PLATFORM=<任意名>
帧格式    8字节大端 uint64 长度 + UTF8 payload
命令      帧 JSON: {"type":"manage_editor","params":{...},"request_id":"manual-1"}
响应      帧 JSON: {"success":true,...,"data":{...}} 带 request_id 关联
```

实测脚本（pwsh，get_state 返回**完整编辑器状态**）：

```powershell
$cfg = Get-Content "C:\Users\trs\Desktop\Valley Rampart\Valley Rampart\Temp\.com-unity-codely.json" | ConvertFrom-Json
$c = New-Object Net.Sockets.TcpClient; $c.Connect("127.0.0.1", [int]$cfg.unity_port); $s = $c.GetStream(); $s.ReadTimeout = 8000
function WF($s,$t){ $p=[Text.Encoding]::UTF8.GetBytes($t); $h=[BitConverter]::GetBytes([UInt64]$p.Length); [Array]::Reverse($h); $s.Write($h,0,8); $s.Write($p,0,$p.Length) }
function RF($s){ $h=New-Object byte[] 8; $r=0; while($r -lt 8){ $n=$s.Read($h,$r,8-$r); if($n -le 0){throw "eof"}; $r+=$n }; [Array]::Reverse($h); $len=[BitConverter]::ToUInt64($h,0); $p=New-Object byte[] $len; $r=0; while($r -lt $len){ $n=$s.Read($p,$r,$len-$r); if($n -le 0){throw "eof"}; $r+=$n }; [Text.Encoding]::UTF8.GetString($p) }
$buf=New-Object byte[] 1; $w=""; while($true){ $n=$s.Read($buf,0,1); if($n -le 0 -or [char]$buf[0] -eq "`n"){break}; $w+=[char]$buf[0] }   # 读 WELCOME
WF $s "CLIENT_VERSION=2"; WF $s "PLATFORM=pwsh-manual"
WF $s '{"type":"manage_editor","params":{"action":"get_state"},"request_id":"m1"}'
RF $s    # ← 响应 JSON
$c.Close()
```

实测响应（节选）：`{"success":true,...,"data":{"isPlaying":false,"isCompiling":false,...,"state":{"editor":{"playMode":"stopped","lastCompilation":{"status":"idle"}},...,"scene":{"activeScenePath":"Assets/Scenes/GameScene.unity"},"console":{"unreadCount":0,"lastErrors":[]}}}`

**万能牌 `execute_csharp_script` 实测通过**（任意 C# 在编辑器执行）：

```powershell
WF $s '{"type":"execute_csharp_script","params":{"script":"return new System.Collections.Generic.Dictionary<string,object>{{"isPlaying",UnityEditor.EditorApplication.isPlaying},{"timeScale",UnityEngine.Time.timeScale},{"scene",UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path}};","summary":"查询Play状态与场景","execution_mode":"editor"},"request_id":"m2"}'
```

实测响应：`result: [ [isPlaying, False], [timeScale, 1], [scene, Assets/Scenes/GameScene.unity] ]`（elapsed 3130ms）

**命令类型全集（22 类）**：`manage_scene / manage_gameobject / manage_asset / manage_editor / manage_script / manage_package / manage_shader / manage_bake / read_console / execute_menu_item / execute_csharp_script / exec_editor_script / exec_runtime_script / manage_screenshot / manage_input / manage_dialog / manage_job / manage_gameview / manage_window_bridge / execute_custom_tool ...`——**终端可全量接管**（换命令只改 `WF` 那一行）。

### ③ bridge 手动开启（bridge 停了的情况）

⚠️ 鸡生蛋：端口没了就没法走 TCP，开启只能在编辑器侧：

1. 编辑器菜单 **AI → Check Connections → Tuanjie AI Bridge 窗口 → Connect 按钮**；
2. 编辑器内置 AI（Cowork）不受 Trae MCP 影响——直接让它"打开 AI Bridge 连接"或执行任意编辑器操作，是**独立通道**；
3. 编辑器重启会自动开（只有手动 Disconnect 过才保持停，重启即恢复）。

### ④ 补充：项目根 vs Temp 的运行时文件（L-02 家族实例）

- **真身**：`<proj>\Temp\.com-unity-codely.json`（本次 `unity_port=57118, reason=ready`, 当日更新）
- **旧副本**：`<proj>\.com-unity-codely.json`（`unity_port=-1`，心跳停在 2026-09-12）——**读错会误判 bridge 已死**（本项目教训库 L-02「直读参数需精确到绝对路径，避免旧副本 vs 运行时真身」的又一实例）。

---

## 三、建议：`unity-mcp-first` 增补「MCP 全失效·终端应急恢复」章节（供策划端定稿）

在「MCP 疑似失效处置（D697 纪律）」之后新增，内容建议：

1. **前置**：五步诊断（D697）仍为第一判据——先分清真/伪失效。
2. **真失效时先终端探活两步（不急着换会话）**：
   - ① `Invoke-WebRequest http://127.0.0.1:8080/health` → 200 说明 `mcp_unityMCP` 服务活着。
   - ② 读 `<proj>\Temp\.com-unity-codely.json`（**非项目根旧副本**）→ 有 `unity_port` 且 `reason=ready` ⇒ bridge 活着。
3. **`mcp_unityMCP` 活 → 终端长连直调**（首选）：按 §二① Node keep-alive 模板，`read_console`/`refresh_unity`/`manage_*` 全量可用（编译验证、场景/资产操作）。**注意 keep-alive 长连是硬前提**（session 绑定 TCP 连接；`Invoke-WebRequest` 短连接会伪"Session not found"）。
4. **bridge 活 → 终端直连接管**：按 §二② 协议 + 脚本模板，`read_console` / `manage_editor` / `execute_csharp_script` 等 22 类命令全量可用（含万能 C#、`EnterTestRun` 菜单执行 `execute_menu_item`）。
5. **bridge 停 → 编辑器侧手动 Connect**（AI → Check Connections → Tuanjie AI Bridge → Connect；或编辑器内置 AI/Cowork 旁路）。
6. **双通道都不行 → 换新会话**（agent-handoff 兜底，HH.248 路径保留）。

---

## 四、请求（策划端）

1. 裁改 `unity-mcp-first`：新增「MCP 全失效·终端应急恢复」章节（本报告 §三 为建议稿，可增删后落册；四端同步 `.codely-cli`/`.trae`/`.workbuddy`/嵌套副本，`check-skills-sync` 校验）。
2. 知悉勘正：HH.248 记「mcp-for-unity 走 stdio/6500 未监听」——本日实测为 HTTP 8080（以实盘为准，L-02）。
3. 本报告为跨域上报，**不改任何 skill、零业务改动、未 push**；HH.244 收尾继续由并行会话执行（交付报告走 HH.249）。

---

> 版本：2026-09-13（执行端）。关联：HH.248（D697）/ HH.247 / HH.244 任务书 / `unity-mcp-first`。

---

## 五、回执区（策划端）

> **裁决：`D698`（2026-09-13，主策划端）｜状态：✅ 准予 + 勘正确认 · 全量落盘**

### 5.1 判据三直读（已过·实读非采信转述）

- **①报告全文**：本报告 §〇~§四逐节读（含红线自检＝零业务改动/未 push/skill 未动）。
- **②实盘旁证（独立复算）**：策划端自行独立探活——
  - `GET http://127.0.0.1:8080/health` → **200** `{"status":"healthy","version":"10.2.0"}`（与报告 §二① 同值）。
  - 实读 `Temp\.com-unity-codely.json` → `unity_port=57118, reason=ready`（与报告 §二② 吻合）；**项目根旧副本** `.com-unity-codely.json` → `unity_port=-1, reason=package_updating, last_heartbeat=2026-09-12`（坐实 L-02 家族陷阱）。
  - 进程命令行硬证（先前已存）＝`mcp-for-unity.exe --transport http --http-url http://127.0.0.1:8080 ...`。
- **③逻辑闭合**：session 绑定 TCP 连接的机理（短连伪"Session not found"）与报告建议的 keep-alive 硬前提自洽。

### 5.2 裁决

1. **✅ 准予 §三 建议稿** → `unity-mcp-first` 四副本新增「MCP 全失效·终端应急恢复（D698 纪律·2026-09-13）」章节，含：终端探活两步（`/health`＋`Temp\.com-unity-codely.json`）→ `mcp_unityMCP` 长连直调（Node keep-alive 模板·硬前提）→ bridge 帧化 TCP 直连（pwsh 模板·22 类命令）→ 编辑器侧 Connect 兜底 → 换会话最后手段；**四端同步完成且哈希一致 `89C39252373F`（407 行，D697/D698 各 1 节）**。
2. **✅ 勘正确认**：HH.248 记「mcp-for-unity 走 stdio/6500 未监听」＝**以实盘为准更正**——本日实测为 **HTTP 8080 形态**（进程命令行 + `/health` 硬证）；已写入 D698 章节 §6 勘正注，HH.248 §六 亦补勘正注。服务形态可随环境变化（stdio↔HTTP 并存可能），**一切以实盘为准**。
3. **✅ 教训入库**：`_策划教训库.md` D698 条目（L-02 家族实例：`Temp\` 运行时真身 vs 项目根旧副本）。
4. **恢复路径升级**：D697「恢复路径总表」的"真失效→换会话"升级为"真失效→**先终端探活**→双通道直连→仍不行才换会话"（D698 §0-5）。

### 5.3 状态

- ✅ **准予 + 勘正确认**；skill 四端同步（含修复 D697 章节重复·哈希 `89C39252373F`）。
- **HH.250 销号**（取号 D698）。HH.244 收尾并行进行（交付 HH.249），不受阻。
