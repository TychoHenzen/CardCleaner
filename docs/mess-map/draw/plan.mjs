// The central picture: the repo drawn as the game's own card shop, seen from
// above. Rooms are parts of the repo; each yellow tag marks one mess.
import { writeSection, text, mPart, mRel, box, shelf, tag, rack, web, arrow, esc } from './lib.mjs';
import { SECTIONS, SPOTS } from './frame.mjs';

const { w: W, h: H } = SECTIONS.plan;
const R = {
  lot: { x: 10, y: 40, w: 220, h: 270 },
  back: { x: 260, y: 40, w: 600, h: 290 },
  work: { x: 860, y: 40, w: 460, h: 290 },
  floor: { x: 10, y: 330, w: 1010, h: 730 },
  office: { x: 1020, y: 330, w: 300, h: 370 },
  dock: { x: 1020, y: 700, w: 300, h: 360 },
};
const rect = (r, cls) => `<rect class="${cls}" x="${r.x}" y="${r.y}" width="${r.w}" height="${r.h}"/>`;
const door = (x1, y1, x2, y2, cls = 'door-gap') => `<line class="${cls}" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"/>`;

function street() {
  return `<rect class="kerb" x="0" y="1060" width="${W}" height="34"/>` +
    `<rect class="street" x="0" y="1094" width="${W}" height="${H - 1094}"/>` +
    `<line class="lane" x1="0" y1="1205" x2="${W}" y2="1205"/>`;
}

function building() {
  const f = R.floor;
  let tiles = '';
  for (let x = f.x + 40; x < f.x + f.w; x += 40) tiles += `<line class="tiles" x1="${x}" y1="${f.y}" x2="${x}" y2="${f.y + f.h}"/>`;
  for (let y = f.y + 40; y < f.y + f.h; y += 40) tiles += `<line class="tiles" x1="${f.x}" y1="${y}" x2="${f.x + f.w}" y2="${y}"/>`;
  const fills = rect(R.back, 'backroom') + rect(R.work, 'workshop') + rect(f, 'floor') + tiles +
    rect(R.office, 'office') + rect(R.dock, 'dock');
  const walls = ['back', 'work', 'floor', 'office', 'dock'].map((k) => rect(R[k], 'wall')).join('');
  const doors = door(520, 330, 600, 330) + door(1020, 440, 1020, 520) +
    door(1240, 700, 1310, 700, 'door-gap-dock') + door(440, 1060, 540, 1060) +
    door(1080, 1060, 1300, 1060, 'door-gap-dock') + door(1130, 330, 1210, 330, 'door-gap-office');
  const portal = `<rect class="portal" x="1130" y="323" width="80" height="14" rx="6"/>`;
  const labels =
    text(276, 74, 'BACK ROOM', 'room') + text(276, 94, 'Scripts/Core', 'room-sub') +
    text(876, 74, 'WORKSHOP', 'room') + text(876, 94, 'Deckbuilder + Worldgen', 'room-sub') +
    text(290, 372, 'SALES FLOOR', 'room') + text(290, 392, 'Shop, Card, Hand, Packs, Player', 'room-sub') +
    text(1036, 368, 'BACK OFFICE', 'room') + text(1036, 388, 'the test rig', 'room-sub') +
    text(1036, 738, 'DELIVERY AREA', 'room') + text(1036, 758, 'reports/', 'room-sub');
  return fills + walls + doors + portal + labels;
}

function lot() {
  const r = R.lot;
  const inner = `<rect class="fog-lot" x="${r.x}" y="${r.y}" width="${r.w}" height="${r.h}" rx="6"/>` +
    `<rect class="fence" x="${r.x}" y="${r.y}" width="${r.w}" height="${r.h}" rx="6"/>` +
    `<line class="sign-post" x1="120" y1="92" x2="120" y2="104"/>` +
    `<rect class="sign-board" x="30" y="58" width="180" height="34" rx="3"/>` +
    text(120, 81, 'LOT FOR SALE', 'sign-text', { anchor: 'middle' }) +
    text(26, 132, 'Not on this map:', 'label-b') +
    text(26, 158, ['art (Assets/)', '.godot cache, 297 MB', 'tool caches', 'vendored add-ons'], 'small', { lh: 22 });
  return mPart('expansion-lot', inner);
}

function coreTiles() {
  let boxes = '';
  const cell = 430 / 7;
  [112, 180].forEach((y, row) => {
    for (let i = 0; i < 7; i++) {
      const other = row === 1 && i >= 5;
      boxes += box(Math.round(285 + i * cell + 5), y + 4, Math.round(cell - 10), 40,
        { tape: false, label: other ? 'other' : 'tiles' });
    }
  });
  const inner = rack(285, 112, 430, 48, 7) + rack(285, 180, 430, 48, 7) + boxes +
    tag(276, 272, '4') + text(330, 279, 'Core: 61 of 71 service files load tiles', 'note');
  return mPart('core-tile-code', inner);
}

function tilesToWorkshop() {
  return mRel('tiles-belong-in-workshop',
    text(730, 156, ['used by maps', '+ tile editor'], 'caption', { lh: 20 }) +
    arrow(732, 200, 890, 186, 'arrow', 11));
}

function workshop() {
  let grid = `<rect class="map-sheet" x="895" y="140" width="160" height="76" rx="2"/>`;
  for (let x = 915; x < 1055; x += 20) grid += `<line class="map-line" x1="${x}" y1="140" x2="${x}" y2="216"/>`;
  for (let y = 159; y < 216; y += 19) grid += `<line class="map-line" x1="895" y1="${y}" x2="1055" y2="${y}"/>`;
  const mesh = `<rect class="map-sheet" x="1122" y="140" width="168" height="76" rx="2"/>` +
    `<path class="map-line" d="M1122,170 L1150,150 L1185,162 L1210,140 M1150,150 L1160,188 L1122,200 ` +
    `M1160,188 L1200,196 L1185,162 M1200,196 L1240,170 L1210,140 M1240,170 L1290,158 M1240,170 L1262,216 ` +
    `M1200,196 L1196,216 M1262,216 L1290,196"/>`;
  const inner = `<line class="divider" x1="1090" y1="110" x2="1090" y2="262"/>` +
    text(975, 128, 'Deckbuilder', 'small', { anchor: 'middle' }) +
    text(1205, 128, 'Worldgen', 'small', { anchor: 'middle' }) + grid + mesh +
    text(975, 238, 'square grid', 'label', { anchor: 'middle' }) +
    text(1205, 238, 'irregular mesh', 'label', { anchor: 'middle' }) +
    text(1205, 258, 'quiet since October', 'small', { anchor: 'middle' }) +
    tag(876, 293, '5') + text(930, 300, 'two map makers, two homes: keep both?', 'note');
  return mPart('workshop-pipelines', inner);
}

function cellar() {
  const inner = `<rect class="hatch-door" x="1260" y="54" width="44" height="44" rx="3"/>` +
    `<line class="wall-thin" x1="1260" y1="76" x2="1304" y2="76"/>` +
    `<circle class="tag-hole" cx="1296" cy="64" r="3"/>` +
    tag(1196, 76, '12') + text(1186, 83, 'the cellar', 'note', { anchor: 'end' });
  return mPart('cellar', inner);
}

function legacy() {
  const inner = web(16, 337, 78) +
    `<ellipse class="dust" cx="60" cy="470" rx="58" ry="12"/>` +
    box(22, 392, 54, 40, { tape: false }) +
    `<rect class="screen" x="26" y="440" width="46" height="26" rx="2"/>` +
    `<rect class="sign-board" x="18" y="474" width="62" height="22" rx="2"/>` +
    text(49, 490, 'START', 'sign-text', { anchor: 'middle' }) +
    tag(96, 372, '6') + text(150, 379, 'dusty corner', 'note') +
    text(96, 410, ['old start scene,', 'old-save code,', '2nd order screen'], 'note', { lh: 22 });
  return mPart('legacy-corner', inner);
}

function gateCone() {
  return mRel('gate-cannot-see-shop',
    `<polygon class="camera-cone" points="56,922 170,548 505,792"/>`);
}

function gate() {
  const inner = `<rect class="camera" x="14" y="916" width="34" height="22" rx="4"/>` +
    `<circle class="camera" cx="52" cy="924" r="6"/>` +
    `<circle class="light-ok" cx="22" cy="909" r="5"/>` +
    tag(92, 930, '2') + text(146, 937, 'green, but sees one lump', 'note');
  return mPart('gate-blind-spot', inner);
}

function hand(cx, cy) {
  const cls = ['card-a', 'card', 'card-b', 'card', 'card-a'];
  return cls.map((c, i) => `<rect class="${c}" x="${cx - 11}" y="${cy - 40}" width="22" height="32" rx="2" ` +
    `transform="rotate(${(i - 2) * 16} ${cx} ${cy + 6})"/>`).join('');
}

function tangle() {
  const stops = [[330, 705], [219, 612], [435, 603], [330, 705], [262, 796], [440, 800], [330, 705], [622, 662]];
  const pins = stops.slice(1).map(([x, y]) => `<circle class="arrow-head" cx="${x}" cy="${y}" r="4.5"/>`).join('');
  const objects =
    `<rect class="box-empty" x="190" y="590" width="58" height="42" rx="2"/>` +
    `<path class="box-top" d="M190,590 L178,574 L236,574 L248,590 Z"/>` +
    `<rect class="card" x="420" y="582" width="30" height="42" rx="3"/>` +
    `<line class="arrow" x1="420" y1="603" x2="450" y2="603"/>` +
    `<ellipse class="pages" cx="262" cy="800" rx="34" ry="18"/>` +
    `<rect class="card-b" x="250" y="788" width="16" height="22" rx="2" transform="rotate(-20 258 799)"/>` +
    `<rect class="card" x="264" y="786" width="16" height="22" rx="2" transform="rotate(14 272 797)"/>` +
    `<circle class="trash" cx="440" cy="802" r="20"/><circle class="tag-hole" cx="440" cy="802" r="12"/>` +
    hand(330, 740);
  const labels = text(219, 652, 'open box', 'small', { anchor: 'middle' }) +
    text(435, 644, 'deck', 'small', { anchor: 'middle' }) +
    text(262, 836, 'pile', 'small', { anchor: 'middle' }) +
    text(440, 842, 'trash can', 'small', { anchor: 'middle' }) +
    text(330, 768, 'hand', 'small', { anchor: 'middle' });
  const string = `<path class="string" d="M${stops.map((p) => p.join(',')).join(' L')}"/>`;
  const inner = objects + string + pins + labels +
    tag(150, 505, '1') + text(204, 512, 'one click: 6 folders, ~20 files', 'note-mess');
  return mPart('click-tangle', inner);
}

function shopHeap() {
  const stock = ['box', 'card-a', 'card', 'box', 'card-b', 'card-a', 'box', 'card'];
  const names = ['Components 58', 'Models 87', 'Services 33', 'Ui 19'];
  let inner = tag(620, 432, '3') + text(672, 426, ['Shop: 197 files, shelved', 'by kind, not by topic'], 'note', { lh: 22 });
  names.forEach((n, i) => {
    const y = 480 + i * 80;
    inner += shelf(620, y, 360, 48, stock.slice(i % 3).concat(stock.slice(0, i % 3))) +
      text(800, y + 30, n, 'label-b halo', { anchor: 'middle' });
  });
  return mPart('shop-heap', inner);
}

function binder() {
  const inner = `<rect class="counter" x="600" y="950" width="300" height="45" rx="4"/>` +
    `<rect class="screen" x="850" y="956" width="38" height="24" rx="2"/>` +
    `<rect class="binder" x="632" y="954" width="64" height="38" rx="3"/>` +
    `<rect class="pages" x="694" y="957" width="7" height="32"/>` +
    tag(600, 905, '10') + text(652, 912, 'CLAUDE.md: a 116 KB rule binder', 'note');
  return mPart('rule-binder', inner);
}

function noticeBoard() {
  const pages = [158, 196, 234, 272, 310].map((x, i) =>
    `<rect class="pages" x="${x}" y="${1024 + (i % 2) * 3}" width="30" height="24" rx="1" transform="rotate(${(i - 2) * 4} ${x + 15} 1036)"/>`).join('');
  const inner = `<rect class="sign-board" x="150" y="1044" width="200" height="12"/>` + pages +
    tag(150, 998, '11') + text(204, 1005, 'notice board: old plans', 'note');
  return mPart('stale-docs', inner);
}

function testRig() {
  const inner = `<rect class="machine" x="1034" y="410" width="74" height="60" rx="4"/>` +
    text(1071, 436, 'runner', 'small', { anchor: 'middle' }) + text(1071, 456, '814 lines', 'small', { anchor: 'middle' }) +
    `<rect class="machine" x="1116" y="404" width="188" height="72" rx="4"/>` +
    text(1210, 432, 'its own test', 'small', { anchor: 'middle' }) +
    text(1210, 454, '1,514 lines', 'small', { anchor: 'middle' }) +
    tag(1036, 510, '7') + text(1090, 517, 'its test: 2x its size,', 'note') +
    text(1036, 546, ['33 copied wait helpers,', 'pins with no exit'], 'note', { lh: 23 });
  return mPart('test-rig', inner);
}

function rigToDock() {
  return mRel('rig-fills-dock',
    `<path class="flow" d="M1292,598 L1292,764"/>` +
    `<polygon class="arrow-head" points="1292,772 1285,758 1299,758"/>` +
    text(1166, 628, ['every run', 'adds a folder'], 'caption', { lh: 21 }));
}

function dock() {
  let pileSvg = '';
  const rows = [[1030, 1012, 6], [1034, 972, 6], [1040, 932, 6], [1050, 892, 5], [1070, 856, 4]];
  rows.forEach(([x, y, n]) => { for (let i = 0; i < n; i++) pileSvg += box(x + i * 46, y, 42, 38); });
  const spill = [[1086, 1066], [1134, 1070], [1182, 1064], [1230, 1072], [1112, 1110], [1162, 1116], [1214, 1108],
    [1262, 1114], [1140, 1150], [1196, 1156]];
  spill.forEach(([x, y], i) => { pileSvg += `<g transform="rotate(${(i % 3 - 1) * 9} ${x + 20} ${y + 17})">${box(x, y, 40, 34)}</g>`; });
  const inner = `<rect class="sign-board" x="1032" y="772" width="276" height="32" rx="3"/>` +
    text(1170, 794, 'THE DELIVERY AREA IS FULL', 'sign-text', { anchor: 'middle' }) +
    tag(1036, 830, '9') + text(1090, 837, '2 GB, never cleared', 'note') + pileSvg;
  return mPart('reports-pile', inner);
}

function van() {
  const inner = `<rect class="van" x="390" y="1112" width="150" height="62" rx="5"/>` +
    `<rect class="van-cab" x="540" y="1118" width="48" height="50" rx="8"/>` +
    text(465, 1139, 'CardCleaner.dll', 'label-b', { anchor: 'middle' }) +
    text(465, 1160, '+ the gdUnit tests', 'small', { anchor: 'middle' }) +
    tag(40, 1140, '8') + text(94, 1136, ['tests compile into the game;', 'do they ship? unchecked'], 'note halo', { lh: 23 });
  return mPart('test-build', inner);
}

function lenses() {
  return SPOTS.map((s) => `<circle class="lens" data-lens-for="${s.part}" cx="${s.x}" cy="${s.y}" r="${s.r}"/>`).join('');
}

const inner = street() + building() + lot() + gateCone() + coreTiles() + tilesToWorkshop() + workshop() + cellar() +
  legacy() + gate() + tangle() + shopHeap() + binder() + noticeBoard() + testRig() + rigToDock() + dock() + van() +
  lenses();
writeSection('plan', W, H, `<title>${esc('The repo as our card shop, seen from above')}</title>` + inner);
