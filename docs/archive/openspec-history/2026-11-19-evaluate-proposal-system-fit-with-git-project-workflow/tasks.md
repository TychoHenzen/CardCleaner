## 1. Describe how OpenSpec works

- [x] 1.1 Create `evaluation.md` in this change folder with the sections from the design's code change inventory, and write the decision rule from design D4 into it before any map content.
- [x] 1.2 Record what OpenSpec creates and where it keeps state: `openspec/config.yaml`, the change folder files, `.openspec.yaml` flags (`skip_specs`, `retire_capabilities`), `changes/archive/` and `openspec/specs/`. Cite the `openspec --help`, `openspec status --json` and `openspec instructions` output that shows each one.
- [x] 1.3 Record what OpenSpec expects to own: the plan, the task checkboxes that the apply stage reads, the change status, and the living spec merged by archive. Note which of these the proposal app drives stage by stage.

## 2. Describe the GitHub flow

- [x] 2.1 Find the installed dod-guard plugin version under `~/.claude/plugins/` and the source repository HEAD in `~/mcp-servers/dod-guard`. Record both, and list any workflow rule that differs between them (design D2).
- [x] 2.2 For each of the five skills named in cardcleaner's `CLAUDE.md` (`add-backlog-idea`, `refine-backlog-item`, `next-ticket`, `submit-draft-pr`, `complete-pr`), record what it writes and where: issue body sections, sub-issues, step plan comments, Project Status, branch, pull request. Cite the `SKILL.md` lines.
- [x] 2.3 Quote the source-of-truth rules: `standards/project-workflow.md` lines 12-24 and the "Never create a tracked planning file" line of `next-ticket`.
- [x] 2.4 Check one recent closed issue in this repository (`gh issue view`) to confirm the flow records really sit where the skills say. Read only.

## 3. Map the two side by side

- [x] 3.1 Build the map with one row per stage of either system: problem, requirements, design choices, tasks, status, where the plan lives, closing, priority and effort, lasting record of behavior.
- [x] 3.2 Classify every row as overlap, conflict or gap with the rule from design D3, and cite the evidence for both sides of the row.
- [x] 3.3 Compare OpenSpec's living spec record with what cardcleaner's `CLAUDE.md` already records about behavior, and state whether the gap is real or already covered.
- [x] 3.4 Add the two diagrams from `design.md` (Architecture and Sequence) to the report, and adjust them if the evidence changed any row.

## 4. History and recommendation

- [x] 4.1 Find out why dod-guard dropped OpenSpec (design D5): read the diff of commit `51037e82` for `AGENTS.md`, `docs/archive/openspec-history/README.md`, and the commits just before it. State the reason, or state that none was written down.
- [x] 4.2 Say whether anything has changed since 2026-09-03 that would weaken that reason.
- [x] 4.3 Apply the decision rule and write one recommendation: adopt as a supplement (and where it plugs in), adopt with adjustments (and the list), or do not adopt. List any evidence that points against it.
- [x] 4.4 Ask the open question from `design.md` (did the owner miss the living spec record?) in the report, and state that the recommendation defaults to "do not adopt" while it is unanswered.
- [x] 4.5 Add a last section that names the follow-up the verdict implies, without doing it: for "do not adopt", where to copy the report and what to do with the untracked `openspec/` folder; for an adoption, which files a later change would edit.

## 5. Verify the report

- [x] 5.1 Check that every claim in `evaluation.md` cites a file and line, a command and its output, or a commit, and that each cited file and line still says what the report claims.
- [x] 5.2 Run `git status --porcelain` and confirm that nothing outside this change folder changed: no source, test, CI, `CLAUDE.md` or plugin file changed.
- [x] 5.3 Check that the report contains no em-dashes, en-dashes, curly quotes or other non-ASCII punctuation.
