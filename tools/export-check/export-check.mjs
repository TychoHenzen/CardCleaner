#!/usr/bin/env node
// What does an exported game carry? Two checks, both headless. See tools/export-check/README.md.
//   node tools/export-check/export-check.mjs publish [--dir=<publish folder>]
//     Publishes CardCleaner.csproj the way Godot's export does (ExportRelease) into a scratch folder, or reads a
//     publish folder that already exists (--dir), and fails when test code or a test package would ship.
//   node tools/export-check/export-check.mjs pck <exported.pck>
//     Reads the file table of an exported pack and fails when a file of a skipped folder would ship.
// Exit codes: 0 nothing wrong, 1 problems found, 2 the check could not run.
import { spawnSync } from 'node:child_process';
import { closeSync, mkdtempSync, openSync, readdirSync, readFileSync, readSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

/** The assemblies an exported game carries: its own, the engine API and the JSON library the save code uses. */
export const ALLOWED_ASSEMBLIES = ['CardCleaner.dll', 'GodotSharp.dll', 'Newtonsoft.Json.dll'];

/** Names that appear inside CardCleaner.dll only when test code or a test package was compiled into it. */
export const TEST_MARKERS = ['CardCleaner.Tests', 'GdUnit4', 'FsCheck', 'NSubstitute', 'Microsoft.CodeAnalysis'];

/** The folders addons/export_guard/export_policy.gd skips, as a pck file table stores them (no res:// prefix). */
export const SKIPPED_FOLDERS = ['Tests/', 'UnitTests/', 'addons/gdUnit4/', 'addons/godot_ai/', 'addons/export_guard/'];

/** The one file of a skipped folder an exported game still holds: the empty node that stands in for the helper autoload. */
export const STUB_PATH = 'addons/godot_ai/runtime/game_helper.gd';
export const STUB_MAX_BYTES = 64;

/** The C# exporter adds a one-byte stand-in for every .cs file, whatever an export plugin skips, so the path of a .cs
 * file in a skipped folder may stay in the pck. Its source may not: the largest stand-in allowed is this many bytes. */
export const CS_STAND_IN_MAX_BYTES = 16;

/** Problems in a publish folder: `files` are its relative paths with `/`, `gameDllText` is CardCleaner.dll as latin1. */
export function checkPublish({ files, gameDllText }) {
  const problems = [];
  for (const file of files.filter((f) => f.toLowerCase().endsWith('.dll')).sort()) {
    if (!ALLOWED_ASSEMBLIES.includes(file)) problems.push(`unexpected assembly in the export: ${file}`);
  }
  for (const name of ALLOWED_ASSEMBLIES) {
    if (!files.includes(name)) problems.push(`missing assembly in the export: ${name}`);
  }
  for (const marker of TEST_MARKERS) {
    if (gameDllText.includes(marker)) problems.push(`CardCleaner.dll contains "${marker}"`);
  }
  return problems;
}

/** Problems in a pck file table: `entries` are { path, size } with the path as stored (no res:// prefix). */
export function checkPck({ entries }) {
  const problems = [];
  for (const { path: entry, size } of entries) {
    if (entry === STUB_PATH) {
      if (size > STUB_MAX_BYTES) problems.push(`${entry} is ${size} bytes: the real helper shipped, not the stub`);
    } else if (SKIPPED_FOLDERS.some((folder) => entry.startsWith(folder))) {
      if (!entry.endsWith('.cs')) problems.push(`the pck holds ${entry}`);
      else if (size > CS_STAND_IN_MAX_BYTES) problems.push(`the pck holds the source of ${entry} (${size} bytes)`);
    }
  }
  if (!entries.some((e) => e.path === STUB_PATH)) {
    problems.push(`the pck lacks ${STUB_PATH}, so the autoload that project.godot names would fail on every start`);
  }
  return problems;
}

/** Reads the file table of a pck (format 4, Godot 4.7) into { path, size } entries. */
export function parsePckTable(table) {
  const count = table.readUInt32LE(0);
  let at = 4;
  const entries = [];
  for (let i = 0; i < count; i++) {
    const length = table.readUInt32LE(at);
    at += 4;
    const entryPath = table.toString('utf8', at, at + length).replace(/\0+$/, '');
    at += length;
    const size = Number(table.readBigUInt64LE(at + 8)); // the offset (8 bytes) comes first, then the size
    at += 8 + 8 + 16 + 4; // offset, size, md5, flags
    entries.push({ path: entryPath, size });
  }
  return entries;
}

export function readPckEntries(file) {
  const fd = openSync(file, 'r');
  try {
    const head = Buffer.alloc(40);
    readSync(fd, head, 0, head.length, 0);
    if (head.toString('latin1', 0, 4) !== 'GDPC') throw new Error(`${file} is not a Godot pck`);
    const format = head.readUInt32LE(4);
    if (format !== 4) throw new Error(`${file} has pck format ${format}; this check reads format 4 (Godot 4.7)`);
    const directoryOffset = Number(head.readBigUInt64LE(32));
    const table = Buffer.alloc(statSync(file).size - directoryOffset);
    readSync(fd, table, 0, table.length, directoryOffset);
    return parsePckTable(table);
  } finally {
    closeSync(fd);
  }
}

function listFiles(dir, prefix = '') {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const relative = prefix + entry.name;
    return entry.isDirectory() ? listFiles(path.join(dir, entry.name), `${relative}/`) : [relative];
  });
}

/** Publishes the game the way Godot's export does. The intermediate folder is redirected so the shared obj folder
 * (and the Debug package set that later gdUnit builds restore from) stays untouched. */
function publishToScratch() {
  const scratch = mkdtempSync(path.join(tmpdir(), 'export-check-'));
  const out = path.join(scratch, 'pub');
  const intermediate = `${path.join(scratch, 'obj').replaceAll('\\', '/')}/`;
  const run = spawnSync('dotnet', [
    'publish', 'CardCleaner.csproj', '-c', 'ExportRelease', '-r', 'win-x64', '--self-contained', 'false',
    '-p:GodotTargetPlatform=windows', '-p:UseAppHost=false', `-p:BaseIntermediateOutputPath=${intermediate}`,
    '--disable-build-servers', '-nologo', '-clp:ErrorsOnly', '-o', out,
  ], { cwd: root, stdio: 'inherit' });
  if (run.status !== 0) throw new Error(`dotnet publish exited with ${run.status}`);
  return out;
}

function report(problems, what) {
  if (problems.length === 0) {
    console.log(`export-check: ok, ${what}`);
    return 0;
  }
  console.error(`export-check: ${problems.length} problem(s), ${what}`);
  for (const problem of problems) console.error(`  - ${problem}`);
  return 1;
}

function main(argv) {
  const [command, ...rest] = argv;
  try {
    if (command === 'publish') {
      const given = rest.find((a) => a.startsWith('--dir='))?.slice('--dir='.length);
      const dir = given ?? publishToScratch();
      const files = listFiles(dir);
      const gameDllText = files.includes('CardCleaner.dll') ? readFileSync(path.join(dir, 'CardCleaner.dll')).toString('latin1') : '';
      return report(checkPublish({ files, gameDllText }), `${files.length} files in ${dir}`);
    }
    if (command === 'pck' && rest[0]) {
      const entries = readPckEntries(rest[0]);
      return report(checkPck({ entries }), `${entries.length} entries in ${rest[0]}`);
    }
    console.error('usage: export-check.mjs publish [--dir=<publish folder>] | export-check.mjs pck <exported.pck>');
    return 2;
  } catch (error) {
    console.error(`export-check: ${error.message}`);
    return 2;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = main(process.argv.slice(2));
}
