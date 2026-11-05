import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { assertClean, gateArgv, parseGate } from "./quality-gate-local.mjs";

const ciText = readFileSync(
  fileURLToPath(new URL("./workflows/ci.yml", import.meta.url)),
  "utf8",
);

// 1. The real workflow yields the pin, the root and the block level.
const gate = parseGate(ciText);
assert.equal(gate.repository, "TychoHenzen/dod-guard");
assert.match(gate.ref, /^[0-9a-f]{40}$/);
assert.equal(gate.root, "Scripts");
assert.equal(gate.blockOn, "high");

// 2. Without the ref line the parser names the missing ref.
assert.throws(
  () => parseGate(ciText.replace(/^\s*ref:.*$/m, "")),
  (error) => error instanceof Error && /ref/.test(error.message),
);

// 3. A file without the quality-guard job is refused by name.
assert.throws(
  () => parseGate("jobs:\n  build:\n    runs-on: x\n"),
  (error) => error instanceof Error && /quality-guard/.test(error.message),
);

// 4. A clean tree passes; untracked and modified paths are named.
assertClean("");
assertClean("\n");
assert.throws(
  () => assertClean(" M Scripts/A.cs\n?? x.tmp\n"),
  (error) =>
    error instanceof Error &&
    error.message.includes("Scripts/A.cs") &&
    error.message.includes("x.tmp"),
);

// 5. The gate arguments keep their order.
assert.deepEqual(
  gateArgv({ base: "abc", root: "Scripts", blockOn: "high", report: "reports/q.md" }),
  ["--base=abc", "--root=Scripts", "--block-on=high", "--report=reports/q.md"],
);

console.log("quality-gate-local ok");
