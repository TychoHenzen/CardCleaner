# Quality Gates

The repository uses a small set of reproducible gates for the C# Godot project. The CI workflow runs on Windows with .NET SDK `8.0.410` and Godot `4.7.1-stable` Mono.

## Applicability

| Language | Format | Lint/static analysis | Type/build | Test | Coverage | Dependency | CodeQL |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C# | Skipped: no repository formatter is configured. | `dotnet build CardCleaner.csproj --no-restore --configuration Release /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` runs the enabled .NET analyzers and code-style checks; `actionlint` validates workflow files. | The same analyzer-enabled build is the type/build gate. | `scripts/ci/Run-GdUnit.ps1` runs the GdUnit4 suite at `res://Tests` with Godot headless. | Skipped: no coverage collector or threshold is configured. | `dotnet list CardCleaner.csproj package --vulnerable --include-transitive` checks the complete NuGet graph after restore; the project relies on `net8.0` framework HTTP APIs rather than a direct `System.Net.Http` package. | The CodeQL workflow initializes, autobuilds, and analyzes C#. |
| GDScript | Skipped: no repository formatter is configured. | Skipped: no standalone GDScript linter is configured; `actionlint` does not inspect GDScript. | Skipped: no independent GDScript type/build gate is configured. | Skipped: the configured GdUnit command targets the repository's `res://Tests` suite rather than a separate GDScript suite. | Skipped: no GDScript coverage collector or threshold is configured. | Not applicable: no GDScript package manifest is used. | Not applicable: CodeQL is configured for C# only. |

## Commands

- Restore before building with `dotnet restore CardCleaner.csproj`.
- Build with analyzers using `dotnet build CardCleaner.csproj --no-restore --configuration Release /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`.
- Run GdUnit locally with `scripts/ci/Run-GdUnit.ps1 -GodotBinary <path-to-Godot_v4.7.1-stable_mono_win64.exe>` or set `GODOT_BIN`; the runner uses the sibling console executable when available so Windows waits for the actual headless process.
- Audit the complete dependency graph with `dotnet list CardCleaner.csproj package --vulnerable --include-transitive`.
- Lint workflows with `go run github.com/rhysd/actionlint/cmd/actionlint@v1.7.12`.

The GdUnit runner requires a newly generated `reports/ci/**/results.xml` and fails when its latest report contains failures or errors. GdUnit exit code `101` is retained as a warning for orphan-node reports; CI uploads `reports/ci` when the Windows build/test job fails.
