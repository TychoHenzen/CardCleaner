// Drawing helpers for the Card Cleaner mess map. Every shape here is a thing
// in the game's own shop: a cardboard box, a shelf, a price tag, a rack.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const sectionsDir = path.join(here, '..', 'sections');

export const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

export function writeSection(name, w, h, inner, extra = '') {
  fs.mkdirSync(sectionsDir, { recursive: true });
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}"${extra}>\n${inner}\n</svg>\n`;
  fs.writeFileSync(path.join(sectionsDir, `${name}.svg`), svg);
}

// Text. Lines is a string or an array; each line is drawn below the last.
export function text(x, y, lines, cls = 'label', opts = {}) {
  const arr = Array.isArray(lines) ? lines : [lines];
  const lh = opts.lh ?? 21;
  const anchor = opts.anchor ? ` text-anchor="${opts.anchor}"` : '';
  return arr.map((l, i) => `<text class="${cls}" x="${x}" y="${y + i * lh}"${anchor}>${esc(l)}</text>`).join('');
}

export function group(attrs, inner) {
  const a = Object.entries(attrs).map(([k, v]) => `${k}="${esc(v)}"`).join(' ');
  return `<g ${a}>${inner}</g>`;
}

// A part of the picture with its semantic binding.
export function part(id, name, scope, mapsTo, desc, inner, kind = 'mess', extra = {}) {
  const more = Object.entries(extra).map(([k, v]) => ` ${k}="${esc(v)}"`).join('');
  return `<g id="${id}" role="group" aria-label="${esc(name)}" data-part="${id}" data-part-id="${id}" ` +
    `data-kind="${kind}" data-scope="${scope}" data-maps-to="${esc(mapsTo)}"${more}>` +
    `<title>${esc(name)}</title><desc>${esc(desc)}</desc>${inner}</g>`;
}

// The manifest is the semantic authority: parts and relationships take their
// name, scope, mapping, description, tag and epistemic status from it.
export const manifest = JSON.parse(fs.readFileSync(path.join(here, '..', 'manifest.json'), 'utf8'));

export function mPart(id, inner) {
  const p = manifest.parts.find((q) => q.id === id);
  if (!p) throw new Error(`no manifest part ${id}`);
  const extra = { 'data-epistemic': p.epistemic, ...(p.tag ? { 'data-tag': p.tag } : {}) };
  return part(id, p.name, p.scope, p.mapsTo.join(' '), p.summary, inner, p.kind, extra);
}

export function mRel(id, inner) {
  const r = manifest.relationships.find((q) => q.id === id);
  if (!r) throw new Error(`no manifest relationship ${id}`);
  return `<g id="rel-${id}" role="group" aria-label="${esc(r.label)}" data-rel="${id}" data-from="${r.from}" ` +
    `data-to="${r.to}" data-kind="${r.kind}" data-epistemic="${r.epistemic}">` +
    `<title>${esc(r.label)}</title><desc>${esc(r.summary)}</desc>${inner}</g>`;
}

// A closed cardboard box seen from slightly above.
export function box(x, y, w, h, opts = {}) {
  const lid = Math.round(h * 0.28);
  const tape = opts.tape === false ? '' :
    `<line class="box-tape" x1="${x + w / 2}" y1="${y + 2}" x2="${x + w / 2}" y2="${y + lid}"/>`;
  const label = opts.label ? text(x + w / 2, y + lid + (h - lid) / 2 + 5, opts.label, opts.labelCls || 'small', { anchor: 'middle' }) : '';
  return `<rect class="box" x="${x}" y="${y + lid}" width="${w}" height="${h - lid}" rx="2"/>` +
    `<rect class="box-top" x="${x}" y="${y}" width="${w}" height="${lid}" rx="2"/>${tape}${label}`;
}

// A heap of boxes stacked in rows, leaning a little as it grows.
export function pile(x, y, cols, rows, bw = 34, bh = 26, seed = 1) {
  let out = '';
  let r = seed;
  const rnd = () => { r = (r * 9301 + 49297) % 233280; return r / 233280; };
  for (let row = 0; row < rows; row++) {
    const n = Math.max(1, cols - Math.floor(row / 2));
    const off = (cols - n) * bw / 2 + (rnd() - 0.5) * 8;
    for (let c = 0; c < n; c++) {
      out += box(Math.round(x + off + c * (bw + 2)), Math.round(y - row * (bh - 4)), bw, bh);
    }
  }
  return out;
}

// A shelf unit seen from above: a long board with stock on it.
export function shelf(x, y, w, h, stock = []) {
  let out = `<rect class="shelf" x="${x}" y="${y}" width="${w}" height="${h}" rx="3"/>` +
    `<rect class="shelf-top" x="${x + 3}" y="${y + 3}" width="${w - 6}" height="${h - 6}" rx="2"/>`;
  stock.forEach((s, i) => {
    const sx = x + 8 + i * ((w - 16) / Math.max(1, stock.length));
    out += `<rect class="${s}" x="${Math.round(sx)}" y="${y + 6}" width="${Math.round((w - 16) / stock.length - 4)}" height="${h - 12}" rx="1.5"/>`;
  });
  return out;
}

// A loose card lying at an angle.
export function card(x, y, angle, cls = 'card', w = 16, h = 22) {
  return `<rect class="${cls}" x="${-w / 2}" y="${-h / 2}" width="${w}" height="${h}" rx="2" ` +
    `transform="translate(${x},${y}) rotate(${angle})"/>`;
}

// The price tag that marks a mess: a yellow paper strip with a hole and a number.
export function tag(x, y, num, id) {
  const idAttr = id ? ` id="${id}"` : '';
  return `<g${idAttr} class="crisp" transform="translate(${x},${y})">` +
    `<path class="tag" d="M0,-17 L34,-17 L46,0 L34,17 L0,17 Z"/>` +
    `<circle class="tag-hole" cx="36" cy="0" r="4"/>` +
    `<text class="tag-num" x="15" y="7" text-anchor="middle">${esc(num)}</text></g>`;
}

// A smaller copy of the price tag, for lists inside the cards.
export function miniTag(x, y, num) {
  return `<g class="crisp" transform="translate(${x},${y}) scale(0.72)">` +
    `<path class="tag" d="M0,-17 L34,-17 L46,0 L34,17 L0,17 Z"/>` +
    `<circle class="tag-hole" cx="36" cy="0" r="4"/>` +
    `<text class="tag-num" x="15" y="7" text-anchor="middle">${esc(num)}</text></g>`;
}

// A rubber stamp: IN GIT or LOCAL ONLY. The word carries the meaning, not the colour.
export function stamp(x, y, word, kind) {
  const w = word.length * 9.5 + 14;
  return `<g class="stamp-${kind}"><rect x="${x}" y="${y - 15}" width="${w}" height="22" rx="3"/>` +
    `<text class="stamp-text" x="${x + w / 2}" y="${y + 1}" text-anchor="middle">${esc(word)}</text></g>`;
}

// A storage rack seen from above: two rails and cross boards.
export function rack(x, y, w, h, n = 4) {
  let out = `<rect class="rack" x="${x}" y="${y}" width="${w}" height="${h}"/>`;
  for (let i = 1; i < n; i++) out += `<line class="rack" x1="${x + (w * i) / n}" y1="${y}" x2="${x + (w * i) / n}" y2="${y + h}"/>`;
  return out;
}

export function web(x, y, r) {
  let out = '';
  for (let a = 0; a <= 90; a += 22.5) {
    const t = (a * Math.PI) / 180;
    out += `<line class="web" x1="${x}" y1="${y}" x2="${x + Math.cos(t) * r}" y2="${y + Math.sin(t) * r}"/>`;
  }
  for (let k = 1; k <= 3; k++) {
    const rr = (r * k) / 3.4;
    out += `<path class="web" d="M${x + rr},${y} Q${x + rr * 0.8},${y + rr * 0.8} ${x},${y + rr}"/>`;
  }
  return out;
}

export function arrow(x1, y1, x2, y2, cls = 'arrow', head = 9) {
  const a = Math.atan2(y2 - y1, x2 - x1);
  const p1 = [x2 - head * Math.cos(a - 0.45), y2 - head * Math.sin(a - 0.45)];
  const p2 = [x2 - head * Math.cos(a + 0.45), y2 - head * Math.sin(a + 0.45)];
  return `<line class="${cls}" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"/>` +
    `<polygon class="arrow-head" points="${x2},${y2} ${p1[0].toFixed(1)},${p1[1].toFixed(1)} ${p2[0].toFixed(1)},${p2[1].toFixed(1)}"/>`;
}

// An index card pinned to the wall: the frame every inset uses.
export function insetFrame(w, h, title, severity) {
  const sevCls = severity.startsWith('high') ? 'sev' : severity.startsWith('med') ? 'sev sev-med'
    : severity.startsWith('prop') ? 'sev sev-prop' : 'sev sev-low';
  return `<rect class="inset" x="2" y="2" width="${w - 4}" height="${h - 4}" rx="10"/>` +
    text(22, 38, title, 'inset-title') +
    text(w - 22, 36, severity.toUpperCase(), sevCls, { anchor: 'end' });
}
