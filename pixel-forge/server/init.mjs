// init.mjs —— 首次初始化：目录骨架 + config + 导入 Assets 单位/地块图
// 用法：node server/init.mjs [--units-dir Assets/_Game/Art/Units] [--ground-dir Assets/_Game/Art/Ground]
import { promises as fs } from 'node:fs';
import path from 'node:path';
import * as forge from './forge.mjs';

const args = new Map();
process.argv.slice(2).forEach((a, i) => {
  const m = a.match(/^--([\w-]+)(?:=(.*))?$/);
  if (m) args.set(m[1], m[2] ?? process.argv.slice(2)[i + 1]);
});

const UNITS_DIR = args.get('units-dir') || 'Assets/_Game/Art/Units';
const GROUND_DIR = args.get('ground-dir') || 'Assets/_Game/Art/Ground';

await forge.ensureConfig();
for (const d of ['units', 'ground', 'variants', 'misc']) {
  await fs.mkdir(path.join(forge.WORKSPACE, d), { recursive: true });
}

const cfg = await forge.loadConfig();
const ur = forge.resolveUnityRoot(cfg);
const valid = await forge.unityRootValid(ur);
console.log(`[init] Unity 工程根：${ur} ${valid ? '（有效）' : '（⚠ 无效，请检查 workspace/config.json 的 unityRoot）'}`);

if (valid) {
  console.log(`[init] 导入单位图 ${UNITS_DIR} ...`);
  const ru = await forge.importDir(ur, UNITS_DIR);
  for (const r of ru) console.log(`  ${r.error ? '✗' : r.skipped ? '·' : '✓'} ${r.file}${r.manualOnly ? '（超 64 色，标记 manualOnly，待量化管线 P2）' : ''}${r.error ? '：' + r.error : ''}`);

  console.log(`[init] 导入地块图 ${GROUND_DIR} ...`);
  const rg = await forge.importDir(ur, GROUND_DIR);
  for (const r of rg) console.log(`  ${r.error ? '✗' : r.skipped ? '·' : '✓'} ${r.file}${r.manualOnly ? '（超 64 色，标记 manualOnly，待量化管线 P2）' : ''}${r.error ? '：' + r.error : ''}`);
}

const files = await forge.listFiles();
const editable = files.filter((f) => f.hasGrid).length;
console.log(`[init] 完成：台账 ${files.length} 项，可编辑（有网格）${editable} 项`);
