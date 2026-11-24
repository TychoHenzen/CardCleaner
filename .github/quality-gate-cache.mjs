// The pinned dod-guard clone that the local quality gate runs from.
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";

export const SCRIPTS =
  "packages/quality-guard/skills/quality-refactor/scripts";

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

// The clone is checked out at the pin and must stay unchanged, because CI runs
// the same commit as it is. A hand-edited cache fails the run by name.
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
  const changed = git(cache, ["status", "--porcelain"]).trim();
  if (changed !== "") {
    fail(`${cache} has local changes; remove it and rerun: ${changed}`);
  }
  return cache;
}
