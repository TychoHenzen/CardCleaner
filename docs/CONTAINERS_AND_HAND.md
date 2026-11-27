# Containers, Opening and the Hand

Read this when you touch boxes, booster boxes or boosters, opening them, the hand, or moving items between stores. Moved
out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

## Container Meshes and Opening

The booster (`Scenes/Shop/Items/Booster.tscn`), pack (`Pack.tscn`) and cardboard box (`CardboardBox.tscn`) are built
like the card: committed meshes in `Data/Packs/Meshes/` with their materials in `Data/Packs/` (the foil wrapper
`Shaders/booster_wrapper.gdshader`, the printed board and kraft carton `Shaders/printed_board.gdshader`). Their source
is `Tests/Features/Packs/Components/ContainerMeshRecipe.cs` (test code), and `ContainerMeshBakeTest` fails when a
committed mesh differs from the recipe. To change a shape, edit the recipe, regenerate with the switch set, then run the
suite again without it:

```bash
CARDCLEANER_REGENERATE_CONTAINER_MESHES=1 <godot console exe> --headless --path . -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -c --ignoreHeadlessMode -rd reports/ci -a res://Tests/Features/Packs/Components/ContainerMeshBakeTest.cs
```

Never edit the mesh `.tres` files by hand. The words printed on a container (brand, count, THIS SIDE UP) are flat,
shaded `Label3D`s lying on its faces, and `StockItemLook` copies them with the meshes, so a checkout prop keeps its
print. The parts that move when a container opens (the booster's tear strip, the box's four flaps) are `OpeningPart`
nodes: each turns by `OpenRotationDegrees` about its hinge and moves by `OpenOffset` over its share of the opening
(`StartAt` to `EndAt`), and one that `VanishWhenOpen` (the torn-off strip) shrinks away. The booster box's lid is a
plain node, since a booster box empties into the hand at once.

Nothing opens on the floor: a container is opened only while the player holds it (see The hand and Opening a booster in hand below). The one
opening that plays over real time is a booster's in the hand (`BoosterReveal`); `OpeningTime` holds the smoothstep
`Ease` and the span `Fraction` it and `OpeningPart` share. `FinishOpening` ends it at once (every card in the hand, the
wrapper freed). `GameSaveService.Save` first finishes every opening (`SavedContainers.FinishOpenings`), so a save made
mid-opening stores the cards in the hand, never a half-open booster. Tests open containers in a test hand through
`InHandOpening`, and wait for a booster's opening with `ContainerOpeningWait` (bounded in wall-clock time through
`BoundedWait`, because a headless frame is not paced at the game's 60 frames a second), or end it with
`FinishOpening` when only the cards matter.

Sizes: booster 0.35 x 0.045 x 0.53 m, pack 0.38 x 0.20 x 0.55 m, box 0.80 x 0.669 x 0.86 m (`CartonSpace`: 8 mm walls,
an inside 0.784 x 0.645 x 0.844 m, tall enough for four stacks of 128 cards). The product shelf's seats
sit at z 0.055, so a pack fits on its 0.671 m deep boards.

**Names.** The player reads a `Box` as a cardboard box, a `Pack` as a booster box and a `Booster` as a booster pack
(`CardContainerKindText.NameOf`, display only). The id of a kind, its item id in `BackofficeCatalog.tres` and the key
its wholesale cost is read under, is `ProductIds.IdOf` (`cardboard_box`, `pack`, `booster`), so renaming what the
player reads never touches a catalog row or a save.

**The hand.** The player has one hand, `Hand` (`IHand`, `Scripts/Features/Hand/Components`, in `player.tscn`), over the
Godot-free `HandItems`: cards and sealed containers in the order they were taken, one of them the front item that every
hand action applies to. A card or a booster pack takes one place and the hand holds at most `HandItems.MostPlaces`
(100); a booster box or a cardboard box is taken only into an empty hand and is then its only item, carried level.
Cards and booster packs fan out together in front of the camera (`CardFan` over the Godot-free `HandFan`: the spread
widens to 50 degrees over the first ten items, and later items close the gaps instead), with the front item raised out
of the fan. The wheel moves the front (`hand_next`, `hand_previous`, wrapping at either end), Q sets the front item down
in front of the player (`PutFrontDown`), and the right button acts on the front item (`UseFront`): it swings a cardboard
box's flaps, empties a booster box into the hand, opens a booster pack, or on a card starts a card drop. A held body is
frozen, on no collision layer, under the hand anchor (the player's camera). The hand's key chips come from one list,
`HandHints`, for whatever is in front. A save stores the hand as one ordered record under `hand`, with the front item
marked (`SavedHand`); a reload builds every item, gives it back to the hand in the saved order and makes the saved front
item the front again (`HandSaveTest`). The scene's own card and container lists leave out what the hand holds.

**Moving items: one rule.** Every move between the hand and a store goes through `ItemTransfer` (Godot-free,
`Scripts/Features/Hand/Models`) over `IItemStore` (`Scripts/Core/Interfaces`: `Count`, `Peek`, `CanPut`, `Put`,
`TakeLast`), which the hand, a pile or deck, an open cardboard box (`CartonStorage`) and a shelf level all implement.
The part of the store the aim hits decides (`AimPart`): a click on stock takes by the store's `TakePolicy` (`One` for a
pile, an open box and a shelf level; `WholeIfFits` for a deck) when the hand has room, and a click on free space puts
the hand's front item when the store accepts it. Shift+click takes as many as the hand has room for, at most
`ItemTransfer.MostPerShiftClick` (100). Piles and decks have no free space: they grow only from dropped cards and from
strapping. While the front item is an open cardboard box, the box is the source instead of the hand
(`DecideFromHeldBox`, through `IPickingStore`, so a click on a rack level stocks the box's last booster box even when
cards lie on top of it), a click on nothing sets its last item down where the aim meets a level surface within 4.5 m
(`IHand.PutOneDown`), and the trash can takes only an empty cardboard box (`DecideTrash`). The player's
`CardHighlighter` is the one hand router (`IClickRouter`, plans in `HandClickPlans`): `InteractionSystem` asks it about
every target it claims, and a plan carries the prompt and the move together, so the prompt (`TransferPrompt`) names
exactly what the click does and a click that would move nothing offers no prompt. A target the router claims (a shelf
level's `ShelfLevelTarget`, a loose card or container) has `CanInteract` false on its own. `ItemTransfer.Apply` moves
records; picking up, setting down and throwing away a body in the world are carried out by the caller.

**Cardboard box storage.** A cardboard box is storage: it never opens whole (`EmptyIntoHand` and `OpenInHand` refuse
it) and is never freed when it empties. What it holds is its `Storage` child (`CartonStorage`) over the Godot-free, immutable `BoxContents`
(`Scripts/Features/Packs/Models`): `StoredItem` records in the order they went in (a `StoredCard` keeps the card's
signature, look seed and price, a `StoredContainer` a booster's or booster box's scene, kind, seed and price), each
placed by `BoxPacker` inside `CartonSpace.Interior` so no two overlap. Cards go into four pinwheel stacks, filled one
stack at a time, so an empty box takes at least 512 cards (a sealed box's worth); containers are still placed by a
corner search, so cards beside containers fit fewer. A delivered box
holds eight booster boxes and stays fresh (`IsFresh`, no seed drawn) until anything goes in or out; the first change
makes them items, the i-th seeded with `ChildSeed(seed, i)`; the last in comes out first, so the first out gets
`ChildSeed(seed, 0)`.
While a box stands free in the world with its flaps open, a card, a booster or a sealed booster box nothing was taken
from that lands on its top goes in where the packer finds room (`TryPut`, from a shape query over the box's top every
physics frame); what does not fit stays where it lies, and a shut box takes nothing in. Items come out last in, first
out (see Moving items: one rule): a click on an open box standing in the world takes its last item into the hand, and
Shift+click as many as the hand has room for; with the open box in hand, a click on a shelf level stocks the last item
that level takes, and a click on nothing sets the last item down; the restocker's `CardShelf.TryUnpack` (`MoveInto`)
moves items into every free seat that takes one, nothing spilled. Taking
out an item from under others packs the later ones again, and one that no longer fits is set down beside the box. An
open box shows what it holds (`CartonView`: a still copy of each container and one block for each of the four card
stacks, built again on every change, colliding with nothing). An emptied box stays in hand until the player throws it
in the trash can (`TrashCan`, `World/RightStorage/TrashCan`), which takes only an empty cardboard box. A cardboard box is
no record, so it moves on and off a rack level as a body. A save stores a box's list
under `Contents` once it changed, and a fresh box saves none and reloads fresh (`SealedBoxSaveTest`).

**Opening a booster in hand.** A booster pack joins the hand's fan beside any cards held (a booster box or cardboard box
needs an empty hand). The right button on a booster pack in front opens it (`CardContainer.OpenInHand`, run by
`BoosterReveal` and timed by the Godot-free
`BoosterRevealPlan`, under two seconds): the booster comes to the middle of the view, its strip tears off, the eight
cards rise out one at a time into a fan (`CardFan`, the same pose math as the hand's fan), the empty wrapper shrinks
away, and after a short look the cards slide into the hand's fan after the cards already held, in the order they left
the booster. The cards come from the booster's seed (`CardGenerator().OpenBooster()`),
spawned under the camera, frozen and with their `CardCollision` off, so they never touch the world. A booster opens
only while the hand has room for its eight cards once the booster has left it (`IHand.FrontBoosterFits`). While it runs
the hand is busy (`IHand.IsOpening`): it takes, gives and uses nothing, no click is routed, and no key chips show. A save finishes the reveal first (`SavedContainers.FinishOpenings`),
so its cards are saved in the hand and no booster is stored.
