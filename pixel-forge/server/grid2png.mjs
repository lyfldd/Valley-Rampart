// grid2png.mjs —— 网格文本 → PNG（agent 批量回路工具面）
// 用法：node server/grid2png.mjs <workspace 相对 png 路径> [更多路径...]
// （读取同名 .grid.txt，重新渲染 PNG；grid 是真源）
import * as forge from './forge.mjs';

const files = process.argv.slice(2).filter((a) => !a.startsWith('--'));
if (!files.length) {
  console.error('用法：node server/grid2png.mjs <workspace 相对 png 路径> [更多路径...]');
  process.exit(2);
}
let fail = 0;
for (const f of files) {
  try {
    const text = await forge.readGrid(f);
    await forge.saveGridPair(f, text);
    console.log(`✓ ${f}`);
  } catch (e) {
    fail++;
    console.error(`✗ ${f}：${e.message}`);
  }
}
process.exit(fail ? 1 : 0);
