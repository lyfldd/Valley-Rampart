# BUGFIX：GameOver 界面"返回主菜单"按钮无法点击（UI 坐标错位）

> 2026-08-02 · 提交 ebf53b2

## 现象

君主死亡弹出结算面板（GameOver）后，"返回主菜单"按钮**点击无效、无 hover 高亮**，Console 无点击相关报错（仅君主死亡 LogError）。

## 排查过程（排除项）

- ❌ 君主身上脚本被销毁：君主 Prefab 仅 UnitController/PlayerInputHandler，GameOverUI 是场景独立根对象，不依赖君主
- ❌ 按钮未绑定：运行时 `_buttonBound=True`，clicked 已注册
- ❌ 元素遮挡：`Panel.Pick(按钮中心)` 命中按钮本身
- ❌ 点击回调异常：反射调用 `OnBackToMenuClicked` 成功切到 MainMenuScene
- ❌ 指针捕获泄漏：`PointerDispatchState.m_PointerCapture` 全 null
- ❌ timeScale：0 和 1 下物理输入都不进 Panel（对照实验）

## 根因

**ScaleWithScreenSize 超屏裁剪导致渲染与输入坐标错位。**

- PanelSettings 参考分辨率 1920x1080，实际 GameView 屏幕 **960x475**（约 2:1）
- 按宽匹配 scale=0.5 → Panel 高 540 > 屏幕 475 → **垂直超屏被裁剪**
- 实测：按钮输入坐标物理位置 (420~540, 282~307)，玩家鼠标实际停在 (469, 321)——**在按钮下方 ~14px**，点击全部落在面板空白上

## 修复点

| 文件 | 改动 |
|------|------|
| `Assets/_Game/UI/PanelSettings.asset` | 参考分辨率 **1920x1080 → 1920x950**（匹配实际屏幕比例，scale=0.5 后高度 475 = 屏幕高，无超屏无错位）——核心修复 |
| `Systems/UI/PausePanel.cs:84-94` | `OnGameStateChanged` 不再在 GameOver 时强制 `Time.timeScale=1`（GameOver 冻结由 GameOverPanel 管理；原逻辑会把结算冻结覆盖回 1，导致世界继续跑） |
| `Systems/UI/GameOverPanel.cs:64-72` | `Show()` 补绑按钮（防 OnEnable 时序导致绑定失败） |
| `Systems/UI/LoadingPanel.cs:11-26` | 新增 `Start()` 兜底隐藏（防 OnEnable 时 rootVisualElement 未就绪导致全屏 loading 面板残留拦截点击） |

## 验证

正常 Play → 君主死亡 → 结算面板弹出（世界冻结 timeScale=0）→ "返回主菜单"按钮 hover 高亮正常 → 点击成功回到主菜单。

> 影响面：本次修复同时修正所有 UI 面板（暂停/建造/作战/AI 调试）的鼠标点击命中。参考分辨率 1920x950 在 16:9 全屏下会底部留空（无错位），可接受。
