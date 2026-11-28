# Cards

Read this when you touch card signatures, the card mesh or `CardFace`, rarity and flavor text, or card art effects.
Moved out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

## Card Signature System

The core mechanic uses `CardSignature` - an 8-dimensional vector with values clamped to [-1, 1] representing elemental properties:

| Index | Name | Description |
|-------|------|-------------|
| 0 | Solidum | Solidity (air-rock) |
| 1 | Febris | Temperature (water-fire) |
| 2 | Ordinem | Orderedness (entropy-order) |
| 3 | Lumines | Luminance (dark-light) |
| 4 | Varias | Manifold (time-space) |
| 5 | Inertiae | Density (heavy-light) |
| 6 | Subsidium | Helpfulness (harmful-helpful) |
| 7 | Spatium | Distance (nearby-distant) |

Signatures influence card generation, combat calculations, map generation parameters, and loot drops.

## Card Mesh

A card's look is its own scene, `Scenes/Components/CardFace.tscn` (`CardFace`: the renderer, the name and flavor labels, the blacklight, the material manager and the mesh), which the card body `Scenes/Components/CardShader.tscn` instances as `Face` and a pile instances once per card it shows; `CardFace.Dress` builds a face from a signature and look seed, so a face on a pile looks exactly like the same card loose. The card body is a committed mesh, `Data/Card/CardMesh.tres`, shown by the `OuterBox_Baked` node in `CardFace.tscn`; no card builds CSG at runtime. Its source is `Tests/Features/Card/Components/CardMeshRecipe.cs` (test code: a CSG tree for a 0.32 x 0.45 x 0.005 m card with 0.032 m rounded corners, baked and given the card's UV layout). `CardMeshBakeTest.CommittedMeshMatchesRecipe` fails when the file and the recipe differ, and `CardShaderSceneTest` fails when the `Designer` dimensions or the `OutlineBox` size (mesh plus 0.01 m) no longer match the mesh. To change the shape, edit the recipe (and the size constants in `CardMeshBakeTest`), then regenerate with the switch set:

```bash
CARDCLEANER_REGENERATE_CARD_MESH=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rd reports/ci -a res://Tests/Features/Card/Components/CardMeshBakeTest.cs
```

The drift test can fail in that run because it may compare against the old file; run the suite again without the switch. For a size change, also update the `Designer` `Width`/`Height`/`Thickness`, the `CardCollision` box and the `OutlineBox` mesh size in `CardShader.tscn`. Never edit the `.tres` by hand.

## Card Rarity and Flavor Text

A card's rarity is the number of pure elements in its signature (`SignatureCardHelper`): an element counts when its
magnitude is at least `PureThreshold` (0.9), in either direction, and 0, 1, 2, 3 and 4 or more pure elements give
Common, Uncommon, Rare, Epic and Legendary. Over uniform random signatures that is about 43.0, 38.3, 14.9, 3.3 and 0.5
percent, and moving any element further from zero never lowers a rarity. The 1-in-200 special card of a booster
(`CardPackGenerator.SpecialCardOdds`) is faint (`SignatureRandom.CreateFaint`: every element within `FaintCap`, 0.05,
one not zero), so it reads as only just above mundane and rates Common; the debug spawner's key 4 still draws a
full-range signature. Every card shows one flavor sentence (`FlavorText.For`, Godot-free, in the `FlavorLabel` of
`CardFace.tscn`): a mundane card gets a filler sentence chosen by its look seed, and a card with a signature gets one
built from its strongest element's word for its direction, plus the second element's word when that one is at least
half as strong. The choice is seeded like the card's look, so the same card always shows the same sentence and nothing
is saved for it.

## Card Art Effects

`SignatureCardGenerator` gives every spawned card a rarity effect (embossed, glow, glossy, foil) and a condition effect (worn, shiny) on its art region: `CardEffectMapping` maps the rarity and the signature's intensity tier (mean element magnitude, so the all-zero signature of an ordinary pack card is Dormant and looks worn) to shader ids, and `CardMaterialManager.SetArtEffects` passes them with a per-card seed (`CardEffectSeed.For`, from the signature alone, so the same signature always gets the same scratches) and the baked bevel map (`CardEffectNormalMapCache`, baked once per art texture) to `Shaders/card_layers.gdshader`. The bevel map is baked off the main thread (a couple of art readbacks a frame, the bake on the thread pool), so the first card to show a new art texture renders its condition effect only and switches its rarity effect on when the map is ready, a few frames later; tests call `CardEffectNormalMapCache.ResetForTesting()` in their teardown. `CardMaterialManager.SetArtEffects` returns the number of its request, and the bevel map is delivered with that number through `SetArtNormalMap`, so a map for a request that a newer generation has superseded is ignored. To compare all five rarities (columns) by the three tiers (rows) side by side, open `Scenes/Debug/CardEffectComparison.tscn` in the editor and press F6 (run current scene); the Epic and Legendary cells of the Dormant row stay empty, since three or more pure elements cannot have a Dormant mean magnitude. It is not the main scene, spawns real cards through the normal spawning service and fits the grid to the window. In that scene key 2 adds 10 cards, key 3 adds 100, key 0 removes them and key E sets `CardShaderRenderer.ArtEffectsEnabled` on every card (each card's material manager holds the on/off state, so a bevel map that arrives while the effects are off waits until they are switched back on), while the top-left readout shows the frame time and the GPU time, so the cost of the effects can be compared; the `CardSpawner` random-card keys 1 to 4 are switched off there (`SpawnKeys`).
