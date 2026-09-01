// PNG 编解码（纯 node，零依赖：zlib + 手写 chunk/滤镜/CRC32）
// 支持解码：颜色类型 6(RGBA)/2(RGB)/3(索引+tRNS)，8 位深度，非隔行
// 编码：固定颜色类型 6、8 位、滤镜 0（None）——对 32×32 像素图足够
import zlib from 'node:zlib';

const SIG = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

const CRC_TABLE = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();

function crc32(buf) {
  let c = -1;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 255] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
}

export function decodePng(buf) {
  if (buf.length < 8 || !SIG.equals(buf.subarray(0, 8))) throw new Error('非 PNG 文件（签名不符）');
  let off = 8;
  let ihdr = null;
  let plte = null;
  let trns = null;
  const idat = [];
  while (off + 8 <= buf.length) {
    const len = buf.readUInt32BE(off);
    const type = buf.toString('ascii', off + 4, off + 8);
    const data = buf.subarray(off + 8, off + 8 + len);
    if (type === 'IHDR') ihdr = data;
    else if (type === 'PLTE') plte = data;
    else if (type === 'tRNS') trns = data;
    else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    off += 12 + len;
  }
  if (!ihdr) throw new Error('PNG 缺少 IHDR');
  const width = ihdr.readUInt32BE(0);
  const height = ihdr.readUInt32BE(4);
  const depth = ihdr[8];
  const colorType = ihdr[9];
  const interlace = ihdr[12];
  if (depth !== 8) throw new Error(`仅支持 8 位深度 PNG（当前 ${depth} 位），请用图像软件重导出`);
  if (interlace !== 0) throw new Error('不支持隔行（Adam7）PNG');
  if (![2, 3, 6].includes(colorType)) throw new Error(`不支持的颜色类型 ${colorType}（仅支持 2/3/6）`);
  if (width <= 0 || height <= 0 || width * height > 4194304) throw new Error(`尺寸非法 ${width}x${height}`);

  const channels = colorType === 6 ? 4 : colorType === 2 ? 3 : 1;
  const stride = width * channels;
  const raw = zlib.inflateSync(Buffer.concat(idat));
  if (raw.length < (stride + 1) * height) throw new Error('IDAT 数据不完整');

  const out = Buffer.alloc(stride * height);
  let p = 0;
  for (let y = 0; y < height; y++) {
    const f = raw[p++];
    const rowStart = y * stride;
    for (let i = 0; i < stride; i++) {
      const c = raw[p++];
      const left = i >= channels ? out[rowStart + i - channels] : 0;
      const up = y > 0 ? out[rowStart - stride + i] : 0;
      const ul = y > 0 && i >= channels ? out[rowStart - stride + i - channels] : 0;
      let v;
      switch (f) {
        case 0: v = c; break;
        case 1: v = c + left; break;
        case 2: v = c + up; break;
        case 3: v = c + ((left + up) >> 1); break;
        case 4: v = c + paeth(left, up, ul); break;
        default: throw new Error(`未知滤镜类型 ${f}（第 ${y + 1} 行）`);
      }
      out[rowStart + i] = v & 255;
    }
  }

  const rgba = new Uint8Array(width * height * 4);
  for (let i = 0; i < width * height; i++) {
    const o = i * 4;
    if (colorType === 6) {
      rgba[o] = out[i * 4];
      rgba[o + 1] = out[i * 4 + 1];
      rgba[o + 2] = out[i * 4 + 2];
      rgba[o + 3] = out[i * 4 + 3];
    } else if (colorType === 2) {
      rgba[o] = out[i * 3];
      rgba[o + 1] = out[i * 3 + 1];
      rgba[o + 2] = out[i * 3 + 2];
      rgba[o + 3] = 255;
    } else {
      const idx = out[i];
      if (!plte || idx * 3 + 2 >= plte.length) throw new Error(`调色板索引越界 ${idx}`);
      rgba[o] = plte[idx * 3];
      rgba[o + 1] = plte[idx * 3 + 1];
      rgba[o + 2] = plte[idx * 3 + 2];
      rgba[o + 3] = trns && idx < trns.length ? trns[idx] : 255;
    }
  }
  return { width, height, rgba };
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body), 0);
  return Buffer.concat([len, body, crc]);
}

export function encodePng(rgba, width, height) {
  if (rgba.length !== width * height * 4) throw new Error('RGBA 尺寸不符');
  const stride = width * 4;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0; // 滤镜 0（None）
    raw.set(rgba.subarray(y * stride, (y + 1) * stride), y * (stride + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; // 位深
  ihdr[9] = 6; // RGBA
  ihdr[10] = 0;
  ihdr[11] = 0;
  ihdr[12] = 0;
  const idat = zlib.deflateSync(raw, { level: 9 });
  return Buffer.concat([SIG, chunk('IHDR', ihdr), chunk('IDAT', idat), chunk('IEND', Buffer.alloc(0))]);
}
