// The leaders layer: a dashed red line from each lens on the floor plan to
// the edge of its index card. No text, so it never collides with a label.
import { writeSection } from './lib.mjs';
import { CANVAS, SECTIONS, SPOTS } from './frame.mjs';

const PAD = 40;
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

function endPoint(spot, cx, cy) {
  const b = SECTIONS[spot.inset];
  if (spot.edge === 'right') return [b.x + b.w, clamp(cy, b.y + PAD, b.y + b.h - PAD)];
  if (spot.edge === 'left') return [b.x, clamp(cy, b.y + PAD, b.y + b.h - PAD)];
  if (spot.edge === 'top') return [clamp(cx, b.x + PAD, b.x + b.w - PAD), b.y];
  return [clamp(cx, b.x + PAD, b.x + b.w - PAD), b.y + b.h];
}

const plan = SECTIONS.plan;
const lines = SPOTS.map((spot) => {
  const cx = plan.x + spot.x;
  const cy = plan.y + spot.y;
  const [tx, ty] = endPoint(spot, cx, cy);
  const a = Math.atan2(ty - cy, tx - cx);
  const sx = cx + Math.cos(a) * spot.r;
  const sy = cy + Math.sin(a) * spot.r;
  const f = (v) => v.toFixed(1);
  return `<g data-leader="${spot.id}" data-from="${spot.part}" data-to="${spot.inset}">` +
    `<path class="leader" d="M${f(sx)},${f(sy)} L${f(tx)},${f(ty)}"/>` +
    `<circle class="arrow-head" cx="${f(tx)}" cy="${f(ty)}" r="5"/></g>`;
}).join('');

writeSection('leaders', CANVAS.w, CANVAS.h, lines);
