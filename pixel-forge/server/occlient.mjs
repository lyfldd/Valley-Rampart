// occlient.mjs —— opencode serve HTTP 适配（实测 v1.18.19）
// 单图回路：每任务新建 session；消息带 json_schema 结构化输出约束；
// 结果落点：POST /session/:id/message 响应的 info.structured（探针 2026-09-01 验证）
import { CHARS, parseGridText, serializeGridText, paletteEntry } from './gridtext.mjs';

export async function ocHealth(base, timeoutMs = 2500) {
  try {
    const r = await fetch(`${base}/global/health`, { signal: AbortSignal.timeout(timeoutMs) });
    return r.ok ? await r.json() : null;
  } catch {
    return null;
  }
}

function extractJson(text) {
  if (!text) return null;
  const fence = text.match(/```(?:json)?\s*([\s\S]*?)```/);
  if (fence) {
    try {
      return JSON.parse(fence[1]);
    } catch {
      /* 落到裸提取 */
    }
  }
  const s = text.indexOf('{');
  const e = text.lastIndexOf('}');
  if (s >= 0 && e > s) {
    try {
      return JSON.parse(text.slice(s, e + 1));
    } catch {
      return null;
    }
  }
  return null;
}

// AI 单图编辑：gridText 进 → 新 gridText 出（强校验）
export async function aiEditGrid({ base, instruction, gridText }) {
  const parsed = parseGridText(gridText);
  const { width: w, height: h, name } = parsed;

  const sr = await fetch(`${base}/session`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ title: `pixel-forge 编辑 ${name}` }),
  });
  if (!sr.ok) throw new Error(`创建 AI 会话失败 HTTP ${sr.status}`);
  const session = await sr.json();

  const paletteBlock = parsed.palette.map((p) => `${p.char} = ${p.hex}`).join('\n');
  const prompt = [
    '你是像素画编辑引擎。图像用 forge v1 网格文本表示："." 是透明；其余字符是调色板色号。',
    `当前图像 ${name}，尺寸 ${w}x${h}。`,
    '调色板：',
    paletteBlock,
    '网格：',
    parsed.rows.join('\n'),
    '',
    `任务指令：${instruction}`,
    '',
    '要求：',
    '1. 只修改与指令相关的像素；未提及的像素必须保持原字符完全不变',
    `2. 调色板数组第 i 项对应色号字符按顺序取自 "${CHARS}" 的第 i 个字符（如第 0 项="0"）；"." 永远是透明、不出现在数组中；数组前缀应与上面调色板一致，需要新颜色时向后追加（总数≤64）`,
    `3. grid 数组恰好 ${h} 行、每行恰好 ${w} 个字符，字符只能取调色板出现过的或 "."`,
  ].join('\n');

  const schema = {
    type: 'object',
    required: ['palette', 'grid'],
    properties: {
      palette: {
        type: 'array',
        maxItems: 64,
        items: { type: 'string', pattern: '^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$' },
      },
      grid: {
        type: 'array',
        minItems: h,
        maxItems: h,
        items: { type: 'string', pattern: `^[.0-9a-zA-Z#$%&]{${w}}$` },
      },
    },
  };

  const mr = await fetch(`${base}/session/${session.id}/message`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      parts: [{ type: 'text', text: prompt }],
      format: { type: 'json_schema', schema, retryCount: 2 },
    }),
  });
  if (!mr.ok) {
    const t = await mr.text().catch(() => '');
    throw new Error(`AI 消息请求失败 HTTP ${mr.status}：${t.slice(0, 300)}`);
  }
  const msg = await mr.json();
  if (msg.info?.error) throw new Error(`AI 执行出错：${JSON.stringify(msg.info.error).slice(0, 300)}`);

  let data = msg.info?.structured ?? null;
  if (!data) {
    const text = (msg.parts || []).filter((p) => p.type === 'text').map((p) => p.text).join('\n');
    data = extractJson(text);
  }
  if (!data || !Array.isArray(data.palette) || !Array.isArray(data.grid)) {
    throw new Error('AI 未返回合法的结构化输出（palette/grid 缺失）');
  }

  // 重组为完整 grid 文本并做全量复检（行宽/字符/调色板）
  const rebuilt = {
    name,
    width: w,
    height: h,
    palette: data.palette.map((hex, i) => paletteEntry(CHARS[i], hex)),
    rows: data.grid,
  };
  const text = serializeGridText(rebuilt);
  const reparsed = parseGridText(text); // 任一硬性规则违反 → 抛错
  return { gridText: text, sessionId: session.id, paletteCount: reparsed.palette.length };
}
