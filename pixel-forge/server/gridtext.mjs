// forge v1 网格文本协议（3.1.4 §2.1）
// 本模块在 node（server 侧）与浏览器（ESM import）共用，禁用任何 node 专属 API。
// 格式：
//   # forge v1 | name=xxx | size=32x32 | colors=15
//   # palette
//   . = transparent
//   0 = #2a1a3a
//   1 = #4a3a5aa0   ← 8 位 hex 表示半透明（alpha≠255 时）
//   # grid
//   <height 行，每行恰好 width 字符>

export const CHARS = '0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ#$%&';
export const MAX_PALETTE = 64; // 协议上限（字符集 66，留 2 冗余）

const HEAD_RE = /^# forge v(\d+) \| name=(.*) \| size=(\d+)x(\d+) \| colors=(\d+)$/;
const ENTRY_RE = /^([.0-9a-zA-Z#$%&]) = (transparent|#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)$/;

export class GridError extends Error {
  constructor(message, line) {
    super(line ? `第 ${line} 行：${message}` : message);
    this.name = 'GridError';
    this.line = line;
  }
}

export function parseHexColor(hex) {
  const h = hex.slice(1);
  const r = parseInt(h.slice(0, 2), 16);
  const g = parseInt(h.slice(2, 4), 16);
  const b = parseInt(h.slice(4, 6), 16);
  const a = h.length === 8 ? parseInt(h.slice(6, 8), 16) : 255;
  return [r, g, b, a];
}

export function hexColor(rgb, a) {
  const p = (n) => n.toString(16).padStart(2, '0');
  return a === 255 ? `#${p(rgb[0])}${p(rgb[1])}${p(rgb[2])}` : `#${p(rgb[0])}${p(rgb[1])}${p(rgb[2])}${p(a)}`;
}

export function paletteEntry(char, hex) {
  const [r, g, b, a] = parseHexColor(hex);
  return { char, hex: hexColor([r, g, b], a), rgb: [r, g, b], a };
}

export function parseGridText(text) {
  if (typeof text !== 'string' || !text.trim()) throw new GridError('空文件', 1);
  const lines = text.split('\n').map((l) => l.replace(/\r$/, ''));
  while (lines.length && lines[lines.length - 1].trim() === '') lines.pop();

  const head = lines[0].match(HEAD_RE);
  if (!head) throw new GridError('头部格式不符（应为 "# forge v1 | name=… | size=WxH | colors=N"）', 1);
  const version = +head[1];
  const name = head[2].trim();
  const width = +head[3];
  const height = +head[4];
  const colors = +head[5];
  if (version !== 1) throw new GridError(`不支持的协议版本 v${version}`, 1);
  if (!(Number.isInteger(width) && width > 0 && Number.isInteger(height) && height > 0)) {
    throw new GridError(`尺寸非法 ${width}x${height}`, 1);
  }

  let i = 1;
  if (lines[i] !== '# palette') throw new GridError('缺少 "# palette" 段', i + 1);
  i++;

  const palette = [];
  const byChar = new Map();
  let hasTransparent = false;
  for (; i < lines.length; i++) {
    const ln = lines[i];
    if (ln === '# grid') break;
    if (ln.trim() === '') continue;
    const em = ln.match(ENTRY_RE);
    if (!em) throw new GridError('调色板条目格式非法（应为 "X = #hex"）', i + 1);
    const ch = em[1];
    const val = em[2];
    if (byChar.has(ch) || (ch === '.' && hasTransparent)) throw new GridError(`调色板字符重复：${ch}`, i + 1);
    if (ch === '.') {
      if (val !== 'transparent') throw new GridError('"." 必须为 transparent', i + 1);
      hasTransparent = true;
      continue;
    }
    byChar.set(ch, palette.length);
    palette.push(paletteEntry(ch, val));
  }
  if (i >= lines.length || lines[i] !== '# grid') throw new GridError('缺少 "# grid" 段', i + 1);
  i++;
  if (colors !== palette.length) {
    throw new GridError(`头部 colors=${colors} 与调色板实际 ${palette.length} 项不符`, 1);
  }

  const rows = [];
  const rowNums = [];
  for (; i < lines.length; i++) {
    const ln = lines[i];
    if (ln.trim() === '') continue;
    if (ln.length !== width) throw new GridError(`网格行宽 ${ln.length} ≠ 声明宽度 ${width}`, i + 1);
    for (const ch of ln) {
      if (ch !== '.' && !byChar.has(ch)) throw new GridError(`未知字符 "${ch}"（不在调色板中）`, i + 1);
    }
    rows.push(ln);
    rowNums.push(i + 1);
  }
  if (rows.length !== height) {
    throw new GridError(`网格行数 ${rows.length} ≠ 声明高度 ${height}（首行在第 ${rowNums[0] ?? '?'} 行）`, rowNums[0] ?? 1);
  }
  if (!hasTransparent) throw new GridError('调色板缺少 ". = transparent" 声明', 2);

  return { version, name, width, height, palette, rows };
}

export function serializeGridText(g) {
  const name = String(g.name ?? 'untitled').replace(/[|\r\n]/g, '_');
  const out = [];
  out.push(`# forge v1 | name=${name} | size=${g.width}x${g.height} | colors=${g.palette.length}`);
  out.push('# palette');
  out.push('. = transparent');
  for (const p of g.palette) out.push(`${p.char} = ${p.hex}`);
  out.push('# grid');
  for (const r of g.rows) out.push(r);
  return out.join('\n') + '\n';
}

// RGBA(Uint8Array, w*h*4) → {palette, rows}；超过 64 色抛错（走量化管线）
export function rgbaToGrid(rgba, width, height, name) {
  const counts = new Map(); // "r,g,b,a" → 次数
  for (let i = 0; i < width * height; i++) {
    const a = rgba[i * 4 + 3];
    if (a === 0) continue; // 全透明统一为 '.'
    const key = `${rgba[i * 4]},${rgba[i * 4 + 1]},${rgba[i * 4 + 2]},${a}`;
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  if (counts.size > MAX_PALETTE) {
    const err = new GridError(`颜色数 ${counts.size} 超过协议上限 ${MAX_PALETTE}（需量化降色管线）`);
    err.code = 'too-many-colors';
    err.colorCount = counts.size;
    throw err;
  }
  const sorted = [...counts.entries()].sort((x, y) => y[1] - x[1]);
  const palette = sorted.map(([key], idx) => {
    const [r, g, b, a] = key.split(',').map(Number);
    return { char: CHARS[idx], hex: hexColor([r, g, b], a), rgb: [r, g, b], a };
  });
  const byKey = new Map(sorted.map(([key], idx) => [key, CHARS[idx]]));
  const rows = [];
  for (let y = 0; y < height; y++) {
    let row = '';
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      const a = rgba[i + 3];
      if (a === 0) row += '.';
      else row += byKey.get(`${rgba[i]},${rgba[i + 1]},${rgba[i + 2]},${a}`);
    }
    rows.push(row);
  }
  return { name, width, height, palette, rows };
}

export function gridToRgba(parsed) {
  const { width, height, rows, palette } = parsed;
  const byChar = new Map(palette.map((p) => [p.char, p]));
  const rgba = new Uint8Array(width * height * 4);
  for (let y = 0; y < height; y++) {
    const row = rows[y];
    for (let x = 0; x < width; x++) {
      const ch = row[x];
      if (ch === '.') continue;
      const p = byChar.get(ch);
      if (!p) throw new GridError(`网格引用了调色板不存在的字符 "${ch}"（第 ${y + 1} 行）`);
      const i = (y * width + x) * 4;
      rgba[i] = p.rgb[0];
      rgba[i + 1] = p.rgb[1];
      rgba[i + 2] = p.rgb[2];
      rgba[i + 3] = p.a;
    }
  }
  return rgba;
}

// 两个已解析网格的逐格差异（尺寸须一致）
export function diffGrids(a, b) {
  if (a.width !== b.width || a.height !== b.height) throw new GridError('尺寸不一致，无法对比');
  const diffs = [];
  for (let y = 0; y < a.height; y++) {
    for (let x = 0; x < a.width; x++) {
      const ca = a.rows[y][x];
      const cb = b.rows[y][x];
      if (ca !== cb) diffs.push({ x, y, from: ca, to: cb });
    }
  }
  return diffs;
}
