# Quality Gates

The repository uses a small set of reproducible gates for the C# Godot project. The CI workflow runs on Windows with .NET SDK `8.0.410` and Godot `4.7.1-stable` Mono.

## Applicability

| Language | Format | Lint/static analysis | Type/build | Test | Coverage | Dependency | CodeQL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C# | Skipped: no repository formatter is configured. | `dotnet build CardCleaner.csproj --no-restore --configuration Release /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` runs the enabled .NET analyzers and code-style checks; `actionlint` validates workflow files. | The same analyzer-enabled build is the type/build gate. | `Scripts/ci/Run-GdUnit.ps1` runs the GdUnit4 suite at `res://Tests` with Godot headless. | Skipped: no coverage collector or threshold is configured. | `dotnet list CardCleaner.csproj package --vulnerable --include-transitive` checks the complete NuGet graph after restore; the project relies on `net8.0` framework HTTP APIs rather than a direct `System.Net.Http` package. | The CodeQL workflow initializes, autobuilds, and analyzes C#. |
| GDScript | Skipped: no repository formatter is configured. | Skipped: no standalone GDScript linter is configured; `actionlint` does not inspect GDScript. | Skipped: no independent GDScript type/build gate is configured. | Skipped: the configured GdUnit command targets the repository's `res://Tests` suite rather than a separate GDScript suite. | Skipped: no GDScript coverage collector or threshold is configured. | Not applicable: no GDScript package manifest is used. | Not applicable: CodeQL is configured for C# only. |

## Commands

- Restore before building with `dotnet restore CardCleaner.csproj`.
- Build with analyzers using `dotnet build CardCleaner.csproj --no-restore --configuration Release /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`.
- Run GdUnit locally with `Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path-to-Godot_v4.7.1-stable_mono_win64.exe>` or set `GODOT_BIN`; the runner uses the sibling console executable when available so Windows waits for the actual headless process.
- Download Godot in CI with `Scripts/ci/Install-Godot.ps1`, which retries a failed download or unzip for up to 3 attempts (15 s, then 30 s apart); `Scripts/ci/Test-InstallGodot.ps1` proves it against a local server.
- Audit the complete dependency graph with `dotnet list CardCleaner.csproj package --vulnerable --include-transitive`.
- Lint workflows with `go run github.com/rhysd/actionlint/cmd/actionlint@v1.7.12`.

The GdUnit runner requires a newly generated `reports/ci/**/results.xml` and fails when its latest report contains failures or errors. GdUnit exit code `101` is retained as a warning for orphan-node reports; CI uploads `reports/ci` when the Windows build/test job fails, on the daily and `full=true` runs, and whenever the runner recorded a stall or crash in `reports/ci/interruptions.txt` (so a run that resumed to green keeps its logs).

## Quality Gate

The `quality-guard` pull-request check (`.github/workflows/ci.yml`, dod-guard pinned by SHA) scans the whole checkout at
a pull request's merge base and at HEAD, and judges the files the pull request changes. A high finding that exists only
at HEAD blocks (for line length
that means above 120 columns, as in `.editorconfig`); a finding that was already there never blocks. New medium findings
do not block either, but they are counted and listed. The pull request must also remove at least one medium/high finding
that was already in those files, so medium cleanups count toward the quota. Every pre-existing finding is listed in the
`quality-gate-report` artifact, linked from the pull request's single quality gate comment, so none of them needs an
individual fix. Tighten the policy with the flags on the gate step (`--block-on=medium`,
`--rule-block-on=<rule>:<severity>`, `--fix-medium-high`, `--fix-low`).

`.quality-guard.json` holds the scan settings, and the gate reads it from the merge base, so a pull request cannot
loosen its own scan. `excludePaths` lists the vendored addons (`asset_manager`, `gdUnit4`, `godot_ai`, `godot_omni`,
`saveable`), which are other people's code; `tile_editor` is ours and is scanned. `registeredAttributes` lists
`Service`: the service container builds every `[Service]` class by reflection, so the `dead-export` rule counts those
classes as reached, as it does Godot's `[GlobalClass]`. review-pr's scanner reads the same file, so CI and reviews judge
the same code the same way. The gate step's `--exclude=/.dod-guard/` keeps the gate's own dod-guard clone out of the
scan. The scanner runs unpatched: a Godot case it gets wrong is fixed in dod-guard, and then the pin moves.

`.github/quality-guard-godot.test.mjs` runs the pinned scanner on fixtures in the `quality-guard` job. It checks that
`[GlobalClass]` and `[Service]` classes are reached, so they are no dead exports, and that C# `using` directives count
as imports, so files that open with the same namespace list are not reported as a duplicate block.

Run the same gate locally with `node .github/quality-gate-local.mjs [--base=<sha>]` on the committed head before
pushing. It reads the dod-guard pin, `--exclude` flags and block-on setting from the `quality-guard` job in
`.github/workflows/ci.yml`, keeps a clone of that pin in the system temp directory, and writes
`reports/quality-gate-report.md`. The gate diffs a base against HEAD, so the command refuses a dirty working tree:
commit or revert first. `.github/quality-gate-parse.mjs` reads `ci.yml`, `.github/quality-gate-cache.mjs` keeps the
clone (a hand-edited clone fails the run by name), and `.github/quality-gate-local.test.mjs` pins the reading of
`ci.yml` and runs in the `quality-guard` job.

The `quality-guard` job also runs `node .github/claude-md-size.mjs`, which fails when `CLAUDE.md` passes 16 KB or names
a `docs/` file that does not exist. Every agent session loads `CLAUDE.md`, so it keeps only rules most tasks need and
feature notes go to the matching `docs/` file (see Where Notes Go in `CLAUDE.md`).
