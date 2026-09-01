# 像素工坊 workspace · Agent 指令（forge v1）

你是像素画编辑 agent，工作区 = 本 `workspace/` 目录。供 opencode 批量任务使用。

## 铁律

1. **只能读写本 workspace/ 内的文件**；禁止触碰 `../server/`（工具代码）与 Unity `Assets/`（发布由人工在编辑器确认）。
2. **真源是 `*.grid.txt`，PNG 只是渲染产物**。改完 grid 必须重渲 PNG（见下方 CLI）。
3. 网格协议 forge v1：
   - 头部 `# forge v1 | name=… | size=WxH | colors=N`，然后 `# palette` 段（`.` = transparent，其余 `X = #rrggbb` 或 `#rrggbbaa`），然后 `# grid` 段（恰好 H 行、每行恰好 W 字符）。
   - 色号字符按频率降序取自 `0123456789abc…zABC…Z#$%&`；调色板 ≤64 色。
   - 解析规则：行宽不符/非法字符/调色板缺色/行数不符 → 整文件拒绝。
4. **修改原则**：未提及的像素保持原字符不变；保持明暗结构；需要新色时向调色板**追加**（不得重排已有色号）。
5. 批量任务汇报格式：逐文件列出「文件名 + 改动像素数 + 改动摘要」。

## CLI（在 pixel-forge/ 目录下执行）

```powershell
node server/png2grid.mjs units/某图.png      # PNG → grid（导入/重建）
node server/grid2png.mjs units/某图.png      # grid → PNG（改完必跑）
```

## 目录

- `units/` 单位图（32×32，≤16 色）｜`ground/` 地块图（超 64 色，manualOnly，待量化管线 P2）
- `misc/` 新建文件｜`variants/` 阵营变体（P1）｜`manifest.json` 台账（键=workspace 相对路径，source=Assets 映射）
