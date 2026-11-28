# Feature Organization

Read this to find which `Scripts/Features` folder holds a feature, and when you touch decks or piles. Moved out of
CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

Code is organized by feature under `Scripts/Features/`:
- **Card/**: Card models, controllers, components, and services (spawning, generation, rarity and flavor text, see
  Card Rarity and Flavor Text in `docs/CARDS.md`). A card on the floor offers no pick-up while the hand is full.
- **Hand/**: The player's one hand (`Hand`, `IHand`), its click router (`CardHighlighter` with `HandClickPlans`, see
  Moving items: one rule under Container Meshes and Opening in `docs/CONTAINERS_AND_HAND.md`) and the Godot-free rules both use: `HandItems` (order,
  front item, capacity), `ItemTransfer` (the one move rule), `TakePolicy`, `TransferPrompt` and `HandHints`. See The
  hand under Container Meshes and Opening in `docs/CONTAINERS_AND_HAND.md`.
- **CardStacks/**: Decks and piles, both a `CardBundle` (one `RigidBody3D` in group `card_bundles` that keeps every
  card as a `StoredCard`, in the order they went in, and is an `IItemStore`). A deck is drawn as one block with a strap
  (`CardBundleLook`). `CardBundler` (in `player.tscn`) straps the hand's cards into a deck on V (`card_strap`, at least
  `CardBundleRules.FewestToStrap`, 2, cards, with no drop aimed) and sets it down in front of the player; a click on a
  deck unstraps it into the hand only when every card fits, in order. A pile is a dome that grows with its count
  (`CardBundleRules.PileRadius`, `PileHeight`) and stays a pile until empty; a plain click takes its top card and
  Shift+click as many as fit in the hand, last in first. A pile has a join area (`JoinArea`, an `Area3D` on no layer
  that watches the card layer, a few centimetres larger than the dome): a free card that touches it (`LooseCards.IsFree`:
  alive, on a collision layer, under no slot and no camera) joins the pile on the next physics frame, without waiting to
  come to rest (`CardPileTest`). `CardPileGatherer` (`CardPiles`, under the shop's scene root) runs every 0.5 s: more
  than `CardBundleRules.MostLooseCards` (100) loose cards at rest linked within 0.5 m (`CardClusters`) become one pile
  where they lay, a card at rest beside a pile joins it, and two piles that run into each other pour into the older one,
  the newer pile's cards laid after the older's. A pile shows a cream mound and its newest `PileLook.FacesShown` (64)
  cards lying face up on it, each at a pose from the Godot-free `PileLayout`: card i lies on the dome the pile had at
  i + 1 cards, at a spot and turn drawn from the pile seed and i alone, so a join adds one node, a take frees one, and
  no other card moves. The pile seed is fixed when the pile is made (`PileLayout.SeedFor`, from the cards it starts
  with, never from a shared random source), kept by a merge, and saved with the pile (`Seed` in its `SavedBundles`
  record), so a reload lays the same heap. Each shown card is a `CardFace` dressed from its record's signature and look
  seed, the same face a loose card shows. A pile costs about 0.65 ms a frame whatever its size, because it draws at
  most `FacesShown` faces (an empty view took 0.59 ms, 4 piles 3.7 ms and 9 piles 6.2 ms, with vsync off); `Tests/Features/CardStacks/Probe/PileFrameCostProbe.tscn` measures that through the e2e
  driver (`call Build [piles, cards]`, `call Measure [frames]`, then `get FrameMs` and `GpuMs` once `Done`).
- **Deckbuilder/**: Game session management, map generation, combat, exploration
- **Worldgen/**: Procedural generation support (auto-tiling, biomes, gradients, blob generation)
- **Player/**: Player controller and interaction system
- **Packs/**: Card containers (a cardboard box that stores booster boxes, boosters and cards, a pack of 8 boosters, a booster of 8 cards, which the player reads as cardboard box, booster box and booster pack; see Container Meshes and Opening in `docs/CONTAINERS_AND_HAND.md`); `CardPackGenerator` makes each card special with probability 1/200 (about 92% of boxes hold at least one) and special cards show their signature along the card edges
- **Conveyor/**: Conveyor belt mechanics
- **Portal/**: Backoffice wall seam that opens to a doorway and teleports the player (pure rules in `Models/`)
- **Pause/**: Pause menu controller
- **Shop/**: The shop game, one folder per topic with that topic's components, rules, services and screens together:
  `Building/`, `Checkout/`, `Customers/`, `Day/`, `Expansion/`, `Furniture/`, `Ordering/`, `People/`, `Pricing/`,
  `Progression/`, `Restocking/`, `Screens/`, `Shelves/` and `Staff/`. A topic's namespace is its folder
  (`CardCleaner.Scripts.Features.Shop.Checkout`) and its tests mirror it under `Tests/Features/Shop` and
  `UnitTests/Features/Shop`; test helpers that several topics share stay in the root of those folders. A topic folder
  never takes the name of a class in it (`Restocking`, not `Restocker`): a namespace of that name hides the class.

Core utilities and interfaces are in `Scripts/Core/`.
