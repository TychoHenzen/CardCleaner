// Runs sync.mjs against a throwaway fork and a throwaway copy of this repository, so it needs no network and no clone
// of the real fork. Usage: node tools/asset-manager/sync.test.mjs
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, sep } from "node:path";
import { fileURLToPath } from "node:url";

const syncScript = join(dirname(fileURLToPath(import.meta.url)), "sync.mjs");
const ADDON = "addons/asset_manager";

function git(cwd, ...args) {
  const run = spawnSync("git", ["-C", cwd, "-c", "user.name=sync-test", "-c", "user.email=sync-test@example.invalid",
    "-c", "core.autocrlf=false", ...args], { encoding: "utf8", windowsHide: true });
  assert.equal(run.status, 0, `git ${args.join(" ")} failed: ${run.stderr}`);
  return run.stdout.trim();
}

function write(root, path, text) {
  mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, path), text);
}

/** Commits the fork's files and points its origin/main at the commit, as a fetch of the real fork would. */
function forkCommit(fork, message) {
  git(fork, "add", "-A");
  git(fork, "commit", "-q", "-m", message);
  git(fork, "update-ref", "refs/remotes/origin/main", "HEAD");
  return git(fork, "rev-parse", "HEAD");
}

function sync(project, fork) {
  const run = spawnSync(process.execPath, [join(project, "tools/asset-manager/sync.mjs"), `--source=${fork}`],
    { encoding: "utf8", windowsHide: true });
  return { status: run.status, output: run.stdout + run.stderr };
}

const scratch = realpathSync(mkdtempSync(join(tmpdir(), "asset-manager-sync-test-")));
try {
  const fork = join(scratch, "fork");
  const project = join(scratch, "project");
  mkdirSync(fork);
  mkdirSync(project);
  git(fork, "init", "-q", "-b", "main");
  git(fork, "remote", "add", "origin", "https://example.invalid/Asset-Manager.git");
  git(project, "init", "-q", "-b", "main");
  write(project, "README.md", "project\n");
  mkdirSync(join(project, "tools/asset-manager"), { recursive: true });
  copyFileSync(syncScript, join(project, "tools/asset-manager/sync.mjs"));
  git(project, "add", "-A");
  git(project, "commit", "-q", "-m", "project");

  // 1. A first sync copies the fork's addon, writes the pin and stages both.
  write(fork, `${ADDON}/plugin.gd`, "extends EditorPlugin\n");
  write(fork, `${ADDON}/old.gd`, "extends Node\n");
  const first = forkCommit(fork, "first");
  let run = sync(project, fork);
  assert.equal(run.status, 0, run.output);
  const pin = JSON.parse(readFileSync(join(project, "tools/asset-manager/pin.json"), "utf8"));
  assert.equal(pin.commit, first);
  assert.deepEqual(Object.keys(pin.files), ["old.gd", "plugin.gd"]);
  git(project, "commit", "-q", "-m", "sync first");

  // 2. A file committed here that the pin lacks stops the sync, and stays tracked and on disk.
  write(project, `${ADDON}/custom.gd`, "extends Node\n");
  git(project, "add", "-A");
  git(project, "commit", "-q", "-m", "local addition");
  run = sync(project, fork);
  assert.notEqual(run.status, 0, "a sync over a committed local addition must fail");
  assert.match(run.output, /addons\/asset_manager\/custom\.gd/);
  assert.ok(existsSync(join(project, `${ADDON}/custom.gd`)));
  assert.equal(git(project, "status", "--porcelain"), "");
  git(project, "rm", "-q", `${ADDON}/custom.gd`);
  git(project, "commit", "-q", "-m", "drop local addition");

  // 3. A committed edit to a pinned file stops the sync too.
  write(project, `${ADDON}/plugin.gd`, "extends EditorPlugin\n# local\n");
  git(project, "commit", "-q", "-am", "local edit");
  run = sync(project, fork);
  assert.notEqual(run.status, 0, "a sync over a committed local edit must fail");
  assert.match(run.output, /addons\/asset_manager\/plugin\.gd/);
  git(project, "revert", "--no-edit", "HEAD");

  // 4. A file the new fork commit drops is removed here and staged as removed.
  rmSync(join(fork, `${ADDON}/old.gd`));
  write(fork, `${ADDON}/new.gd`, "extends Node\n");
  forkCommit(fork, "second");
  run = sync(project, fork);
  assert.equal(run.status, 0, run.output);
  assert.ok(!existsSync(join(project, `${ADDON}/old.gd`)));
  const staged = git(project, "diff", "--cached", "--no-renames", "--name-status").split("\n").sort();
  assert.deepEqual(staged, [`A\t${ADDON}/new.gd`, `D\t${ADDON}/old.gd`, "M\ttools/asset-manager/pin.json"]);
} finally {
  // ASSUMPTION: only the folder mkdtemp made for this run is removed; the check guards against a changed tmpdir.
  if (scratch.startsWith(realpathSync(tmpdir()) + sep) && scratch.includes("asset-manager-sync-test-")) {
    rmSync(scratch, { recursive: true, force: true });
  }
}

console.log("sync.mjs fixture: 4 cases passed");
