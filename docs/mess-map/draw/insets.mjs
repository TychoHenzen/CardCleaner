// The index cards pinned around the floor plan. Each one zooms into one mess,
// except the clipboard, which is the cleaning list.
import { writeSection, text, box, shelf, arrow, insetFrame, miniTag, stamp, esc, manifest } from './lib.mjs';
import { SECTIONS } from './frame.mjs';

const detail = (of, inner) => `<g data-detail-of="${of}">${inner}</g>`;
const size = (id) => [SECTIONS[id].w, SECTIONS[id].h];

// Wrap a sentence into lines of at most n characters.
function wrap(s, n) {
  const out = [];
  let line = '';
  for (const word of s.split(' ')) {
    if (line && (line + ' ' + word).length > n) { out.push(line); line = word; } else line = line ? line + ' ' + word : word;
  }
  if (line) out.push(line);
  return out;
}

function tangle() {
  const [W, H] = size('inset-tangle');
  const crates = [['Player', 1], ['Core', 3], ['Card', 3], ['Hand', 9], ['Shop', 2], ['Stacks', 1]];
  let s = text(22, 70, 'The path of one right-click, from aim to shelf', 'caption');
  const stops = [];
  crates.forEach(([name, n], i) => {
    const x = 22 + i * 94;
    s += text(x + 41, 100, name, 'label-b', { anchor: 'middle' }) + box(x, 108, 82, 82, { tape: false }) +
      text(x + 41, 165, String(n), 'mid-num', { anchor: 'middle' }) +
      text(x + 41, 184, n === 1 ? 'file' : 'files', 'small', { anchor: 'middle' });
    stops.push([x + 41, 120]);
  });
  s += `<path class="string" d="M${stops.map((p) => p.join(',')).join(' L')}"/>` +
    stops.map(([x, y]) => `<circle class="arrow-head" cx="${x}" cy="${y}" r="4"/>`).join('');
  s += text(22, 222, 'about 20 files take part in one click, in this order', 'label');
  s += text(22, 262, 'Three ways one item moves', 'label-b');
  // The hand on the left, a shelf level on the right, three different lanes between.
  const fan = ['card-a', 'card', 'card-b', 'card'].map((c, i) =>
    `<rect class="${c}" x="47" y="352" width="22" height="32" rx="2" transform="rotate(${(i - 1.5) * 16} 58 396)"/>`).join('');
  s += fan + text(58, 432, 'hand', 'small', { anchor: 'middle' });
  s += shelf(500, 362, 78, 50, ['card-a', 'box', 'card']) + text(539, 432, 'shelf', 'small', { anchor: 'middle' });
  const lanes = [
    [318, 'arrow', 'cards, boosters, booster boxes: as records', 'ItemTransfer'],
    [388, 'string-soft', 'a cardboard box: carried as a body', 'BoxBodyOnLevel'],
    [458, 'flow', 'an open box in hand: the box is the source', 'DecideFromHeldBox'],
  ];
  lanes.forEach(([y, cls, label, file]) => {
    s += arrow(105, y, 492, y, cls, 11) + text(120, y - 10, label, 'label') + text(120, y + 20, file, 'mono');
  });
  s += text(22, 522, ['"A box is no record" is written out in 5 files.',
    '3 click targets stub CanInteract => false; a type',
    'switch over 5 concrete classes decides for them.'], 'label', { lh: 24 });
  s += text(22, 616, 'Idea (proposed): one move rule for every item.', 'note');
  s += text(22, 655, 'see HandClickPlans, IHand, InteractionSystem', 'mono');
  writeSection('inset-tangle', W, H, insetFrame(W, H, 'One click, six folders, three rules', 'high') +
    detail('click-tangle', s));
}

function gate() {
  const [W, H] = size('inset-gate');
  let s = text(22, 70, 'How the dependency gate sorts the 792 classes in Scripts/', 'caption');
  const segs = [[120.1, 'bar-quiet'], [48.4, 'bar-contract'], [218.3, 'bar-game'], [169.2, 'bar-mess']];
  let x = 22;
  const mid = [];
  segs.forEach(([w, cls]) => {
    s += `<rect class="${cls}" x="${x.toFixed(1)}" y="112" width="${w}" height="38"/>`;
    mid.push(x + w / 2);
    x += w;
  });
  s += text(mid[3], 137, 'one module', 'label-w', { anchor: 'middle' }) +
    text(mid[0], 172, 'data 171', 'small', { anchor: 'middle' }) +
    text(mid[1], 104, 'contracts 69', 'small', { anchor: 'middle' }) +
    text(mid[2], 172, 'middle 311', 'small', { anchor: 'middle' }) +
    text(mid[3], 172, 'UI 241', 'small', { anchor: 'middle' });
  s += text(22, 206, ['Every Godot node class is in UI, as one module,',
    'so no loop inside the shop half can ever show up.'], 'label', { lh: 23 });
  s += text(22, 266, 'The four gated numbers, all at their floor:', 'label-b');
  [['up edges', '0'], ['biggest loop', '1'], ['leaky seams', '0'], ['refs to UI', '0']].forEach(([name, n], i) => {
    const dx = 22 + i * 141;
    s += `<rect class="dial" x="${dx}" y="280" width="133" height="64" rx="6"/>` +
      `<circle class="light-ok" cx="${dx + 118}" cy="294" r="6"/>` +
      text(dx + 12, 300, name, 'small') + text(dx + 12, 334, n, 'mid-num');
  });
  s += text(22, 366, '(a loop of 1 class means no loop)', 'small');
  s += text(22, 400, ['The gate compiles Scripts/ itself: 83 errors on Nov 19',
    '(55 and 76 on Nov 16), ignored. The game builds fine.'], 'label', { lh: 23 });
  s += text(22, 454, ['69 of 92 interfaces have one implementation; many were',
    'added to satisfy the gate (inferred from commit messages).'], 'label', { lh: 23 });
  s += text(22, 516, 'Idea (proposed): fail on errors, look inside UI.', 'note');
  writeSection('inset-gate', W, H, insetFrame(W, H, 'The gate is green but blind', 'high') +
    detail('gate-blind-spot', s));
}

function cellar() {
  const [W, H] = size('inset-cellar');
  const rows = [
    ['.venv-poc', 'a 312 MB Python environment for a January prototype', 'LOCAL ONLY', 'local'],
    ['tools/irregular_grid_poc.py and 11 PNGs', '1,501 lines and 21 MB that nothing references', 'IN GIT', 'git'],
    ['crash dumps and logs at the root', 'nul, hs_err_pid36288.log, grep.exe.stackdump, 5 logs', 'LOCAL ONLY', 'local'],
    ['.quality/', '4.1 MB of scanner output, last written Oct 8', 'LOCAL ONLY', 'local'],
    ['the old IDE test-runner chain', '.runsettings, DotSettings.user, gdunit4_testadapter_v5', 'IN GIT', 'git'],
    ['export_context.ps1', 'a context-export script nothing calls', 'IN GIT', 'git'],
    ['architecture-decisions.json', '345 lines of waivers that nothing reads', 'IN GIT', 'git'],
    ['.gitignore', '3 lines for files that no longer exist', 'IN GIT', 'git'],
  ];
  let s = text(22, 70, 'Delete test: removing any of these changes nothing else.', 'caption');
  rows.forEach(([name, what, word, kind], k) => {
    const y = 104 + k * 52;
    const w = word.length * 9.5 + 14;
    s += box(22, y - 14, 38, 32, { tape: false }) + text(76, y, name, 'label-b') + text(76, y + 20, what, 'small') +
      stamp(Math.round(574 - w), y + 6, word, kind);
  });
  s += text(22, 536, 'Idea (proposed): throw these out first.', 'note');
  writeSection('inset-cellar', W, H, insetFrame(W, H, 'The cellar: leftovers nobody opens', 'medium') +
    detail('cellar', s));
}

function dock() {
  const [W, H] = size('inset-dock');
  let s = `<rect class="sign-board" x="22" y="56" width="556" height="62" rx="4"/>` +
    text(300, 82, ['The delivery area is full. Move delivered', 'items off it first.'], 'label-b', { anchor: 'middle', lh: 24 }) +
    text(300, 144, "the game's own message, and true of reports/", 'caption', { anchor: 'middle' });
  const rows = [
    [382, 48, [[22, 130, 'ci: 262 reports'], [156, 70, 'bisect'], [230, 68, '']]],
    [332, 48, [[34, 96, 'fix437fast'], [134, 90, 't405fast2'], [228, 60, '']]],
    [282, 48, [[50, 88, 'gatediag'], [142, 70, 'split'], [216, 60, '']]],
    [232, 48, [[70, 60, ''], [134, 64, ''], [202, 56, '']]],
    [186, 44, [[100, 56, ''], [160, 56, '']]],
  ];
  rows.forEach(([y, h, boxes]) => boxes.forEach(([x, w, label]) => { s += box(x, y, w, h, label ? { label } : {}); }));
  s += text(320, 212, 'about 2 GB', 'big-num') + text(320, 240, 'in about 750 folders in reports/', 'label') +
    text(320, 262, 'since Oct 3, all ignored by git', 'small') +
    text(320, 322, '262', 'mid-num') + text(374, 320, 'of them in reports/ci,', 'label') +
    text(320, 350, ['about 400 MB, kept', 'because the runner', 'passes -rc 100000'], 'label', { lh: 22 });
  s += text(22, 476, ['Nothing ever removes a folder: the runner deletes only',
    'its old godot logs, and e2e cleans only its own session.'], 'label', { lh: 23 });
  s += text(22, 534, ['Most folders are named after one ticket or one bisect',
    'step and are never opened again (inferred).'], 'label', { lh: 23 });
  s += text(22, 610, ['Idea (proposed): keep the last few runs,', 'prune the rest.'], 'note', { lh: 26 });
  writeSection('inset-dock', W, H, insetFrame(W, H, 'The delivery area is full', 'high') + detail('reports-pile', s));
}

function binder() {
  const [W, H] = size('inset-binder');
  // Left: CLAUDE.md.
  let left = `<rect class="binder" x="24" y="56" width="38" height="50" rx="3"/>` +
    `<rect class="pages" x="60" y="60" width="6" height="42"/>` + miniTag(76, 80, '10') +
    text(116, 76, 'CLAUDE.md', 'label-b') + text(116, 98, '116 KB, 950 lines', 'small');
  const parts = [[350, 'bar-code', 'a tour of the current code, about 70%'], [80, 'bar-test', 'incident history, about 16%'],
    [65, 'bar-game', 'commands and rules, about 13%'], [5, 'bar-quiet', '']];
  let x = 24;
  parts.forEach(([w, cls]) => { left += `<rect class="${cls}" x="${x}" y="138" width="${w}" height="36"/>`; x += w; });
  parts.slice(0, 3).forEach(([, cls, label], i) => {
    left += `<rect class="${cls}" x="24" y="${190 + i * 22}" width="14" height="14"/>` + text(46, 202 + i * 22, label, 'small');
  });
  left += text(24, 280, '80 of 329 commits since Nov 1 had to touch it.', 'label') +
    text(24, 312, 'Already stale:', 'label-b') +
    text(24, 336, ['openspec/specs is called tracked; it is empty',
      "'OverlayUi' and 'GeneratePreview' exist in no code",
      'the plan v4 rule (#239) outlived its issue'], 'small', { lh: 22 }) +
    text(24, 418, 'Idea (proposed): keep the rules, drop the tour.', 'note');
  // Right: docs/ and openspec/.
  let right = miniTag(580, 80, '11') + text(622, 76, 'docs/ and openspec/', 'label-b') +
    text(622, 98, 'old plans on the notice board', 'small');
  const share = 496 * 5136 / 9043;
  right += `<rect class="bar-test" x="580" y="138" width="${share.toFixed(1)}" height="36"/>` +
    `<rect class="bar-quiet" x="${(580 + share).toFixed(1)}" y="138" width="${(496 - share).toFixed(1)}" height="36"/>` +
    text(580, 198, 'October map-generation plans: 5,136 of 9,043 lines', 'small') +
    text(580, 232, '12 to 40 percent of the names in them are gone from the code.', 'label') +
    text(580, 262, 'MODULAR_DEVICE_ARCHITECTURE', 'mono') +
    text(580, 282, 'still marked Design Phase; 85 percent of its names are gone', 'small') +
    text(580, 310, 'WORLDGEN_ARCHITECTURE.md', 'mono') +
    text(580, 330, 'CLAUDE.md points to it; 37 percent of its names are gone', 'small') +
    text(580, 360, ['quality-gates.md says weekly; CI runs daily.',
      'openspec/ is a template and an empty specs folder; plans live in issues.'], 'small', { lh: 22 }) +
    text(580, 418, 'Idea (proposed): retire the old plans.', 'note');
  const divider = `<line class="divider" x1="560" y1="60" x2="560" y2="420"/>`;
  writeSection('inset-binder', W, H, insetFrame(W, H, 'The rule binder and the notice board', 'high') + divider +
    detail('rule-binder', left) + detail('stale-docs', right));
}

function clipboard() {
  const [W, H] = size('inset-clipboard');
  // The items come from manifest.cleanupOrder; each ends with its tag number in brackets.
  const items = (list) => list.map((s) => {
    const m = /^(.*) \((\d+)\)$/.exec(s);
    if (!m) throw new Error(`cleanup item without a tag: ${s}`);
    return [m[2], m[1]];
  });
  const order = manifest.cleanupOrder;
  const cols = [
    ['First: throw out', 'no design needed', items(order.throwOut)],
    ['Then: tidy', 'move code, no new design', items(order.tidy)],
    ['Then: redesign', 'needs a design session', items(order.redesign)],
    ['Owner decides', 'questions only you can answer', items(order.decide)],
  ];
  let s = `<rect class="clip-metal" x="700" y="6" width="120" height="22" rx="6"/>`;
  cols.forEach(([head, sub, items], c) => {
    const x = 30 + c * 370;
    if (c) s += `<line class="divider" x1="${x - 18}" y1="70" x2="${x - 18}" y2="414"/>`;
    s += text(x, 90, head, 'col-head') + text(x, 114, sub, 'caption');
    let y = 150;
    items.forEach(([num, line]) => {
      const lines = wrap(line, 39);
      s += miniTag(x, y - 5, num) + text(x + 44, y, lines, 'label', { lh: 21 });
      y += lines.length * 21 + 14;
    });
  });
  s += text(196, 38, 'each tag number points back to the same tag on the plan', 'caption');
  writeSection('inset-clipboard', W, H, insetFrame(W, H, 'Cleaning order', 'proposed') +
    `<g id="cleaning-order" role="group" aria-label="Proposed cleaning order" data-shows="cleanupOrder" ` +
    `data-epistemic="proposed"><title>Proposed cleaning order</title>${s}</g>`);
}

tangle();
gate();
cellar();
dock();
binder();
clipboard();
export const written = Object.keys(SECTIONS).filter((k) => k.startsWith('inset-'));
void esc;
