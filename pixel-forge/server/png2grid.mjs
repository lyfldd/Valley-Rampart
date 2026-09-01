// png2grid.mjs —— PNG → 网格文本（agent 批量回路工具面）
// 用法：node server/png2grid.mjs <workspace 相对 png 路径> [更多路径...]
import * as forge from './forge.mjs';

const files = process.argv.slice(2).filter((a) => !a.startsWith('--'));
if (!files.length) {
  console.error('用法：node server/png2grid.mjs <workspace 相对 png 路径> [更多路径...]');
  process.exit(2);
}
let fail = 0;
for (const f of files) {
  try {
    const r = await forge.convertToGrid(f);
    console.log(`✓ ${f}（调色板 ${r.paletteCount} 色）`);
  } catch (e) {
    fail++;
    console.error(`✗ ${f}：${e.message}`);
  }
}
process.exit(fail ? 1 : 0);
