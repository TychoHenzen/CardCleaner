# export-check

What does an exported game carry? Two headless checks answer it. Neither needs a window or a running game.

## publish

`node tools/export-check/export-check.mjs publish [--dir=<publish folder>]`

Publishes `CardCleaner.csproj` in the `ExportRelease` configuration, the one Godot builds an export in, into a scratch folder, and fails when the output holds an assembly other than `CardCleaner.dll`, `GodotSharp.dll` and `Newtonsoft.Json.dll`, or when `CardCleaner.dll` mentions test code or a test package. `--dir` reads a publish folder that already exists. CI runs it in the `unit-tests` job. It redirects the intermediate folder, so the Debug packages that gdUnit builds restore from stay where they are.

## pck

`node tools/export-check/export-check.mjs pck <exported.pck>`

Reads the file table of an exported pack (format 4, Godot 4.7) and fails when a file of `Tests/`, `UnitTests/`, `addons/gdUnit4/`, `addons/godot_ai/` or `addons/export_guard/` ships, or when the empty-node stand-in for the helper autoload is missing. A `.cs` file of those folders may stay as a stand-in of at most 16 bytes: the C# exporter adds one for every script, whatever an export plugin says.

## What keeps an export clean

- `CardCleaner.csproj`: under the `ExportDebug` and `ExportRelease` configurations, `Tests/` and `addons/gdUnit4/` leave the compile and the test packages leave the restore. `Newtonsoft.Json`, which only arrived through the gdUnit4 packages, is a direct reference. Debug and Release builds (the editor, gdUnit, the unit tier, CI, e2e) are unchanged.
- `addons/export_guard`, an editor plugin enabled in `project.godot`: every export, whatever its preset, skips the folders above. `project.godot` lists the AI-editor helper autoload in every build, so the export ships `game_helper.gd` as an empty node instead of dropping it. `Tests/Addons/ExportGuard` pins the policy and checks that no autoload points into a skipped folder.
- `export_presets.cfg` is gitignored, so the guard does not depend on it.

## Looking at a real export

1. In the editor, `Project > Export > Add... > Windows Desktop` writes the gitignored `export_presets.cfg`.
2. Run `Godot --headless --path . --import`, then `Godot --headless --path . --export-release "Windows Desktop" <folder>/CardCleaner.exe`. The Godot process can stay alive for minutes after it logs `savepack` as done: the pack and the data folder are complete by then.
3. Run `node tools/export-check/export-check.mjs pck <folder>/CardCleaner.pck`, and look in `<folder>/data_CardCleaner_windows_x86_64` for `GdUnit4Api.dll`, `FsCheck.dll`, `NSubstitute.dll` and `Microsoft.CodeAnalysis.dll`: none should be there.
4. Run `<folder>/CardCleaner.exe --headless --quit-after 5 --log-file <log>` to start the exported game; the log must not mention an autoload.
