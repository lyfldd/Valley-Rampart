// test-publish.mjs —— 门禁 T6（发布流程：.meta 保护 / 覆盖 / 外部修改冲突 / force）
// 使用 .selftest-fakeroot 作为假 Unity 工程根，不触碰真实 Assets。
import { promises as fs } from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import * as forge from './forge.mjs';

const TOOL_ROOT = forge.TOOL_ROOT;
const FAKE_ROOT = path.join(TOOL_ROOT, '.selftest-fakeroot');
const FAKE_UNITS = path.join(FAKE_ROOT, 'Assets/_Game/Art/Units');
const SRC = path.join(TOOL_ROOT, '..', 'Valley Rampart', 'Assets/_Game/Art/Units');
const WS_KEY = 'units/__selftest__.png';

let pass = 0;
let fail = 0;
const results = [];
const report = (n, ok, d = '') => {
  results.push(`${ok ? 'PASS' : 'FAIL'} ${n}${d ? ' — ' + d : ''}`);
  ok ? pass++ : fail++;
};
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');

try {
  // 准备假 Unity 根
  await fs.rm(FAKE_ROOT, { recursive: true, force: true });
  await fs.mkdir(FAKE_UNITS, { recursive: true });
  const sample = (await fs.readdir(SRC)).filter((f) => f.endsWith('.png')).sort()[0];
  const sampleBytes = await fs.readFile(path.join(SRC, sample));
  await fs.writeFile(path.join(FAKE_UNITS, '__selftest__.png'), sampleBytes);
  // 预置 .meta（模拟 Unity 生成，验证发布绝不碰它）
  const META_CONTENT = 'fileFormatVersion: 2\nguid: 11112222333344445555666677778888\n';
  await fs.writeFile(path.join(FAKE_UNITS, '__selftest__.png.meta'), META_CONTENT);

  // 1) 导入
  const imp = await forge.importAsset(FAKE_ROOT, 'Assets/_Game/Art/Units/__selftest__.png', '自测');
  report('T6-1 导入', imp.key === WS_KEY && !imp.entry.manualOnly, `key=${imp.key}`);

  // 2) 改一个像素后成对保存
  const gridText = await forge.readGrid(WS_KEY);
  const lines = gridText.split('\n');
  const gridIdx = lines.indexOf('# grid') + 1;
  const row = lines[gridIdx].split('');
  const origChar = row[0];
  row[0] = row[0] === '.' ? '0' : (row[0] === '0' ? (lines[gridIdx + 1][0] !== '.' ? '.' : '0') : '.');
  lines[gridIdx] = row.join('');
  await forge.saveGridPair(WS_KEY, lines.join('\n'));
  const wsPng = await fs.readFile(path.join(forge.WORKSPACE, WS_KEY));
  report('T6-2 修改后保存', sha(wsPng) !== sha(sampleBytes), '工作区 PNG 已与原图不同');

  // 3) 发布（正常覆盖）
  const pub1 = await forge.publish(FAKE_ROOT, [WS_KEY]);
  const targetBytes = await fs.readFile(path.join(FAKE_UNITS, '__selftest__.png'));
  report('T6-3 发布覆盖', pub1[0].status === 'published' && sha(targetBytes) === sha(wsPng), '目标与工作区一致');

  // 4) .meta 字节不变（铁律）
  const metaAfter = await fs.readFile(path.join(FAKE_UNITS, '__selftest__.png.meta'), 'utf8');
  report('T6-4 .meta 保护', metaAfter === META_CONTENT, '发布后 .meta 逐字节未动');

  // 5) 外部修改目标 → 预检 conflict，普通发布跳过，force 覆盖
  await fs.writeFile(path.join(FAKE_UNITS, '__selftest__.png'), Buffer.from('tampered-by-external'));
  const prev = await forge.publishPreview(FAKE_ROOT, [WS_KEY]);
  report('T6-5 外部修改预检', prev[0].status === 'conflict' && prev[0].externalModified === true);
  const pub2 = await forge.publish(FAKE_ROOT, [WS_KEY]);
  report('T6-6 冲突默认跳过', pub2[0].status === 'conflict', JSON.stringify(pub2[0].status));
  const pub3 = await forge.publish(FAKE_ROOT, [WS_KEY], true);
  const t3 = await fs.readFile(path.join(FAKE_UNITS, '__selftest__.png'));
  report('T6-7 强制覆盖', pub3[0].status === 'published' && sha(t3) === sha(wsPng));

  // 6) 新文件发布（目标不存在）
  const fakeNew = 'units/__selftest_new__.png';
  await fs.copyFile(path.join(forge.WORKSPACE, WS_KEY), path.join(forge.WORKSPACE, fakeNew));
  const manifest = await forge.loadManifest();
  manifest[fakeNew] = { ...manifest[WS_KEY], source: 'Assets/_Game/Art/Units/__selftest_new__.png', alias: '新文件自测' };
  await forge.saveManifest(manifest);
  const pub4 = await forge.publish(FAKE_ROOT, [fakeNew]);
  report('T6-8 新文件发布', pub4[0].status === 'newfile');
} catch (e) {
  report('T6 异常中断', false, e.message);
} finally {
  // 清理：台账自测条目 + workspace 自测文件 + 假根
  const manifest = await forge.loadManifest().catch(() => ({}));
  delete manifest[WS_KEY];
  delete manifest['units/__selftest_new__.png'];
  await forge.saveManifest(manifest);
  await fs.rm(path.join(forge.WORKSPACE, 'units/__selftest__.png'), { force: true });
  await fs.rm(path.join(forge.WORKSPACE, 'units/__selftest__.grid.txt'), { force: true });
  await fs.rm(path.join(forge.WORKSPACE, 'units/__selftest_new__.png'), { force: true });
  await fs.rm(path.join(forge.WORKSPACE, 'units/__selftest_new__.grid.txt'), { force: true });
  await fs.rm(FAKE_ROOT, { recursive: true, force: true });
}

console.log(results.join('\n'));
console.log(`\n[test-publish] ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
