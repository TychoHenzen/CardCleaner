// The pinned dod-guard clone that the local quality gate runs from.
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";

const PATCH_FILE = ".github/quality-guard-godot.patch";
export const SCRIPTS = "packages/quality-guard/skills/quality-refactor/scripts";
const PATCHED_FILE = `${SCRIPTS}/lib/rules-reachability.mjs`;

export function git(cwd, args) {
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
}

export function fail(message) {
  throw new Error(message);
}

export function ensureCache(repo, gate) {
  const cache = join(tmpdir(), "cardcleaner-quality-gate", gate.ref);
  if (!existsSync(join(cache, ".git"))) {
    if (existsSync(cache)) {
      fail(`${cache} has no .git folder; remove it by hand and rerun`);
    }
    mkdirSync(dirname(cache), { recursive: true });
    git(repo, ["clone", `https://github.com/${gate.repository}.git`, cache]);
  }
  git(cache, ["checkout", "--detach", gate.ref]);
  const head = git(cache, ["rev-parse", "HEAD"]).trim();
  if (head !== gate.ref) fail(`${cache} is at ${head}, expected ${gate.ref}`);
  return cache;
}

// A cache whose patched file is stale or hand-edited is reset to the pin
// (that one file only) and patched again, so it always equals the current
// patch. Any other changed path in the cache fails the run by name.
export function applyPatch(repo, cache) {
  const patch = join(repo, PATCH_FILE);
  git(cache, ["checkout", "--", PATCHED_FILE]);
  git(cache, ["apply", "--check", "--whitespace=error", patch]);
  git(cache, ["apply", "--whitespace=error", patch]);
  const changed = git(cache, ["diff", "--name-only"]).trim();
  if (changed !== PATCHED_FILE) {
    fail(`cache changed ${JSON.stringify(changed)}, expected ${PATCHED_FILE}`);
  }
}
