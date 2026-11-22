// Runs the e2e session prune against a throwaway reports/e2e tree, so it needs no Godot and no running session.
// Usage: node tools/e2e/e2e.test.mjs
import assert from 'node:assert/strict';
import {
  existsSync, mkdirSync, mkdtempSync, readdirSync, realpathSync, rmSync, symlinkSync, utimesSync, writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join, sep } from 'node:path';
import { pruneSessions } from './e2e.mjs';

/** Makes a session folder holding one file, then dates the folder `hoursAgo` hours back. */
function session(e2eRoot, name, hoursAgo) {
  const dir = join(e2eRoot, name);
  mkdirSync(join(dir, 'shots'), { recursive: true });
  writeFileSync(join(dir, 'shots', 'shot.png'), 'png');
  // Writing into a folder updates its write time, so the date is set after the contents.
  const when = new Date(Date.now() - hoursAgo * 3_600_000);
  utimesSync(dir, when, when);
  return dir;
}

const scratch = realpathSync(mkdtempSync(join(tmpdir(), 'e2e-prune-test-')));
let cases = 0;
try {
  const e2eRoot = join(scratch, 'reports', 'e2e');
  session(e2eRoot, 'bin', 100);
  session(e2eRoot, 'current', 90);
  const running = session(e2eRoot, 'running', 80);
  for (let i = 1; i <= 6; i++) session(e2eRoot, `stopped${i}`, i);
  writeFileSync(join(e2eRoot, 'notes.txt'), 'a file directly below e2e');
  const outside = join(scratch, 'outside');
  mkdirSync(outside);
  writeFileSync(join(outside, 'sentinel.txt'), 'keep');
  // A junction needs no administrator rights on Windows; elsewhere the type is ignored and a plain link is made.
  symlinkSync(outside, join(e2eRoot, 'link'), 'junction');

  const removed = pruneSessions(e2eRoot, 'current', 5, (dir) => dir === running);
  assert.deepEqual(removed.sort(), ['stopped5', 'stopped6'], 'the two oldest stopped sessions go');
  assert.deepEqual(readdirSync(e2eRoot).sort(),
    ['bin', 'current', 'link', 'notes.txt', 'running', 'stopped1', 'stopped2', 'stopped3', 'stopped4'],
    'the launcher, the starting session, a running session, a file, a link and the 4 newest stopped sessions stay');
  assert.ok(existsSync(join(outside, 'sentinel.txt')), 'what the link points at is untouched');
  cases++;

  assert.deepEqual(pruneSessions(e2eRoot, 'current', 5, () => false), ['running'],
    'once it stops, a session is counted with the others');
  cases++;

  const missing = join(scratch, 'missing');
  assert.deepEqual(pruneSessions(missing, 'current', 5, () => false), [], 'no e2e folder, nothing to do');
  cases++;
} finally {
  // ASSUMPTION: only the folder mkdtemp made for this run is removed; the check guards against a changed tmpdir.
  if (scratch.startsWith(realpathSync(tmpdir()) + sep) && scratch.includes('e2e-prune-test-')) {
    rmSync(scratch, { recursive: true, force: true });
  }
}

console.log(`e2e.mjs session prune: ${cases} cases passed`);
