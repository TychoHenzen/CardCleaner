// Keeps CLAUDE.md small. Every agent session loads it on every call, so it holds only the rules most tasks
// need, and feature notes go to the matching docs/ file (see "Where Notes Go" in CLAUDE.md). It also fails
// when CLAUDE.md names a docs/ file that does not exist, so the index cannot point at nothing.
// Usage: node .github/claude-md-size.mjs [path to CLAUDE.md]
import { existsSync, readFileSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const LIMIT_BYTES = 16 * 1024;

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const claudePath = resolve(process.argv[2] ?? join(repoRoot, "CLAUDE.md"));
const problems = [];

const size = statSync(claudePath).size;
if (size > LIMIT_BYTES) {
  problems.push(
    `CLAUDE.md is ${size} bytes, over its ${LIMIT_BYTES}-byte limit. Move feature notes into the matching ` +
      `docs/ file (see "Where Notes Go" in CLAUDE.md).`,
  );
}

const named = new Set(readFileSync(claudePath, "utf8").match(/docs\/[A-Za-z0-9_./-]+\.md/g) ?? []);
for (const doc of named) {
  if (!existsSync(join(dirname(claudePath), doc))) problems.push(`CLAUDE.md names ${doc}, which does not exist.`);
}

if (problems.length) {
  for (const problem of problems) console.error(problem);
  process.exit(1);
}
console.log(`CLAUDE.md is ${size} bytes (limit ${LIMIT_BYTES}) and every docs/ file it names exists.`);
