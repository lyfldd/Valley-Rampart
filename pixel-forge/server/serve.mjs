// serve.mjs —— 像素工坊本地服务：静态白名单 + workspace API + AI 代理
// 用法：node serve.mjs --port 5173（仅监听 127.0.0.1）
import http from 'node:http';
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as forge from './forge.mjs';
import { ocHealth, aiEditGrid } from './occlient.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const TOOL_ROOT = path.resolve(__dirname, '..');

const args = process.argv.slice(2);
const portIdx = args.indexOf('--port');
const PORT = portIdx >= 0 ? +args[portIdx + 1] || 5173 : 5173;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.txt': 'text/plain; charset=utf-8',
};

// 静态白名单：只服务编辑器与共享协议模块，workspace 数据一律走 API
const STATIC = new Map([
  ['/', 'index.html'],
  ['/index.html', 'index.html'],
  ['/server/gridtext.mjs', 'server/gridtext.mjs'],
]);

function json(res, code, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(code, { 'content-type': 'application/json; charset=utf-8' });
  res.end(body);
}

async function readJsonBody(req, limit = 32 * 1024 * 1024) {
  const chunks = [];
  let size = 0;
  for await (const c of req) {
    size += c.length;
    if (size > limit) throw new Error('请求体过大');
    chunks.push(c);
  }
  if (!chunks.length) return {};
  return JSON.parse(Buffer.concat(chunks).toString('utf8'));
}

function serveStatic(req, res, pathname) {
  const rel = STATIC.get(pathname);
  if (!rel) return false;
  fs.readFile(path.join(TOOL_ROOT, rel))
    .then((bytes) => {
      res.writeHead(200, { 'content-type': MIME[path.extname(rel)] || 'application/octet-stream' });
      res.end(bytes);
    })
    .catch(() => json(res, 500, { error: `静态文件读取失败：${rel}` }));
  return true;
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://127.0.0.1:${PORT}`);
  const pathname = decodeURIComponent(url.pathname);
  try {
    if (serveStatic(req, res, pathname)) return;

    if (pathname === '/api/health' && req.method === 'GET') {
      const cfg = await forge.loadConfig();
      const ur = forge.resolveUnityRoot(cfg);
      const oc = await ocHealth(cfg.opencodeBase);
      return json(res, 200, {
        ok: true,
        toolRoot: TOOL_ROOT,
        workspace: forge.WORKSPACE,
        unityRoot: { path: ur, valid: await forge.unityRootValid(ur) },
        opencode: oc ? { healthy: true, version: oc.version } : { healthy: false },
      });
    }

    if (pathname === '/api/list' && req.method === 'GET') {
      return json(res, 200, { files: await forge.listFiles() });
    }

    if (pathname === '/api/grid' && req.method === 'GET') {
      const p = url.searchParams.get('path');
      if (!p) return json(res, 400, { error: '缺少 path' });
      try {
        const text = await forge.readGrid(p);
        res.writeHead(200, { 'content-type': 'text/plain; charset=utf-8' });
        return res.end(text);
      } catch {
        return json(res, 404, { error: '该图无网格文件（需先转换/导入）' });
      }
    }

    if (pathname === '/api/convert' && req.method === 'POST') {
      const { path: p } = await readJsonBody(req);
      if (!p) return json(res, 400, { error: '缺少 path' });
      const r = await forge.convertToGrid(p);
      return json(res, 200, r);
    }

    if (pathname === '/api/save-grid' && req.method === 'POST') {
      const { path: p, gridText } = await readJsonBody(req);
      if (!p || typeof gridText !== 'string') return json(res, 400, { error: '缺少 path/gridText' });
      const r = await forge.saveGridPair(p, gridText);
      return json(res, 200, r);
    }

    if (pathname === '/api/new-file' && req.method === 'POST') {
      const { name, width, height } = await readJsonBody(req);
      const w = +width;
      const h = +height;
      if (![16, 32, 48, 64].includes(w) || ![16, 32, 48, 64].includes(h)) {
        return json(res, 400, { error: '尺寸仅支持 16/32/48/64' });
      }
      return json(res, 200, await forge.newFile(name, w, h));
    }

    if (pathname === '/api/manifest' && req.method === 'PATCH') {
      const { key, alias } = await readJsonBody(req);
      if (!key) return json(res, 400, { error: '缺少 key' });
      return json(res, 200, await forge.updateAlias(key, alias || ''));
    }

    if (pathname === '/api/import' && req.method === 'POST') {
      const { source } = await readJsonBody(req);
      if (!source || typeof source !== 'string') return json(res, 400, { error: '缺少 source（Assets 相对路径）' });
      const norm = source.replace(/\\/g, '/').trim();
      if (!norm.startsWith('Assets/') || norm.includes('..')) return json(res, 400, { error: '仅允许 Assets/ 下的路径' });
      const cfg = await forge.loadConfig();
      const ur = forge.resolveUnityRoot(cfg);
      if (!(await forge.unityRootValid(ur))) return json(res, 400, { error: `Unity 工程根无效：${ur}（检查 workspace/config.json）` });
      const results = /\.png$/i.test(norm)
        ? [await (async () => {
            try {
              const r = await forge.importAsset(ur, norm);
              return { file: path.basename(norm), key: r.key, manualOnly: r.entry.manualOnly, note: r.note };
            } catch (e) {
              return { file: path.basename(norm), error: e.message };
            }
          })()]
        : await forge.importDir(ur, norm);
      return json(res, 200, { results });
    }

    if (pathname === '/api/ai/edit' && req.method === 'POST') {
      const { instruction, gridText } = await readJsonBody(req);
      if (!instruction || !gridText) return json(res, 400, { error: '缺少 instruction/gridText' });
      const cfg = await forge.loadConfig();
      const oc = await ocHealth(cfg.opencodeBase);
      if (!oc) {
        return json(res, 503, { error: `opencode 不可达（${cfg.opencodeBase}）。请先运行：opencode serve --port 4096` });
      }
      const r = await aiEditGrid({ base: cfg.opencodeBase, instruction, gridText });
      return json(res, 200, r);
    }

    if (pathname === '/api/publish/preview' && req.method === 'POST') {
      const { keys } = await readJsonBody(req);
      if (!Array.isArray(keys)) return json(res, 400, { error: '缺少 keys' });
      const cfg = await forge.loadConfig();
      const ur = forge.resolveUnityRoot(cfg);
      if (!(await forge.unityRootValid(ur))) return json(res, 400, { error: `Unity 工程根无效：${ur}` });
      return json(res, 200, { results: await forge.publishPreview(ur, keys) });
    }

    if (pathname === '/api/publish' && req.method === 'POST') {
      const { keys, force } = await readJsonBody(req);
      if (!Array.isArray(keys)) return json(res, 400, { error: '缺少 keys' });
      const cfg = await forge.loadConfig();
      const ur = forge.resolveUnityRoot(cfg);
      if (!(await forge.unityRootValid(ur))) return json(res, 400, { error: `Unity 工程根无效：${ur}` });
      return json(res, 200, { results: await forge.publish(ur, keys, !!force) });
    }

    json(res, 404, { error: `未找到路由：${req.method} ${pathname}` });
  } catch (e) {
    const code = e.name === 'GridError' ? 400 : 500;
    json(res, code, { error: e.message });
  }
});

// AI 调用耗时较长（LLM + 重试），放宽请求超时
server.requestTimeout = 600000;
server.headersTimeout = 610000;

server.listen(PORT, '127.0.0.1', () => {
  console.log(`[pixel-forge] 本地服务 http://localhost:${PORT}`);
  console.log(`[pixel-forge] workspace = ${forge.WORKSPACE}`);
});
