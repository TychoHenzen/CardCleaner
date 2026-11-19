// Copies addons/asset_manager from one commit of our fork of the Asset Manager into this repository, stages the copy,
// and records the commit and a hash of every file in pin.json beside this script. Every change to the addon, ours
// included, is made in the fork, and an upstream release is merged into the fork, where git keeps our edits or shows
// the conflict. AssetManagerVendorPinTest fails when the addon here differs from pin.json.
//
// Usage: node tools/asset-manager/sync.mjs [--source=<fork clone>] [--commit=<ref>] [--allow-unmerged]
//   --source          a local clone of the fork (default: ../asset-manager beside this repository)
//   --commit          the commit to copy (default: origin/main of that clone, so fetch it first)
//   --allow-unmerged  accept a commit that origin/main does not contain, to try a fork pull request here
//
// The files come from git objects at the commit, never from the clone's working tree, so whatever branch or local
// edit the clone is on cannot leak in. Only files the old pin or git lists under the addon are ever deleted.
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, rmdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const ADDON = "addons/asset_manager";
const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..");
const pinPath = join(here, "pin.json");

/** SHA-256 of a file, with CRLF read as LF in a file holding no NUL byte. AssetManagerVendorPinTest does the same. */
function contentHash(bytes) {
  let data = bytes;
  if (!bytes.includes(0)) {
    const out = Buffer.allocUnsafe(bytes.length);
    let n = 0;
    for (let i = 0; i < bytes.length; i++) {
      if (bytes[i] === 13 && bytes[i + 1] === 10) continue;
      out[n++] = bytes[i];
    }
    data = out.subarray(0, n);
  }
  return createHash("sha256").update(data).digest("hex");
}

function git(cwd, args, input) {
  const run = spawnSync("git", ["-C", cwd, ...args], { input, maxBuffer: 1 << 30, windowsHide: true });
  if (run.error) throw run.error;
  return run;
}

function gitText(cwd, args) {
  const run = git(cwd, args);
  if (run.status !== 0) fail(`git ${args.join(" ")} failed in ${cwd}: ${run.stderr.toString().trim()}`);
  return run.stdout.toString().trim();
}

function fail(message) {
  console.error(`sync.mjs: ${message}`);
  process.exit(1);
}

function parseArgs(argv) {
  const options = { source: resolve(repoRoot, "..", "asset-manager"), commit: "origin/main", allowUnmerged: false };
  for (const arg of argv) {
    const [key, value] = arg.split(/=(.*)/s, 2);
    if (key === "--source" && value) options.source = resolve(value);
    else if (key === "--commit" && value) options.commit = value;
    else if (key === "--allow-unmerged" && value === undefined) options.allowUnmerged = true;
    else fail(`unknown argument ${arg}`);
  }
  return options;
}

/** The addon's files at the commit: path below the addon folder and blob id. */
function treeAt(source, commit) {
  const listing = git(source, ["ls-tree", "-r", "-z", "--full-tree", commit, "--", ADDON]);
  if (listing.status !== 0) fail(`git ls-tree failed: ${listing.stderr.toString().trim()}`);
  const entries = [];
  for (const record of listing.stdout.toString("utf8").split("\0")) {
    if (!record) continue;
    const [meta, path] = record.split("\t");
    const [mode, type, blob] = meta.split(" ");
    if (type !== "blob" || mode === "120000") fail(`${path} is a ${type} with mode ${mode}; only plain files are copied`);
    entries.push({ path: path.slice(ADDON.length + 1), blob });
  }
  if (entries.length === 0) fail(`${commit} has no ${ADDON} folder`);
  return entries;
}

/** Reads every blob through one git cat-file process. */
function readBlobs(source, blobs) {
  const run = git(source, ["cat-file", "--batch"], blobs.join("\n") + "\n");
  if (run.status !== 0) fail(`git cat-file failed: ${run.stderr.toString().trim()}`);
  const out = run.stdout;
  const contents = [];
  let at = 0;
  for (const blob of blobs) {
    const lineEnd = out.indexOf(10, at);
    const [id, type, size] = out.subarray(at, lineEnd).toString().split(" ");
    if (id !== blob || type !== "blob") fail(`git cat-file returned ${id} ${type} for ${blob}`);
    const start = lineEnd + 1;
    contents.push(out.subarray(start, start + Number(size)));
    at = start + Number(size) + 1;
  }
  return contents;
}

function readPin() {
  if (!existsSync(pinPath)) return null;
  return JSON.parse(readFileSync(pinPath, "utf8"));
}

/** The addon's files that git tracks here, as paths below the addon folder. */
function trackedAddonFiles() {
  return gitText(repoRoot, ["ls-files", "-z", "--", ADDON]).split("\0").filter(Boolean)
    .map((path) => path.slice(ADDON.length + 1));
}

/**
 * Refuses while the addon differs from the pin it was synced to: that difference is an edit that belongs in the fork.
 * A committed file the pin lacks counts too, because the removal pass below deletes every tracked file the new commit
 * lacks.
 */
function refuseLocalEdits(oldPin) {
  const status = gitText(repoRoot, ["status", "--porcelain", "--", ADDON]);
  if (status) fail(`${ADDON} has uncommitted changes. Make them in the fork, then sync:\n${status}`);
  if (!oldPin) return;
  const drifted = Object.entries(oldPin.files).filter(([path, hash]) => {
    const file = join(repoRoot, ADDON, path);
    return !existsSync(file) || contentHash(readFileSync(file)) !== hash;
  }).map(([path]) => path);
  const unpinned = trackedAddonFiles().filter((path) => !(path in oldPin.files));
  const local = [...drifted, ...unpinned].sort();
  if (local.length > 0) {
    fail(`these files differ from the pinned commit ${oldPin.commit} or are not in it. Move them to the fork first:\n`
      + local.map((path) => `  ${ADDON}/${path}`).join("\n"));
  }
}

function removeEmptyParents(file) {
  const top = join(repoRoot, ADDON);
  for (let dir = dirname(file); dir.startsWith(top) && dir !== top; dir = dirname(dir)) {
    if (!existsSync(dir) || readdirSync(dir).length > 0) return;
    rmdirSync(dir);
  }
}

function main() {
  const options = parseArgs(process.argv.slice(2));
  if (!existsSync(join(options.source, ".git"))) fail(`${options.source} is not a git clone of the fork; pass --source`);
  const commit = gitText(options.source, ["rev-parse", "--verify", `${options.commit}^{commit}`]);
  if (!options.allowUnmerged) {
    const merged = git(options.source, ["merge-base", "--is-ancestor", commit, "origin/main"]);
    if (merged.status !== 0) {
      fail(`${commit} is not on origin/main of ${options.source}. Merge the fork pull request and fetch, `
        + "or pass --allow-unmerged to try a branch.");
    }
  }
  const repository = gitText(options.source, ["remote", "get-url", "origin"]).replace(/\.git$/, "");

  const oldPin = readPin();
  refuseLocalEdits(oldPin);

  const entries = treeAt(options.source, commit);
  const contents = readBlobs(options.source, entries.map((entry) => entry.blob));
  const files = {};
  entries.forEach((entry, i) => {
    const target = join(repoRoot, ADDON, entry.path);
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, contents[i]);
    files[entry.path] = contentHash(contents[i]);
  });

  const tracked = trackedAddonFiles();
  const removed = [...new Set([...tracked, ...Object.keys(oldPin?.files ?? {})])].filter((path) => !(path in files));
  for (const path of removed) {
    const file = join(repoRoot, ADDON, path);
    if (existsSync(file)) rmSync(file);
    removeEmptyParents(file);
  }

  const sorted = Object.fromEntries(Object.keys(files).sort().map((path) => [path, files[path]]));
  const pin = { repository, commit, path: ADDON, files: sorted };
  writeFileSync(pinPath, JSON.stringify(pin, null, 2) + "\n");

  // The fork commits its .uid sidecars and this repository ignores *.uid under addons/, so the copy is added with -f.
  const toPath = (path) => `${ADDON}/${path}`;
  if (removed.length > 0) {
    const rm = git(repoRoot, ["rm", "-q", "--cached", "--ignore-unmatch", "--pathspec-from-file=-",
      "--pathspec-file-nul"], removed.map(toPath).join("\0"));
    if (rm.status !== 0) fail(`git rm failed: ${rm.stderr.toString().trim()}`);
  }
  const added = [...Object.keys(sorted).map(toPath), "tools/asset-manager/pin.json"];
  const add = git(repoRoot, ["add", "-f", "--pathspec-from-file=-", "--pathspec-file-nul"], added.join("\0"));
  if (add.status !== 0) fail(`git add failed: ${add.stderr.toString().trim()}`);

  console.log(`Synced ${ADDON} from ${repository} at ${commit}: ${entries.length} files, ${removed.length} removed.`);
  console.log(`Staged. Review it with: git diff --cached --stat -- ${ADDON}`);
}

main();
