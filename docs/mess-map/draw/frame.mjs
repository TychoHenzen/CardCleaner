// One source for where everything sits on the canvas. build.mjs writes
// layout.json from this, and leaders.mjs reads it, so a moved inset moves
// its leader too.
export const CANVAS = { w: 2700, h: 1820 };

export const SECTIONS = {
  plan: { x: 680, y: 0, w: 1340, h: 1300 },
  'inset-tangle': { x: 0, y: 0, w: 600, h: 680 },
  'inset-gate': { x: 0, y: 760, w: 600, h: 540 },
  'inset-cellar': { x: 2100, y: 0, w: 600, h: 560 },
  'inset-dock': { x: 2100, y: 640, w: 600, h: 660 },
  'inset-clipboard': { x: 0, y: 1380, w: 1520, h: 440 },
  'inset-binder': { x: 1600, y: 1380, w: 1100, h: 440 },
};

// Where each inset's lens sits on the floor plan, in plan coordinates, and
// which edge of the inset its leader meets.
export const SPOTS = [
  { id: 'tangle', x: 330, y: 700, r: 170, inset: 'inset-tangle', part: 'click-tangle', edge: 'right' },
  { id: 'gate', x: 40, y: 930, r: 40, inset: 'inset-gate', part: 'gate-blind-spot', edge: 'right' },
  { id: 'cellar', x: 1282, y: 76, r: 38, inset: 'inset-cellar', part: 'cellar', edge: 'left' },
  { id: 'dock', x: 1170, y: 1000, r: 115, inset: 'inset-dock', part: 'reports-pile', edge: 'left' },
  { id: 'binder', x: 664, y: 972, r: 54, inset: 'inset-binder', part: 'rule-binder', edge: 'top' },
];
