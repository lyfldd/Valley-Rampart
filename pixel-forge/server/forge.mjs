// forge.mjs —— workspace 业务层（node 专属）
// 职责：manifest 台账 / 资产导入（sha256 快照）/ 成对保存（grid 先写、PNG 失败回滚）/ 发布（保 .meta 铁律）
import { promises as fs } from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { decodePng, encodePng } from './pngcodec.mjs';
import { parseGridText, serializeGridText, rgbaToGrid, gridToRgba } from './gridtext.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const TOOL_ROOT = path.resolve(__dirname, '..');
export const WORKSPACE = path.join(TOOL_ROOT, 'workspace');
const MANIFEST = path.join(WORKSPACE, 'manifest.json');
const CONFIG = path.join(WORKSPACE, 'config.json');

export const DEFAULT_CONFIG = {
  unityRoot: '../../Valley Rampart', // 相对 pixel-forge/ 的 Unity 工程根
  opencodeBase: 'http://127.0.0.1:4096',
};

export async function loadConfig() {
  try {
    const c = JSON.parse(await fs.readFile(CONFIG, 'utf8'));
    return { ...DEFAULT_CONFIG, ...c };
  } catch {
    return { ...DEFAULT_CONFIG };
  }
}

export async function ensureConfig() {
  try {
    await fs.access(CONFIG);
  } catch {
    await fs.mkdir(WORKSPACE, { recursive: true });
    await fs.writeFile(CONFIG, JSON.stringify(DEFAULT_CONFIG, null, 2) + '\n', 'utf8');
  }
}

export function resolveUnityRoot(cfg) {
  return path.resolve(TOOL_ROOT, cfg.unityRoot);
}

export async function unityRootValid(root) {
  try {
    await fs.access(path.join(root, 'Assets'));
    return true;
  } catch {
    return false;
  }
}

export async function loadManifest() {
  try {
    return JSON.parse(await fs.readFile(MANIFEST, 'utf8'));
  } catch {
    return {};
  }
}

export async function saveManifest(m) {
  await fs.mkdir(WORKSPACE, { recursive: true });
  await fs.writeFile(MANIFEST, JSON.stringify(m, null, 2) + '\n', 'utf8');
}

// workspace 相对路径 → 绝对路径（防逃逸）
export function wsResolve(rel) {
  const abs = path.resolve(WORKSPACE, rel);
  if (abs !== WORKSPACE && !abs.startsWith(WORKSPACE + path.sep)) {
    throw new Error(`路径越界（仅允许 workspace 内）：${rel}`);
  }
  return abs;
}

export function gridPathFor(pngRel) {
  return wsResolve(pngRel).replace(/\.png$/i, '.grid.txt');
}

const sha256 = (buf) => crypto.createHash('sha256').update(buf).digest('hex');

function catFromSource(src) {
  if (/\/Units\//.test(src)) return 'units';
  if (/\/Ground\//.test(src)) return 'ground';
  return 'misc';
}

// 导入单个 Assets 资产：拷贝 PNG + 转 grid + 登记台账（含导入时 checksum 快照）
export async function importAsset(unityRoot, srcRel, alias = '') {
  const target = path.resolve(unityRoot, srcRel);
  const bytes = await fs.readFile(target);
  const { width, height, rgba } = decodePng(bytes);
  let gridText = null;
  let manualOnly = false;
  let note = null;
  try {
    const g = rgbaToGrid(rgba, width, height, path.basename(srcRel, '.png'));
    gridText = serializeGridText(g);
  } catch (e) {
    manualOnly = true;
    note = e.message;
  }
  const cat = catFromSource(srcRel);
  const pngRel = `${cat}/${path.basename(srcRel)}`;
  const manifest = await loadManifest();
  if (manifest[pngRel] && manifest[pngRel].source && manifest[pngRel].source !== srcRel) {
    throw new Error(`工作区同名文件已被其他来源占用：${pngRel}`);
  }
  const pngAbs = wsResolve(pngRel);
  await fs.mkdir(path.dirname(pngAbs), { recursive: true });
  await fs.writeFile(pngAbs, bytes);
  if (gridText) await fs.writeFile(gridPathFor(pngRel), gridText, 'utf8');
  manifest[pngRel] = {
    alias,
    source: srcRel,
    size: `${width}x${height}`,
    paletteCount: gridText ? gridText.split('\n').length - 0 : 0,
    manualOnly,
    importedAt: new Date().toISOString().slice(0, 10),
    importedChecksum: sha256(bytes),
  };
  if (gridText) {
    const m = gridText.match(/colors=(\d+)/);
    manifest[pngRel].paletteCount = m ? +m[1] : 0;
  }
  await saveManifest(manifest);
  return { key: pngRel, entry: manifest[pngRel], note };
}

// 导入目录下全部 PNG（已导入的同 source 跳过）
export async function importDir(unityRoot, dirRel) {
  const absDir = path.resolve(unityRoot, dirRel);
  const files = (await fs.readdir(absDir)).filter((f) => /\.png$/i.test(f)).sort();
  const manifest = await loadManifest();
  const existing = new Set(Object.values(manifest).map((e) => e.source).filter(Boolean));
  const results = [];
  for (const f of files) {
    const srcRel = `${dirRel.replace(/\/+$/, '')}/${f}`;
    if (existing.has(srcRel)) {
      results.push({ file: f, skipped: 'already' });
      continue;
    }
    try {
      const r = await importAsset(unityRoot, srcRel);
      results.push({ file: f, key: r.key, manualOnly: r.entry.manualOnly, note: r.note });
    } catch (e) {
      results.push({ file: f, error: e.message });
    }
  }
  return results;
}

// workspace 内 PNG → 生成 grid（agent 或编辑器触发）
export async function convertToGrid(pngRel) {
  const abs = wsResolve(pngRel);
  const bytes = await fs.readFile(abs);
  const { width, height, rgba } = decodePng(bytes);
  const g = rgbaToGrid(rgba, width, height, path.basename(pngRel, '.png'));
  const text = serializeGridText(g);
  await fs.writeFile(gridPathFor(pngRel), text, 'utf8');
  const manifest = await loadManifest();
  if (manifest[pngRel]) {
    manifest[pngRel].manualOnly = false;
    manifest[pngRel].paletteCount = g.palette.length;
    manifest[pngRel].size = `${width}x${height}`;
    await saveManifest(manifest);
  }
  return { gridText: text, paletteCount: g.palette.length };
}

export async function readGrid(pngRel) {
  return await fs.readFile(gridPathFor(pngRel), 'utf8');
}

export async function hasGrid(pngRel) {
  try {
    await fs.access(gridPathFor(pngRel));
    return true;
  } catch {
    return false;
  }
}

// 成对保存：真源 grid，PNG 为渲染产物。先写 grid，PNG 编码失败则回滚 grid（§5.3）
export async function saveGridPair(pngRel, gridText) {
  const parsed = parseGridText(gridText); // 校验失败 → 抛给上层 400
  const pngAbs = wsResolve(pngRel);
  const gridAbs = gridPathFor(pngRel);
  const oldGrid = await fs.readFile(gridAbs, 'utf8').catch(() => null);
  await fs.writeFile(gridAbs, gridText, 'utf8');
  try {
    const bytes = encodePng(gridToRgba(parsed), parsed.width, parsed.height);
    await fs.writeFile(pngAbs, bytes);
  } catch (e) {
    if (oldGrid !== null) await fs.writeFile(gridAbs, oldGrid, 'utf8');
    else await fs.rm(gridAbs, { force: true });
    throw new Error(`PNG 编码失败，已回滚 grid：${e.message}`);
  }
  const manifest = await loadManifest();
  const entry = manifest[pngRel] || (manifest[pngRel] = { alias: '', source: null, importedAt: new Date().toISOString().slice(0, 10) });
  entry.size = `${parsed.width}x${parsed.height}`;
  entry.paletteCount = parsed.palette.length;
  entry.manualOnly = false;
  await saveManifest(manifest);
  return { ok: true, size: entry.size, paletteCount: entry.paletteCount };
}

// 新建空白文件（misc/，无 Assets 映射）
export async function newFile(name, width, height) {
  const safe = String(name || 'untitled').replace(/[\\/:*?"<>|]/g, '_');
  const manifest = await loadManifest();
  let rel = `misc/${safe}.png`;
  let i = 2;
  while (manifest[rel]) rel = `misc/${safe}${i++}.png`;
  const g = { name: safe, width, height, palette: [], rows: Array(height).fill('.'.repeat(width)) };
  const text = serializeGridText(g);
  await fs.mkdir(path.dirname(wsResolve(rel)), { recursive: true });
  await fs.writeFile(wsResolve(rel), encodePng(gridToRgba(g), width, height));
  await fs.writeFile(gridPathFor(rel), text, 'utf8');
  manifest[rel] = {
    alias: safe,
    source: null,
    size: `${width}x${height}`,
    paletteCount: 0,
    manualOnly: false,
    importedAt: new Date().toISOString().slice(0, 10),
  };
  await saveManifest(manifest);
  return { key: rel };
}

export async function updateAlias(key, alias) {
  const manifest = await loadManifest();
  if (!manifest[key]) throw new Error(`台账无此条目：${key}`);
  manifest[key].alias = String(alias).slice(0, 30);
  await saveManifest(manifest);
  return { ok: true };
}

// 发布预检：目标存在性 + 外部修改检测（对比导入时 checksum）
export async function publishPreview(unityRoot, keys) {
  const manifest = await loadManifest();
  const results = [];
  for (const key of keys) {
    const e = manifest[key];
    if (!e) {
      results.push({ key, status: 'no-entry' });
      continue;
    }
    if (!e.source) {
      results.push({ key, status: 'no-source', note: '无 Assets 映射（新建文件，发布时需先入库或手动拷贝）' });
      continue;
    }
    const target = path.resolve(unityRoot, e.source);
    let exists = false;
    let externalModified = false;
    try {
      const cur = await fs.readFile(target);
      exists = true;
      if (e.importedChecksum && sha256(cur) !== e.importedChecksum) externalModified = true;
    } catch {
      /* 目标不存在 → 新文件 */
    }
    results.push({
      key,
      status: exists ? (externalModified ? 'conflict' : 'ok') : 'new',
      target: e.source,
      externalModified,
    });
  }
  return results;
}

// 发布：拷 workspace PNG → Assets source 路径。
// 铁律（§3.2 步骤4）：只写 PNG 内容，绝不触碰 .meta（GUID 稳定，Unity 引用不断链）
export async function publish(unityRoot, keys, force = false) {
  const manifest = await loadManifest();
  const results = [];
  for (const key of keys) {
    const e = manifest[key];
    if (!e || !e.source) {
      results.push({ key, status: 'no-source' });
      continue;
    }
    const normSource = e.source.replace(/\\/g, '/');
    if (!normSource.startsWith('Assets/') || normSource.includes('..')) {
      results.push({ key, status: 'bad-source' });
      continue;
    }
    const target = path.resolve(unityRoot, normSource);
    let exists = false;
    let external = false;
    try {
      const cur = await fs.readFile(target);
      exists = true;
      if (e.importedChecksum && sha256(cur) !== e.importedChecksum) external = true;
    } catch {
      /* 不存在 */
    }
    if (exists && external && !force) {
      results.push({ key, status: 'conflict', note: 'Assets 文件在导入后被外部修改，未覆盖（可勾选强制）' });
      continue;
    }
    const bytes = await fs.readFile(wsResolve(key));
    await fs.mkdir(path.dirname(target), { recursive: true });
    await fs.writeFile(target, bytes);
    e.importedChecksum = sha256(bytes); // 发布后以工作区版本为基线
    results.push({ key, status: exists ? 'published' : 'newfile' });
  }
  await saveManifest(manifest);
  return results;
}

export async function listFiles() {
  const manifest = await loadManifest();
  const out = [];
  for (const [key, e] of Object.entries(manifest)) {
    out.push({
      key,
      alias: e.alias || '',
      source: e.source || null,
      size: e.size || '',
      paletteCount: e.paletteCount ?? 0,
      manualOnly: !!e.manualOnly,
      importedAt: e.importedAt || '',
      hasGrid: await hasGrid(key),
    });
  }
  out.sort((a, b) => a.key.localeCompare(b.key));
  return out;
}
