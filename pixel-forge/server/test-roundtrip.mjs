// test-roundtrip.mjs —— 门禁 T1（往返无损）+ T2（网格解析负例）
// 前提：已跑过 init（workspace/units 有图）
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { decodePng, encodePng } from './pngcodec.mjs';
import { parseGridText, serializeGridText, rgbaToGrid, gridToRgba } from './gridtext.mjs';

const WS = path.resolve(import.meta.dirname, '..', 'workspace');
let pass = 0;
let fail = 0;
const results = [];
function report(name, ok, detail = '') {
  results.push(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ' — ' + detail : ''}`);
  ok ? pass++ : fail++;
}

// ---- T1：units 全量往返（PNG→grid→PNG 视觉等价：alpha 逐位相等 + 不透明像素 RGB 逐位相等；
//      全透明像素的 RGB 归一化为 0——协议 '.' 不携带 RGB，AI 生成图透明区常带杂色，归一化是标准做法）----
const unitsDir = path.join(WS, 'units');
const pngs = (await fs.readdir(unitsDir).catch(() => [])).filter((f) => f.endsWith('.png')).sort();
if (!pngs.length) {
  report('T1 单位图往返', false, 'workspace/units 为空（先跑 node server/init.mjs）');
}
for (const f of pngs) {
  try {
    const bytes = await fs.readFile(path.join(unitsDir, f));
    const { width, height, rgba } = decodePng(bytes);
    const g = rgbaToGrid(rgba, width, height, f.replace(/\.png$/, ''));
    const text = serializeGridText(g);
    const reparsed = parseGridText(text);
    const reRgba = gridToRgba(reparsed);
    const rePng = encodePng(reRgba, width, height);
    const reDecoded = decodePng(rePng);
    let equal = reDecoded.width === width && reDecoded.height === height;
    let bad = '';
    if (equal) {
      for (let i = 0; i < width * height; i++) {
        const o = rgba.subarray(i * 4, i * 4 + 4);
        const n = reDecoded.rgba.subarray(i * 4, i * 4 + 4);
        if (o[3] !== n[3]) { bad = `第 ${i} 像素 alpha ${o[3]}→${n[3]}`; break; }
        if (o[3] > 0 && (o[0] !== n[0] || o[1] !== n[1] || o[2] !== n[2])) {
          bad = `第 ${i} 像素（不透明）RGB ${[o[0], o[1], o[2]]}→${[n[0], n[1], n[2]]}`;
          break;
        }
      }
      equal = !bad;
    }
    if (equal) report(`T1 往返 ${f}`, true, `${width}x${height}，${g.palette.length} 色`);
    else report(`T1 往返 ${f}`, false, bad || '尺寸不符');
  } catch (e) {
    report(`T1 往返 ${f}`, false, e.message);
  }
}

// ---- T1b：ground 图必须正确拒绝（超 64 色 → manualOnly 路由量化管线）----
const groundDir = path.join(WS, 'ground');
const grounds = (await fs.readdir(groundDir).catch(() => [])).filter((f) => f.endsWith('.png')).sort();
for (const f of grounds) {
  try {
    const bytes = await fs.readFile(path.join(groundDir, f));
    const { width, height, rgba } = decodePng(bytes);
    let threw = false;
    let count = 0;
    try {
      rgbaToGrid(rgba, width, height, f);
    } catch (e) {
      threw = e.code === 'too-many-colors';
      count = e.colorCount ?? 0;
    }
    report(`T1b 拒收 ${f}`, threw, threw ? `${count} 色 > 64（正确路由量化管线）` : '未按超色拒绝');
  } catch (e) {
    report(`T1b 拒收 ${f}`, false, e.message);
  }
}

// ---- T2：解析负例四类 ----
const base = (rows, palette = ['0 = #ff0000']) =>
  `# forge v1 | name=t | size=4x2 | colors=${palette.length}\n# palette\n. = transparent\n${palette.join('\n')}\n# grid\n${rows.join('\n')}\n`;

const negatives = [
  {
    name: 'T2a 行宽错误',
    text: base(['....', '...']),
  },
  {
    name: 'T2b 非法字符',
    text: base(['....', '..!`']),
  },
  {
    name: 'T2c 调色板缺色（网格用了未声明的字符）',
    text: `# forge v1 | name=t | size=4x2 | colors=1\n# palette\n. = transparent\n0 = #ff0000\n# grid\n....\n1111\n`.replace('1111', '1112'),
  },
  {
    name: 'T2d 坏头部',
    text: `# forge v2 | name=t | size=4x2 | colors=1\n# palette\n. = transparent\n0 = #ff0000\n# grid\n....\n....\n`,
  },
  {
    name: 'T2e 行数不足',
    text: base(['....']),
  },
  {
    name: 'T2f 调色板字符重复',
    text: `# forge v1 | name=t | size=4x2 | colors=2\n# palette\n. = transparent\n0 = #ff0000\n0 = #00ff00\n# grid\n....\n....\n`,
  },
];
for (const n of negatives) {
  try {
    parseGridText(n.text);
    report(n.name, false, '未拒绝');
  } catch (e) {
    report(n.name, e.name === 'GridError', `正确拒绝：${e.message.slice(0, 60)}`);
  }
}

// ---- T2g：合法样例必须通过 ----
try {
  const ok = parseGridText(base(['....', '0000']));
  report('T2g 合法样例', ok.rows.length === 2 && ok.rows[1] === '0000');
} catch (e) {
  report('T2g 合法样例', false, e.message);
}

console.log(results.join('\n'));
console.log(`\n[test-roundtrip] ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
