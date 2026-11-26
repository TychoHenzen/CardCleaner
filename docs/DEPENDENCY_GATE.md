# Dependency Gate

Read this when `DependencyGateTest` fails, when you re-record its baseline, or when you change the gate's code. Moved
out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

`Tests/Architecture/DependencyGateTest.cs` (in the pull-request tier, so every pull request runs it) measures `Scripts/` as a
layered architecture on every run. Its code is test code under `Tests/Architecture/DependencyGate/`: Roslyn extracts
the class graph of every top-level type in `Scripts/` (nested types fold into their outer type; `addons/` except
gdUnit4 is compiled alongside but not counted), plus one member-type reference per NodePath between two scripted
nodes in the `.tscn` files of `Scenes/` and `Data/` (merged with the code's own member-type references on that pair).
Three anchors are each one module: UI on top (Godot types and engine-API users, plus interfaces only UI uses), pure
data at the bottom, and pure contracts (interfaces naming only data and contracts) above it. The middle is clustered
with a deterministic Louvain consensus (seeds 0 to 49). A class referenced from 6 or more homes (`Features/<X>`,
`Core/<X>`) is omnipresent: it is left out of clustering and joins the module it references most, or stays loose
when it references none. A class whose summed clustering weight to other middle classes is below 4 stays loose, and
so does every class of a community under 4 classes. Modules are ordered Data, Contracts, middle, UI; inside each
layer every loop of up to 20 modules takes the order with the fewest up edges, and a bigger loop is ordered greedily
(the report names it).

The UI layer is also measured from inside (#484, `UiModules`), because the four older numbers see it as one module,
so no reference between two UI classes can move them. Only the UI classes are clustered, over only the references
between them, with the same consensus as the middle (omnipresent and loose classes and the 4-class minimum
included). The clusters are `U<n>` modules, a UI class in no cluster is its own `loose <class>` module, and they
are ordered on their own. Their up edges and their biggest loop are the gated numbers `ui_up_edges` and
`ui_biggest_loop`, the last two in the priority order. A reference between two UI modules is never rated as a
seam: node scripts reach each other through exported node references, and an interface for each would only satisfy
the gate. These two numbers are not at a floor: they record the hand, shelf, checkout, customer and PC tangle, so
the ratchet only stops it from growing until work on that tangle lowers them.

The test prints a report (`dependency gate:` lines): counts, the module order, the loose classes, every up edge with
its class pairs (heaviest first), every loop, and every leaky or missing seam with the concrete classes it crosses,
then the UI view (`ui modules`, `ui order`, `ui loose`, `ui up edge` and `ui loop` lines).
It measures the class graph built once per test run (`RepositoryClassGraph.Shared`, which `SignatureLayerTest` reads
too). The Daily suite `DependencyGateRepeatabilityTest` builds a second graph on a worker thread at the same time and
fails if its report differs from the shared graph's; it is Daily because the second Roslyn compilation costs several
seconds. The gate then ratchets six gated numbers (`up_edges`, `biggest_loop`, `seams_leaky_or_missing`,
`refs_from_non_ui_to_ui`, `ui_up_edges`, `ui_biggest_loop`) against
`Tests/Architecture/dependency-baseline.json` in that priority order (#299). The label of one seam can flip between
leaky and missing when Louvain membership changes, so the report still prints both counts but only their sum,
`seams_leaky_or_missing`, is gated (#278). The first number that differs from its baseline decides; if no number
differs, the test passes. If it went up, the test fails naming it (every other rise is
listed after it). If it went down, the step is accepted, and a rise in a lower-priority number is accepted with it:
each such rise is named in the failure and printed as a `dependency gate:` line (`<name> is <m>, above the baseline
<b>: accepted because <deciding> went down`). An accepted step still fails until the baseline is re-recorded with the
switch set:

```bash
CARDCLEANER_UPDATE_DEPENDENCY_BASELINE=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rd reports/ci -a res://Tests/Architecture/DependencyGateTest.cs
```

The switch rewrites the file only when the deciding number went down (it still prints the accepted rises), refuses
and leaves the file unchanged when the deciding number went up, leaves it untouched when no number differs, and a
missing or malformed baseline fails with its path whether the switch is set or not and is never written. A first
baseline is created by committing the file, and so is a number the file lacks: add it by hand at the value that
the failing run's `gated:` line prints, then run the switch once to record its lists (#484). CI prints the report
lines (`Show the dependency gate report` step and
the job summary) on every run, pull requests included. Every step of plan v4 (#239) ends with this test green and
the baseline re-recorded to what the step achieved. The fixture suites beside it
(`LayerAnchorsTest`, `LouvainClusteringTest`, `ModuleOrderTest`, `SeamRaterTest`, `GateMetricsTest`,
`ClassGraphExtractorTest`, `SceneLinkReaderTest`, `DependencyBaselineTest`, `UiModulesTest`) pin each rule on
small graphs.

The baseline also records six class-name lists behind the gated numbers: `up_edge_pairs` (for `up_edges`),
`biggest_loop_classes` (for `biggest_loop`), `seam_classes` (for `seams_leaky_or_missing`), `ui_ref_pairs` (for
`refs_from_non_ui_to_ui`), `ui_up_edge_pairs` (for `ui_up_edges`) and `ui_loop_classes` (for `ui_biggest_loop`),
each sorted and written as "A -> B" pairs or class names. The lists never fail the gate. When
a number is refused or lowered, the failure text names the classes joined and left in its list. When no number differs
but a list changed, the gate passes and prints a `dependency gate:` note with the joined and left classes. The switch
records the lists when no number differs and they are missing or changed; it still never writes after a refused rise.
Recovery when a step moves a gated number: run the gate on the base as a control, and try at most three placements of
the new class before recording a seam. The UI clusters move with membership as the middle's do (dropping one UI
class at a time moved `ui_up_edges` by at most 1 and `ui_biggest_loop` by at most 2 in a probe), so a UI number
that rises with no class of the change in its list is checked on the base first. Leaky and missing are already one
gated sum (`GateMeasurement.Gated`), so a
missing-to-leaky relabel is not a rise.
The Louvain visit order is sorted by a hash of the seed, level and class name, so adding a class no longer reshuffles
every seed; the total weight and the termination threshold stay global, so stability under every change is not promised.

The baseline records `seams_leaky_or_missing` 0 with an empty `seam_classes` list, and `ui_up_edges` and
`ui_biggest_loop` at the values the UI view first measured (#484). The WFC constraint
`BiomeAffinityConstraint` reads biome strengths through `IBiomeStrengthField` (`Scripts/Features/Worldgen/Wfc`), which
`BiomeStrengthField` implements.
