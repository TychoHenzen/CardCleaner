// Runs the export check's rules on small fixtures, so it needs no dotnet publish, no Godot and no pck file.
// Usage: node tools/export-check/export-check.test.mjs
import assert from 'node:assert/strict';
import { ALLOWED_ASSEMBLIES, STUB_PATH, checkPck, checkPublish, parsePckTable } from './export-check.mjs';

let cases = 0;
function check(name, run) {
  run();
  cases += 1;
  console.log(`ok - ${name}`);
}

/** One pck file table entry: path length, path, offset, size, md5, flags. */
function entryBytes(entryPath, size) {
  const text = Buffer.from(entryPath, 'utf8');
  const padded = Buffer.concat([text, Buffer.alloc((4 - (text.length % 4)) % 4)]);
  const head = Buffer.alloc(4);
  head.writeUInt32LE(padded.length);
  const numbers = Buffer.alloc(8 + 8 + 16 + 4);
  numbers.writeBigUInt64LE(BigInt(size), 8);
  return Buffer.concat([head, padded, numbers]);
}

function tableBytes(entries) {
  const count = Buffer.alloc(4);
  count.writeUInt32LE(entries.length);
  return Buffer.concat([count, ...entries.map((e) => entryBytes(e.path, e.size))]);
}

const goodFiles = [...ALLOWED_ASSEMBLIES, 'CardCleaner.pdb', 'CardCleaner.deps.json'];
const goodEntries = [
  { path: 'project.binary', size: 300 },
  { path: 'Scripts/Core/Interfaces/ILog.cs', size: 1200 },
  { path: STUB_PATH, size: 13 },
  { path: 'addons/saveable/plugin.cfg', size: 90 },
];

check('a publish folder with the three allowed assemblies and no test marker passes', () => {
  assert.deepEqual(checkPublish({ files: goodFiles, gameDllText: 'CardCleaner.Scripts.Core' }), []);
});

check('a test package assembly, a satellite assembly and a missing Newtonsoft.Json are each named', () => {
  const files = ['CardCleaner.dll', 'GodotSharp.dll', 'FsCheck.dll', 'cs/Microsoft.CodeAnalysis.resources.dll'];
  assert.deepEqual(checkPublish({ files, gameDllText: '' }), [
    'unexpected assembly in the export: FsCheck.dll',
    'unexpected assembly in the export: cs/Microsoft.CodeAnalysis.resources.dll',
    'missing assembly in the export: Newtonsoft.Json.dll',
  ]);
});

check('test code inside CardCleaner.dll is named by its marker', () => {
  const problems = checkPublish({ files: goodFiles, gameDllText: 'x CardCleaner.Tests.Features.Card y GdUnit4Api z' });
  assert.deepEqual(problems, ['CardCleaner.dll contains "CardCleaner.Tests"', 'CardCleaner.dll contains "GdUnit4"']);
});

check('a pck without files of a skipped folder passes', () => {
  assert.deepEqual(checkPck({ entries: goodEntries }), []);
});

check('every skipped folder is named when a file of it ships', () => {
  const entries = [
    ...goodEntries,
    { path: 'Tests/Scenes/Fixtures/RoundTripOverride.tscn', size: 400 },
    { path: 'UnitTests/Foo.md', size: 10 },
    { path: 'addons/gdUnit4/plugin.cfg', size: 90 },
    { path: 'addons/godot_ai/runtime/game_logger.gd', size: 900 },
    { path: 'addons/export_guard/export_policy.gd', size: 500 },
  ];
  assert.deepEqual(checkPck({ entries }), [
    'the pck holds Tests/Scenes/Fixtures/RoundTripOverride.tscn',
    'the pck holds UnitTests/Foo.md',
    'the pck holds addons/gdUnit4/plugin.cfg',
    'the pck holds addons/godot_ai/runtime/game_logger.gd',
    'the pck holds addons/export_guard/export_policy.gd',
  ]);
});

check('a one-byte .cs stand-in in a skipped folder passes, a .cs source there is named', () => {
  const standIn = [...goodEntries, { path: 'Tests/Features/Card/CardSignatureTest.cs', size: 1 }];
  assert.deepEqual(checkPck({ entries: standIn }), []);
  const source = [...goodEntries, { path: 'addons/gdUnit4/src/dotnet/GdUnit4CSharpApi.cs', size: 5200 }];
  assert.deepEqual(checkPck({ entries: source }), [
    'the pck holds the source of addons/gdUnit4/src/dotnet/GdUnit4CSharpApi.cs (5200 bytes)',
  ]);
});

check('a folder whose name only starts like a skipped one is not a problem', () => {
  const entries = [...goodEntries, { path: 'TestData/notes.tres', size: 20 }];
  assert.deepEqual(checkPck({ entries }), []);
});

check('the real helper in the pck, or no helper stand-in at all, is named', () => {
  const real = goodEntries.map((e) => (e.path === STUB_PATH ? { ...e, size: 40000 } : e));
  assert.deepEqual(checkPck({ entries: real }), [`${STUB_PATH} is 40000 bytes: the real helper shipped, not the stub`]);
  const none = goodEntries.filter((e) => e.path !== STUB_PATH);
  assert.deepEqual(checkPck({ entries: none }), [
    `the pck lacks ${STUB_PATH}, so the autoload that project.godot names would fail on every start`,
  ]);
});

check('the file table reader returns every path and size, whatever the padding', () => {
  const entries = [{ path: 'a.gd', size: 1 }, { path: 'dir/b.tscn', size: 22 }, { path: 'c/d/e.cs', size: 0 }];
  assert.deepEqual(parsePckTable(tableBytes(entries)), entries);
});

console.log(`export-check.mjs: ${cases} cases passed`);
