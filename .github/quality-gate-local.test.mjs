import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
  assertClean,
  gateArgv,
  parseGate,
} from "./quality-gate-parse.mjs";

const ciPath = fileURLToPath(new URL("./workflows/ci.yml", import.meta.url));
const ciText = readFileSync(ciPath, "utf8");

function refuses(text, pattern) {
  assert.throws(
    () => parseGate(text),
    (error) => error instanceof Error && pattern.test(error.message),
  );
}

// 1. The real workflow yields the pin, the root and the block level.
const gate = parseGate(ciText);
assert.equal(gate.repository, "TychoHenzen/dod-guard");
assert.match(gate.ref, /^[0-9a-f]{40}$/);
assert.equal(gate.root, "Scripts");
assert.equal(gate.blockOn, "high");

// 2. Without the ref line the parser names the missing ref.
refuses(ciText.replace(/^\s*ref:.*$/m, ""), /ref/);

// 3. A file without the quality-guard job is refused by name.
refuses("jobs:\n  build:\n    runs-on: x\n", /quality-guard/);

// 4. A job without the gate step, or without the pin step, is refused.
refuses(ciText.replaceAll("quality-pr-gate.mjs", "other.mjs"), /gate step/);
refuses(
  ciText.replace("dod-guard quality-guard gate", "dod-guard other"),
  /pin step/,
);

// 5. A clean tree passes; untracked and modified paths are named.
assertClean("");
assertClean("\n");
assert.throws(
  () => assertClean(" M Scripts/A.cs\n?? x.tmp\n"),
  (error) =>
    error instanceof Error &&
    error.message.includes("Scripts/A.cs") &&
    error.message.includes("x.tmp"),
);

// 6. The gate arguments keep their order.
assert.deepEqual(
  gateArgv({
    base: "abc",
    root: "Scripts",
    blockOn: "high",
    report: "reports/q.md",
  }),
  ["--base=abc", "--root=Scripts", "--block-on=high", "--report=reports/q.md"],
);

console.log("quality-gate-local ok");
