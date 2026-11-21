// Draws every section, then writes layout.json from frame.mjs.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { CANVAS, SECTIONS } from './frame.mjs';
import './plan.mjs';
import './insets.mjs';
import './leaders.mjs';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const order = ['plan', 'inset-tangle', 'inset-gate', 'inset-cellar', 'inset-dock', 'inset-clipboard', 'inset-binder'];
const layout = {
  title: 'Where the mess is in Card Cleaner',
  date: '2026-11-21',
  version: 'v1',
  source: 'TychoHenzen/cardcleaner @ e458b795 (master), for the repo owner',
  subtitle: 'Our repo drawn as our own card shop: every yellow tag marks a mess, and the numbers match the cards and the cleaning list.',
  look: 'look.css',
  canvas: { w: CANVAS.w, h: CANVAS.h, class: 'none' },
  minGap: 80,
  head: { title: 66, stamp: 20, subtitle: 30 },
  sections: [
    ...order.map((id) => ({ id, file: `sections/${id}.svg`, x: SECTIONS[id].x, y: SECTIONS[id].y })),
    { id: 'leaders', file: 'sections/leaders.svg', x: 0, y: 0, overlay: true },
  ],
};
fs.writeFileSync(path.join(root, 'layout.json'), JSON.stringify(layout, null, 2) + '\n');
console.log('built', layout.sections.length, 'sections');
