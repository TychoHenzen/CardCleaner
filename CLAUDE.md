# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CardCleaner is a Godot 4.4+ game built with C# (.NET 8.0) featuring a card-based deckbuilder with procedural world generation. Cards have an 8-dimensional "signature" system representing elemental properties that influence gameplay, combat, and loot generation.

## Build & Test Commands

```bash
# Build the game project
dotnet build

# Unit tier, the development loop: plain .NET tests with no Godot process (budget 10 s, not counting the build)
dotnet test UnitTests

# One unit test class
dotnet test UnitTests --filter "FullyQualifiedName~CardSignatureTest"

# Pull-request tier: every gdUnit suite except the Daily ones, as CI runs on pull requests and pushes to master.
# Run it while working on merging a pull request, not during development (Windows PowerShell 5.1 or PowerShell 7)
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot_v4.7.1-stable_mono_win64.exe> -SkipDaily

# Every gdUnit suite, Daily ones included, as the daily CI run does
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe>

# One suite through the same runner (watchdog and resume included), reports and logs into reports/<name>
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe> -Suite res://Tests/Architecture/DependencyGateTest.cs -ReportDirectory reports/single

# Run the Godot import even though no file it reads changed (the runner skips it then, see the import skip in docs/TESTING.md)
powershell -NoProfile -File Scripts/ci/Run-GdUnit.ps1 -GodotBinary <path to Godot exe> -SkipDaily -ForceImport

# Runner fixture: runs Run-GdUnit.ps1 against a fake Godot, so no Godot is needed (CI runs it too)
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-RunGdUnit.ps1

# Godot download retry fixture: runs Install-Godot.ps1 against a loopback HTTP server, so no Godot or network is needed (CI runs it too)
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/Test-InstallGodot.ps1
```

**Test tiers.** Each tier has a time budget, measured without the build:

- **Unit** (`UnitTests/`, budget 10 s): plain .NET suites run by `dotnet test UnitTests`, with no Godot process. Run
  only this tier during development. A suite belongs here only when neither it nor the code it calls needs the engine:
  a Godot native call (a node, a resource, `GD`, `ProjectSettings`) crashes the plain test host. `UnitTests/` has a
  `.gdignore`, so its files get no `.uid` sidecars, and `CardCleaner.csproj` leaves the folder out of the game.
- **Pull request** (`Tests/` without the Daily suites, budget 5 minutes): the gdUnit suites inside Godot, run by
  `Run-GdUnit.ps1 -SkipDaily`. Run it while working on merging a pull request; CI runs it on every pull request and
  push to `master`.
- **Daily**: a suite whose class carries `[TestCategory("Daily")]`. Benchmarks belong here, and so does a check that
  cannot be made fast without testing less. A slow suite first gets faster while checking the same thing (a smaller
  fixture, `FastClock` in `docs/TESTING.md`, one shared expensive setup); only when that fails does its class get
  the tag. CI runs every suite, Daily ones included, at 03:00 UTC each day on `master`.

The runner's watchdog, resume, reports and import skip, CI scope and the daily run, the test helpers (`FastClock`,
`BoundedWait`), running Godot from Bash, and the e2e driver for the rendered game are in `docs/TESTING.md`.

## Rules for Every Task

A rule that names a test or check is enforced by it, mostly only in the pull-request tier, so a break shows up late.
The file named after a rule explains it.

- A `[TestSuite]` class is named after its file, and a `[TestCategory("Daily")]` tag sits on the suite class, never on
  a member (`TestSuiteNamingTest`). `docs/TESTING.md`.
- No `[Before]` hook may await or return a task (`SuiteStartGuardTest`). `docs/VENDORED_ADDONS.md`.
- A scene test that waits on game time wraps the wait in `using (FastClock.Start())`, never `--fixed-fps`, and a wait
  on frames calls `BoundedWait` instead of a loop of its own. `docs/TESTING.md`.
- A test writes its files under `user://`, never into the project. `docs/TESTING.md` (the import skip).
- Every C# class that derives from `Resource` derives from `PinnedResource` instead, and nothing loads on a worker
  thread (`PinnedResourceTest`). `docs/ENGINE_WORKAROUNDS.md`.
- Physics stays on the main thread, navigation maps build synchronously and the Jolt penetration slop stays 0.001,
  each pinned by a test. `docs/ENGINE_WORKAROUNDS.md`.
- `[Tool]` goes only on scripts that need the editor. An outer scene never overrides nodes inside an instanced scene
  unless that instance is `[editable]` (`SceneEditorRoundTripSceneTest`). `docs/GODOT_SCENE_RULES.md`.
- Domain code logs through `ILog`, never `GD`, and calls `CoreMath`, not `Mathf`. Nodes reach services only through
  the `IServiceResolver` they are injected with. `docs/ARCHITECTURE.md`.
- `DependencyGateTest` fails when a gated number rises above `Tests/Architecture/dependency-baseline.json`, and when
  one falls, until the baseline is re-recorded with the switch. `docs/DEPENDENCY_GATE.md`.
- The `quality-guard` check blocks a new line over 120 columns and asks every pull request to remove at least one
  medium or high finding already in the files it changes. Run `node .github/quality-gate-local.mjs` on the committed
  head before pushing. `docs/quality-gates.md`.
- Committed meshes (`Data/Card/CardMesh.tres`, `Data/Packs/Meshes/`) are regenerated from their recipe with a switch,
  never edited by hand. `docs/CARDS.md`, `docs/CONTAINERS_AND_HAND.md`.
- Text in the world is printed on a `SignBoard`, never a billboard `Label3D` (`NoFloatingTextSceneTest`).
  `docs/SHOP_SCENE.md`.
- A screen that claims the cursor and freezes the player implements `IModalScreen` and joins `modal_screens` in
  `_EnterTree`. `docs/SHOP_ECONOMY.md` (Day Cycle).
- New saved state gets a round-trip test that saves, reloads and compares it. An old save gets no migration step,
  because the game is not released. `docs/PERSISTENCE.md`.
- Art lives in the private `Assets/` submodule; an empty `Assets/` means it is not initialised (`git submodule update
  --init`). Changing an asset is two commits: inside `Assets/` first, then the `Assets` pointer here. `docs/ASSETS.md`.

## Code Style

The project uses `.editorconfig` for code style enforcement with Godot-specific adaptations:

- **Naming Conventions**: Interfaces prefixed with `I`, private fields with `_camelCase`, public members use `PascalCase`
- **Nullable Reference Types**: Enabled project-wide at warning level (CS8600-CS8625 series)
- **Modern C# Features**: File-scoped namespaces, pattern matching, primary constructors encouraged
- **Godot Lifecycle Exceptions**:
  - CA1051 disabled (allows public fields for `[Export]` attributes)
  - CA2000/CA1816 relaxed (Godot manages node lifecycle)
  - CA1031 lenient (game code exception handling)
- **Tests/Addons**: Tests have relaxed null checking; addons folder has all analyzers disabled
- **Unused usings**: the build reports an unused `using` as warning IDE0005 in our own code, because both project
  files set `GenerateDocumentationFile` (the compiler reports IDE0005 on a command-line build only while it parses XML
  comments), and `.editorconfig` turns IDE0005 off for the vendored `addons/asset_manager` and `addons/gdUnit4` only;
  the game project sets `PublishDocumentationFile` to false so no XML file is exported, and the fix is to delete the
  `using` line that the warning names.
- **Warnings**: the build prints none, and the CI steps `Build with analyzers` and `Build the unit test project` run
  with `-warnaserror`, so a pull request that adds a compiler or analyzer warning fails with an error that names it
  (check with the commands below, which build what those two steps build); fix the warning, or suppress it beside the
  code with a `SuppressMessage` or `#pragma` that says why. Each step builds after a restore of its own and passes
  `-p:EmitAssetsLogMessages=false`, so a NuGet restore warning (an advisory on a pinned package) stays a warning and
  fails no build. Locally `--no-incremental` matters: an incremental build that compiles nothing prints no warnings.

  ```bash
  # Restores the game project too, through the project reference
  dotnet restore UnitTests/CardCleaner.UnitTests.csproj
  # Build the unit test project: the unit tests, and the game in Debug through the project reference
  dotnet build UnitTests/CardCleaner.UnitTests.csproj --no-restore --no-incremental --configuration Debug \
    -warnaserror -p:EmitAssetsLogMessages=false
  # Build with analyzers: the game in Release
  dotnet build CardCleaner.csproj --no-restore --no-incremental --configuration Release \
    -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -warnaserror -p:EmitAssetsLogMessages=false
  ```

### Godot .uid Sidecars

Godot 4.4+ gives every script and shader a `.uid` sidecar, and scenes reference them by that UID. Sidecars under
`Scripts/`, `Tests/` and `Shaders/` are committed (`.gitignore` negates `*.uid` for those roots), so a fresh clone keeps
the same UIDs; `addons/` sidecars stay ignored, except `addons/asset_manager`'s, which the fork commits and its sync
tool force-adds with the rest of the copy. Move or rename a source together with its `.uid` (both in one `git mv`,
or through the editor's FileSystem dock). A headless gdUnit run does not create a new script's `.uid`; run `<godot console exe> --headless --path . --import`, which writes only the sidecar, and commit it with the script. A new folder of scripts or shaders outside those roots needs its own negation
in `.gitignore`. A `.uid` whose source is gone is removed, not ignored: this is an exception to the rule of adding an
ignore entry for a generated file, because no workflow needs a sidecar without its source. `ExtResourceUidTest` fails
when a scene's `ext_resource` UID does not match its target.

## Canonical dod-guard Workflow

- `/add-backlog-idea` creates an issue in **Backlog**.
- `/refine-backlog-item` researches the issue and moves it to **Todo** when it is ready.
- `/next-ticket` implements exactly one issue on one issue branch.
- `/submit-draft-pr` publishes the branch as a draft pull request.
- Review remains read-only until the user explicitly accepts the findings or requests remediation.
- `/complete-pr` owns the ready-to-merge check, merge, linked-issue confirmation, and remote branch deletion.

Behavior is specified in `openspec/specs/`, which is tracked and grows as archived changes merge their specs into it.
Change folders under `openspec/changes/` (archives included) are ignored local scratch and never a source of truth; the
GitHub issue owns the plan. How-to and workflow guidance goes into the matching `docs/` file (see Where Notes Go),
not into `openspec/`.

## Where Notes Go

This file is loaded into every agent session, so it holds only rules that most tasks could break. A change that
documents a feature, a scene, a system, a tool or an engine workaround writes that note into the matching `docs/` file
from the index below, never here. A topic with no file gets a new `docs/<TOPIC>.md` and one line in the index. A rule
joins Rules for Every Task only when most tasks could break it: one line here, the explanation in its `docs/` file. An
issue plan or acceptance criterion that asks for a sentence "in CLAUDE.md" is met by the matching `docs/` file unless
that sentence is such a rule. `.github/claude-md-size.mjs` fails the `quality-guard` check when this file passes 16 KB
or names a `docs/` file that does not exist.

## Documentation Index

Read a file when your change touches what its line names.

- `docs/TESTING.md`: `Scripts/ci/Run-GdUnit.ps1` and its fixtures, CI scope and the daily run, the import skip,
  `FastClock`, `BoundedWait`, gdUnit report folders, Godot from Bash without PowerShell, `tools/e2e`.
- `docs/VENDORED_ADDONS.md`: the project patches in `addons/gdUnit4` (suite-start watchdog, discovery cache) and
  `addons/asset_manager`, synced from our fork by `tools/asset-manager/sync.mjs`.
- `docs/ENGINE_WORKAROUNDS.md`: the physics, navigation and Jolt settings in `project.godot`, `PinnedResource`,
  `CardSleepEnforcer`.
- `docs/GODOT_SCENE_RULES.md`: `[Tool]` scripts, overriding nodes inside an instanced scene, `_PropertyCanRevert`.
- `docs/ARCHITECTURE.md`: `ServiceHost`, `CompositionRoot`, `[Service]` classes, `IDependent`, `ILog`, `CoreMath`,
  the session factories, the tile catalog and its loaders.
- `docs/DEPENDENCY_GATE.md`: `Tests/Architecture/DependencyGateTest.cs` and its fixtures, the baseline file, a failing
  gate.
- `docs/quality-gates.md`: the CI gates, `quality-guard`, `.quality-guard.json`, `.github/quality-gate-*.mjs`.
- `docs/CARDS.md`: `CardSignature`, the card mesh and `CardFace`, rarity and flavor text, card art effects.
- `docs/CONTAINERS_AND_HAND.md`: boxes, booster boxes and boosters (`Scripts/Features/Packs`) and opening them, the
  hand (`Scripts/Features/Hand`), `ItemTransfer` and `IItemStore`, cardboard box storage.
- `docs/FEATURES.md`: what each `Scripts/Features` folder holds, and decks and piles (`Scripts/Features/CardStacks`).
- `docs/WORLDGEN_ARCHITECTURE.md`: map generation, WFC, biomes, auto-tiling and gradients (`Scripts/Features/Worldgen`,
  `GridMapGenerator`).
- `docs/GAME_SESSION_FLOW.md`: the `GameSessionService` states, exploration and combat (`Scripts/Features/Deckbuilder`).
- `docs/PERSISTENCE.md`: saving and loading (`Scripts/Features/SaveSlots`, `GameSaveService`, `ISaveable`), save
  slots, furniture saves, stock saved on seats.
- `docs/ASSETS.md`: the `Assets/` submodule and adding art through the Asset Manager.
- `docs/SHOP_SCENE.md`: the `ShopScene.tscn` layout, `ShopArtSlot`, roof, lights, street, furniture and placing it,
  `SignBoard`.
- `docs/UI_AND_HUD.md`: the main menu, save slots and options screens, the aim HUD, click prompts and key hints.
- `docs/SHOP_ORDERING.md`: the backoffice PC desktop and its apps, ordering and delivery (`StockApp`,
  `DeliveryMarker`).
- `docs/SHOP_CHECKOUT_AND_SHELVES.md`: the checkout counter and payment, shelves, shelf levels and seats, prices and
  price tags.
- `docs/SHOP_ECONOMY.md`: shop level and XP, the day cycle and bills, storefront expansion, staff (cashier,
  restocker) and the people's figures.
- `docs/WORKSHOP.md`: the workshop warehouse, its seams and the cabinet.
