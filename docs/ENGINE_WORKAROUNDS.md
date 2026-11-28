# Engine Settings and Workarounds

Read this before you change a physics, navigation or Jolt setting in `project.godot`, or a C# `Resource` class. Moved
out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

**Physics runs on the main thread** (`physics/3d/run_on_separate_thread=false`, pinned by `PhysicsThreadSettingTest`).
With the separate thread on, Godot 4.7.1 froze the whole process, in tests and in the game alike. A main-thread physics
call during the physics frame (here a `CollisionObject3D` notification) found the physics command queue mid-flush,
queued a sync and waited, while the physics thread finished its flush and refused to flush again until the frame ended.
Native stacks of two captured freezes showed exactly that (godotengine/godot issue 123654). The upstream fix is pull
request 124990, merged to master on 2026-10-31 and marked for a 4.7 backport. Turn the setting back on only on an
engine version that contains it.

**Navigation maps build synchronously** (`navigation/world/map_use_async_iterations=false`, pinned by
`NavigationMapSyncSettingTest`). With async map builds, a build that included a rebaked region could finish in the same
navigation sync that applied the region, which from outside looks the same as a stale build finishing there, so
`ShopNavigationRig.WaitForBakeSync` could wait for a map change that never came. Built synchronously, the sync that
applies a region iteration also builds the map from it.

**Cards rest on top of the floor** (`physics/jolt_physics_3d/simulation/penetration_slop=0.001`, checked by
`CardFloorRestTest` on the real card scene). Jolt lets resting bodies sink into each other by the slop, 0.02 m by
default, and a card is 5 mm thick, so the default hid cards under the shop floor. The card's `CardSleepEnforcer` also
freezes a card only after it has stayed below its thresholds for `RestSeconds` (0.5 s, the rest time Jolt waits before it
lets a body sleep): freezing on the first slow frame froze a landing card inside the floor, before Jolt had pushed it out. It also freezes
a card as soon as Jolt puts it to sleep (`SleepingStateChanged`). A sleeping body gets no more `IntegrateForces` calls,
so when Jolt's own half second ran out first, the enforcer's count never finished and the card lay unfrozen, which
failed `CardFloorRestTest` on some CI runs and not others (`ACardJoltPutsToSleepIsFrozen` pins it).

**C# resources stay loaded** (`PinnedResource`, `Scripts/Core/Resources`). Every C# class that derives from `Resource`
derives from `PinnedResource` instead (`PinnedResourceTest` fails otherwise), which keeps an instance referenced for the
rest of the run once a load has given it a path. Godot 4.7.1 holds only a weak handle to a C#-scripted resource that
nothing but its managed wrapper references. When the collector took that wrapper and a scene or a `GD.Load` took the
same path from the resource cache before the wrapper's finalizer ran, the finalizer stopped a debug build with `FATAL:
Condition "gchandle.is_released()" is true` (exit `0xC000001D`, godotengine/godot issue 83762). The same crash, with the
same finalizer backtrace, stopped `RestockerCarryTest` on full CI runs, since each of its cases loads the shop again.
`ScriptedResourceRevivalTest` parks the finalizer thread so the race would happen on every run. An instance made with
`new` has no path and is collected as usual. The pin checks the path at the end of the frame, so nothing may load on a
worker thread (`ResourceLoader.LoadThreadedRequest`), which `PinnedResourceTest` also checks. A loaded resource is never
unloaded: leaving the shop keeps its catalogs and the card visuals (about 3.6 MB of PNG textures) in memory. Drop the
base class only on an engine version that contains a fix for that issue.
