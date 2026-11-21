// Pulls the semantic layer out of page.svg for the machine read-back:
// every element that carries data-part, data-rel, data-leader or
// data-detail-of, with its attributes, title and desc, plus the layout block.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const svg = fs.readFileSync(path.join(root, 'page.svg'), 'utf8');
const out = [];
const re = /<g ([^>]*data-(?:part|rel|leader|detail-of|shows)=[^>]*)>/g;
let m;
while ((m = re.exec(svg))) {
  const attrs = Object.fromEntries([...m[1].matchAll(/([\w-]+)="([^"]*)"/g)].map((a) => [a[1], a[2]]));
  const after = svg.slice(m.index + m[0].length, m.index + m[0].length + 3000);
  const title = /^<title>([^<]*)<\/title>/.exec(after)?.[1];
  const desc = /^<title>[^<]*<\/title><desc>([^<]*)<\/desc>/.exec(after)?.[1];
  out.push({ ...attrs, ...(title ? { title } : {}), ...(title && desc ? { desc } : {}) });
}
const layout = JSON.parse(/<metadata id="layout">([^<]*)<\/metadata>/.exec(svg)[1]);
fs.writeFileSync(path.join(root, 'semantic.json'), JSON.stringify({ layout, elements: out }, null, 1));
console.log(out.length, 'semantic elements');
