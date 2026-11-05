// Local runner for the dod-guard quality gate that the quality-guard CI job runs.
// parseGate reads the pin, root and block level from .github/workflows/ci.yml,
// so this script and the workflow cannot drift apart silently.
import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { pathToFileURL } from "node:url";

const JOB_HEADER = "  quality-guard:";
const JOB_KEY = /^  [a-z][\w-]*:$/;
const STEP_START = /^      - /;
const SHA = /^[0-9a-f]{40}$/;

function qualityGuardJob(ciText) {
  const lines = ciText.split(/\r?\n/);
  const start = lines.indexOf(JOB_HEADER);
  if (start < 0) throw new Error("ci.yml quality-guard job is absent");
  const body = [];
  for (const line of lines.slice(start + 1)) {
    if (JOB_KEY.test(line)) break;
    body.push(line);
  }
  return body;
}

function stepsOf(jobLines) {
  const steps = [];
  for (const line of jobLines) {
    if (line.trim().startsWith("#")) continue;
    if (STEP_START.test(line)) steps.push([line]);
    else if (steps.length > 0) steps[steps.length - 1].push(line);
  }
  return steps.map((step) => step.join("\n"));
}

function fieldOf(text, pattern) {
  const match = text.match(pattern);
  return match ? match[1] : undefined;
}

function missing(key) {
  return new Error(`ci.yml quality-guard job has no ${key}`);
}

export function parseGate(ciText) {
  const steps = stepsOf(qualityGuardJob(ciText));
  const pinStep = steps.find((step) =>
    /^      - name: .*dod-guard quality-guard gate[ \t]*$/m.test(step),
  );
  const runStep = steps.find(
    (step) => /^\s+run:/m.test(step) && step.includes("quality-pr-gate.mjs"),
  );

  const repository = pinStep
    ? fieldOf(pinStep, /^\s+repository:\s*(\S+)[ \t]*$/m)
    : undefined;
  if (!repository) throw missing("repository");

  const ref = pinStep ? fieldOf(pinStep, /^\s+ref:\s*(\S+)[ \t]*$/m) : undefined;
  if (!ref || !SHA.test(ref)) throw missing("ref (40 hex characters)");

  const root = runStep ? fieldOf(runStep, /--root=(\S+)/) : undefined;
  if (!root) throw missing("root");

  const blockOn = runStep ? fieldOf(runStep, /--block-on=(\S+)/) : undefined;
  if (!blockOn) throw missing("block-on");

  return { repository, ref, root, blockOn };
}

export function gateArgv({ base, root, blockOn, report }) {
  return [
    `--base=${base}`,
    `--root=${root}`,
    `--block-on=${blockOn}`,
    `--report=${report}`,
  ];
}

export function assertClean(porcelainText) {
  const lines = porcelainText
    .split(/\r?\n/)
    .filter((line) => line.trim() !== "");
  if (lines.length === 0) return;
  throw new Error(
    `working tree is not clean; commit or revert: ${lines.slice(0, 10).join(", ")}`,
  );
}

const PATCH_FILE = ".github/quality-guard-godot.patch";
const PATCHED_FILE =
  "packages/quality-guard/skills/quality-refactor/scripts/lib/rules-reachability.mjs";

function git(cwd, args) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
}

function fail(message) {
  throw new Error(message);
}

function cacheFor(gate) {
  return join(tmpdir(), "cardcleaner-quality-gate", gate.ref);
}

function ensureCache(repo, gate, cache) {
  if (!existsSync(join(cache, ".git"))) {
    if (existsSync(cache)) {
      fail(
        `${cache} exists without a .git folder; remove it by hand and rerun`,
      );
    }
    mkdirSync(dirname(cache), { recursive: true });
    git(repo, ["clone", `https://github.com/${gate.repository}.git`, cache]);
  }
  git(cache, ["checkout", "--detach", gate.ref]);
  const head = git(cache, ["rev-parse", "HEAD"]).trim();
  if (head !== gate.ref) {
    fail(`${cache} is at ${head}, expected pinned ref ${gate.ref}`);
  }
}

function applyPatch(repo, cache) {
  if (git(cache, ["diff", "--name-only"]).trim() === "") {
    const patch = join(repo, PATCH_FILE);
    git(cache, ["apply", "--check", "--whitespace=error", patch]);
    git(cache, ["apply", "--whitespace=error", patch]);
  }
  const changed = git(cache, ["diff", "--name-only"]).trim();
  if (changed !== PATCHED_FILE) {
    fail(
      `patched cache changed ${JSON.stringify(changed)}, expected ${PATCHED_FILE}`,
    );
  }
}

function main(argv) {
  const repo = git(process.cwd(), ["rev-parse", "--show-toplevel"]).trim();
  assertClean(git(repo, ["status", "--porcelain"]));
  const gate = parseGate(
    readFileSync(join(repo, ".github", "workflows", "ci.yml"), "utf8"),
  );
  const baseArgument = argv.find((arg) => arg.startsWith("--base="));
  const base = baseArgument
    ? baseArgument.slice("--base=".length)
    : git(repo, ["merge-base", "HEAD", "origin/master"]).trim();

  const cache = cacheFor(gate);
  ensureCache(repo, gate, cache);
  applyPatch(repo, cache);

  mkdirSync(join(repo, "reports"), { recursive: true });
  console.log(
    `quality-gate-local: gate ${gate.repository}@${gate.ref.slice(0, 8)} base ${base.slice(0, 8)}`,
  );
  const script = join(
    cache,
    "packages",
    "quality-guard",
    "skills",
    "quality-refactor",
    "scripts",
    "quality-pr-gate.mjs",
  );
  const result = spawnSync(
    process.execPath,
    [
      script,
      ...gateArgv({
        base,
        root: gate.root,
        blockOn: gate.blockOn,
        report: "reports/quality-gate-report.md",
      }),
    ],
    { cwd: repo, stdio: "inherit", windowsHide: true },
  );
  if (result.error) fail(`could not start the gate: ${result.error.message}`);
  return result.status ?? 2;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    console.error(
      `quality-gate-local: ${String(error.message).split("\n")[0]}`,
    );
    process.exitCode = 2;
  }
}
