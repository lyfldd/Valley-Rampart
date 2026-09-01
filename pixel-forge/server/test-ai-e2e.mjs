// test-ai-e2e.mjs —— 门禁 T3（AI 单图回路端到端，真实 LLM 调用）
// 前提：opencode serve 已运行（默认 127.0.0.1:4096，可用 OC_BASE 覆盖）
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { loadConfig } from './forge.mjs';
import { ocHealth, aiEditGrid } from './occlient.mjs';
import { parseGridText, diffGrids } from './gridtext.mjs';

const base = process.env.OC_BASE;
const cfg = await loadConfig();
const OC = base || cfg.opencodeBase;

const oc = await ocHealth(OC);
if (!oc) {
  console.log(`SKIP test-ai-e2e：opencode 不可达（${OC}）。启动后重跑：opencode serve --port 4096`);
  process.exit(0);
}
console.log(`[test-ai-e2e] opencode ${oc.version} @ ${OC}`);

const unitsDir = path.resolve(import.meta.dirname, '..', 'workspace', 'units');
const grids = (await fs.readdir(unitsDir).catch(() => [])).filter((f) => f.endsWith('.grid.txt')).sort();
if (!grids.length) {
  console.log('SKIP test-ai-e2e：workspace/units 无网格（先跑 node server/init.mjs）');
  process.exit(0);
}
const gridText = await fs.readFile(path.join(unitsDir, grids[0]), 'utf8');
const before = parseGridText(gridText);
console.log(`[test-ai-e2e] 样本 ${grids[0]}（${before.width}x${before.height}，${before.palette.length} 色）`);

const t0 = Date.now();
const envModel = process.env.OC_MODEL; // 可选：providerID/modelID（如 opencode/glm-5-free）
const model = envModel
  ? { providerID: envModel.split('/')[0], modelID: envModel.split('/').slice(1).join('/') }
  : undefined;
if (model) console.log(`[test-ai-e2e] 指定模型 ${model.providerID}/${model.modelID}`);
try {
  const r = await aiEditGrid({
    base: OC,
    instruction: `把第 1 行第 1 列这一个像素改为色号 "0"，除此之外的所有像素必须保持原样不变。`,
    gridText,
    model,
  });
  const after = parseGridText(r.gridText);
  const diffs = diffGrids(before, after);
  const dimsOk = after.width === before.width && after.height === before.height;
  const formatOk = dimsOk && diffs.length >= 1;
  // 默认模型回归保护：必须外科级（1~8 像素）；指定模型只验管线通+格式合法（能力对比看编辑器 diff）
  const surgical = diffs.length >= 1 && diffs.length <= 8;
  const all = model ? formatOk : formatOk && surgical;
  const grade = model ? '管线通/格式合法（指定模型，改动量自对比）' : '回路通/格式合法/外科级改动';
  console.log(`[test-ai-e2e] 耗时 ${((Date.now() - t0) / 1000).toFixed(1)}s，改动 ${diffs.length} 像素：${JSON.stringify(diffs.slice(0, 5))}`);
  console.log(`[test-ai-e2e] 调色板 ${before.palette.length} → ${after.palette.length}，尺寸 ${after.width}x${after.height}`);
  console.log(`\n[test-ai-e2e] ${all ? 'PASS' : 'FAIL'}（${grade}）`);
  process.exitCode = all ? 0 : 1; // 不用 process.exit：避免 Windows libuv keep-alive socket 断言
} catch (e) {
  console.error(`FAIL test-ai-e2e：${e.message}`);
  process.exitCode = 1;
}
