// Pure readers for the local quality gate: the pin, root and block level
// come from .github/workflows/ci.yml, so the local run and the workflow
// cannot drift apart silently.
const JOB_HEADER = "  quality-guard:";
const JOB_KEY = /^  [a-z][\w-]*:$/;
const STEP_START = /^      - /;
const PIN_STEP = /^      - name: .*dod-guard quality-guard gate[ \t]*$/m;
const SHA = /^[0-9a-f]{40}$/;

function qualityGuardJob(ciText) {
  const lines = ciText.split(/\r?\n/);
  const start = lines.indexOf(JOB_HEADER);
  if (start < 0) throw new Error("ci.yml quality-guard job is absent");
  const rest = lines.slice(start + 1);
  const end = rest.findIndex((line) => JOB_KEY.test(line));
  return end < 0 ? rest : rest.slice(0, end);
}

function stepsOf(jobLines) {
  const starts = [];
  const code = jobLines.filter((line) => !line.trim().startsWith("#"));
  code.forEach((line, index) => {
    if (STEP_START.test(line)) starts.push(index);
  });
  return starts.map((from, i) =>
    code.slice(from, starts[i + 1] ?? code.length).join("\n"),
  );
}

function fieldOf(text, pattern, key) {
  const match = text?.match(pattern);
  if (!match) throw new Error(`ci.yml quality-guard job has no ${key}`);
  return match[1];
}

function pinOf(steps) {
  const step = steps.find((candidate) => PIN_STEP.test(candidate));
  if (!step) throw new Error("ci.yml quality-guard job has no pin step");
  const line = (name) => new RegExp(`^\\s+${name}:\\s*(\\S+)[ \\t]*$`, "m");
  const repository = fieldOf(step, line("repository"), "repository");
  const ref = fieldOf(step, line("ref"), "ref");
  if (!SHA.test(ref)) {
    throw new Error("ci.yml quality-guard job has no ref (40 hex characters)");
  }
  return { repository, ref };
}

function runOf(steps) {
  const step = steps.find(
    (candidate) =>
      /^\s+run:/m.test(candidate) && candidate.includes("quality-pr-gate.mjs"),
  );
  if (!step) throw new Error("ci.yml quality-guard job has no gate step");
  return {
    root: fieldOf(step, /--root=(\S+)/, "root"),
    blockOn: fieldOf(step, /--block-on=(\S+)/, "block-on"),
  };
}

export function parseGate(ciText) {
  const steps = stepsOf(qualityGuardJob(ciText));
  return { ...pinOf(steps), ...runOf(steps) };
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
  const shown = lines.slice(0, 10).join(", ");
  throw new Error(`working tree is not clean; commit or revert: ${shown}`);
}
