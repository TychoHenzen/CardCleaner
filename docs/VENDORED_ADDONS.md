# Vendored Addons and Their Project Patches

Read this before you update or edit `addons/gdUnit4`, the `gdUnit4.api` package or `addons/asset_manager`. Moved out of
CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

**Project patch in the vendored addon (issue 448).** gdUnit4.api runs each C# suite on a thread-pool thread, and the
suite first awaits Godot's `process_frame` through a `SignalAwaiter`, which is not thread-safe. When the frame lands
between the awaiter's completion check and its continuation registration, the suite never starts and the run stalls
between suites (gdUnit4Net issue 397, closed upstream as a Godot bug; the package keeps that code internal).
`addons/gdUnit4/src/dotnet/GdUnit4CSharpApi.cs` therefore runs the bridge through `SuiteStartWatchdog` (same folder): an
attempt that sends no event within 30 s is cancelled, its late events are dropped, and the suite starts again, three
attempts in all. Each abandoned attempt prints `gdUnit4 suite start lost:` to stderr. gdUnit sends a suite's first event
after its `[Before]` hook, so no `[Before]` hook may await. `SuiteStartGuardTest` fails when an addon update drops the
patch or a `[Before]` hook returns a task; reapply both files after updating the addon.

**Project patch in the vendored addon (discovery cache).** gdUnit discovers C# tests one script at a time, and the
package's per-script discovery reads the whole test assembly and its symbols with Mono.Cecil on every call, about 33 ms
each: 17 s for the 518 scripts under `res://Tests` at the start of every run. Outside the editor, `GdUnit4CSharpApi.cs`
therefore asks `SuiteDiscoveryCache` (same folder) first. It reads the assembly once through the package's own
whole-assembly discovery (the internal `TestCaseDiscoverer.Discover`, reached by reflection) and files each suite's tests
under its script, so a script that is no `[TestSuite]` class gets no tests without a read. When the cache cannot be built
it prints `gdUnit4 discovery cache unavailable` to stderr and every script goes to the package as before.
`SuiteDiscoveryGuardTest` fails when the cache is unavailable or differs from the package on a sample of scripts, and
`SuiteDiscoveryEquivalenceTest` (Daily) compares the two for every script; reapply `SuiteDiscoveryCache.cs` and the call
in `GdUnit4CSharpApi.DiscoverTests` after updating the addon or the `gdUnit4.api` package.

**asset_manager is a synced copy of our fork.** `addons/asset_manager` is one commit of the fork
`TychoHenzen/Asset-Manager` (upstream `Kivylius/Asset-Manager`), and every change to it, ours included, is made in the
fork. Ours: the tab offers Load library on every showing until the library is used, and in a headless editor (the
import `Run-GdUnit.ps1` and CI run) the plugin builds no panel and no menu and preloads nothing, which took about 1.1 s
off each import. An upstream release is merged into the fork, where git keeps our edits or shows the conflict; the
addon's update button only opens the store page. `node tools/asset-manager/sync.mjs` then copies the fork's
`origin/main` (or `--commit=<ref>`) from a local clone (`--source=<dir>`, by default `../asset-manager`), `.uid`
sidecars included, stages the copy and records the commit and every file's hash in `tools/asset-manager/pin.json`. It
refuses while the addon differs from the old pin, a committed file the pin lacks included, so an edit made here goes to
the fork first. `node tools/asset-manager/sync.test.mjs` runs it against a throwaway fork and project (the
`runner-fixture` CI job runs it too). `AssetManagerVendorPinTest` fails when the addon differs from the pin, and
`AssetManagerLazyOpenTest` fails when a fork update drops one of our edits.
