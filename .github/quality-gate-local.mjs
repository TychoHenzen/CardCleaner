// Local runner for the dod-guard quality gate that the quality-guard CI job
// runs. quality-gate-parse.mjs reads the pin, root and block level from ci.yml.
import { spawnSync } from "node:child_process";
import { mkdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import {
  SCRIPTS,
  applyPatch,
  ensureCache,
  fail,
  git,
} from "./quality-gate-cache.mjs";
import { assertClean, gateArgv, parseGate } from "./quality-gate-parse.mjs";

const REPORT = "reports/quality-gate-report.md";

function baseOf(repo, argv) {
  const argument = argv.find((arg) => arg.startsWith("--base="));
  if (argument) return argument.slice("--base=".length);
  return git(repo, ["merge-base", "HEAD", "origin/master"]).trim();
}

function runGate(run) {
  const { repo, cache, gate, base } = run;
  mkdirSync(join(repo, "reports"), { recursive: true });
  const pin = `${gate.repository}@${gate.ref.slice(0, 8)}`;
  console.log(`quality-gate-local: gate ${pin} base ${base.slice(0, 8)}`);
  const argv = gateArgv({ ...gate, base, report: REPORT });
  const script = join(cache, SCRIPTS, "quality-pr-gate.mjs");
  const result = spawnSync(process.execPath, [script, ...argv], {
    cwd: repo,
    stdio: "inherit",
    windowsHide: true,
  });
  if (result.error) fail(`could not start the gate: ${result.error.message}`);
  return result.status ?? 2;
}

function main(argv) {
  const repo = git(process.cwd(), ["rev-parse", "--show-toplevel"]).trim();
  assertClean(git(repo, ["status", "--porcelain"]));
  const ciPath = join(repo, ".github", "workflows", "ci.yml");
  const gate = parseGate(readFileSync(ciPath, "utf8"));
  const base = baseOf(repo, argv);
  const cache = ensureCache(repo, gate);
  applyPatch(repo, cache);
  return runGate({ repo, cache, gate, base });
}

const entry = process.argv[1];
if (entry && import.meta.url === pathToFileURL(entry).href) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    const first = String(error.message).split("\n")[0];
    console.error(`quality-gate-local: ${first}`);
    process.exitCode = 2;
  }
}
