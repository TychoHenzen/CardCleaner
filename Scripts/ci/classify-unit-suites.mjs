// TEMPORARY (feature/test-tiers): runs every UnitTests suite alone in the plain .NET test host and writes
// reports/unit-classify/summary.tsv, so a suite that needs the engine (or crashes the host) is named on its own.
import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';

const root = 'UnitTests';
const out = path.join('reports', 'unit-classify');
fs.mkdirSync(out, { recursive: true });
function walk(dir, acc = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory() && !['bin', 'obj', 'TestResults'].includes(e.name)) walk(p, acc);
    else if (e.name.endsWith('.cs')) acc.push(p);
  }
  return acc;
}
const suites = [];
for (const f of walk(root)) {
  const s = fs.readFileSync(f, 'utf8');
  if (!/^\s*\[TestSuite\]/m.test(s)) continue;
  const ns = (s.match(/^namespace\s+([\w.]+)/m) || [])[1];
  const cls = path.basename(f, '.cs');
  suites.push({ file: f, fqn: `${ns}.${cls}.` });
}
const run = (suite) => new Promise((resolve) => {
  const start = Date.now();
  const args = ['test', 'UnitTests/CardCleaner.UnitTests.csproj', '--no-build', '--filter', `FullyQualifiedName~${suite.fqn}`,
    '--logger', 'console;verbosity=normal', '--blame-crash', '--results-directory', path.join(out, 'results', path.basename(suite.file, '.cs'))];
  const p = spawn('dotnet', args, { windowsHide: true });
  let text = '';
  p.stdout.on('data', (d) => { text += d; });
  p.stderr.on('data', (d) => { text += d; });
  const timer = setTimeout(() => p.kill(), 120000);
  p.on('close', (code) => {
    clearTimeout(timer);
    fs.writeFileSync(path.join(out, path.basename(suite.file, '.cs') + '.log'), text);
    const m = text.match(/(Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)/);
    const crashed = /Test host process crashed|aborted/i.test(text);
    resolve({ ...suite, code, secs: (Date.now() - start) / 1000, failed: m ? +m[2] : -1, passed: m ? +m[3] : -1, crashed });
  });
});
const results = [];
const queue = [...suites];
await Promise.all(Array.from({ length: 4 }, async () => {
  while (queue.length) results.push(await run(queue.shift()));
}));
results.sort((a, b) => a.file.localeCompare(b.file));
const lines = ['verdict\tcode\tpassed\tfailed\tcrashed\tsecs\tfile'];
for (const r of results) {
  const verdict = r.code === 0 && r.failed === 0 && r.passed > 0 ? 'ok' : 'bad';
  lines.push([verdict, r.code, r.passed, r.failed, r.crashed, r.secs.toFixed(1), r.file].join('\t'));
}
fs.writeFileSync(path.join(out, 'summary.tsv'), lines.join('\n') + '\n');
const bad = results.filter((r) => !(r.code === 0 && r.failed === 0 && r.passed > 0));
console.log(`suites=${results.length} ok=${results.length - bad.length} bad=${bad.length}`);
for (const r of bad) console.log(`BAD ${r.file} code=${r.code} passed=${r.passed} failed=${r.failed} crashed=${r.crashed}`);
