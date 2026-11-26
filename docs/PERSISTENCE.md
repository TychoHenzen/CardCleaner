# Persistence

Read this when you add saved state or change saving, loading, save slots or a saved node. Moved out of CLAUDE.md, which
keeps only the rules most tasks need. Notes about this area belong here.

Uses the Saveable addon (`addons/saveable/`) with Newtonsoft.Json. Implement `ISaveable` interface with `UniqueID`, `Load(NodeSave)`, and `Save(NodeSave)` methods. The shop's `GameSaveService` (`Scripts/Features/SaveSlots/Services/SaveGame`, with the `Saved*` classes that write and read the cards, containers, decks, piles and hand) autosaves the active slot on a timer and on the pause menu's Save button (`ISaveGameCommand.ManualSave`).

Save slots (`Scripts/Features/SaveSlots`) live under `user://saves/<slot>/save.json` (`SaveSlotLayout`). Each file is a `SaveEnvelope` at version 1. `SlotStore` writes `save.json.tmp` first and renames it into place, so a crash leaves the previous save intact. `SlotStore` also implements `ISaveSlotCatalog`, and a slot without a readable `save.json` is listed as unreadable. Slot names (`SaveSlotName`) are 1 to 32 characters of letters, digits, spaces, `-` and `_`, and cannot be `con`, `prn`, `aux` or `nul` (compared without case). `ActiveSaveSlot` starts on the `default` slot until one is selected. `LegacySaveImporter` copies the old single-file `user://game_save.json` into a slot named `imported`, and runs only when no slot exists yet and that file holds a JSON object. A first save that fails removes the slot it started. A booster still opening in the hand when the game saves is finished first, so its cards are saved instead of it. A `CardContainer` draws a content seed only when a save asks for one (`SeedForSave`); the seed is saved with the slot, so the same seed opens the same cards after a reload, and an unseeded booster box or booster opens from the shared random source as before. Each later stateful slice adds its own saved state to the envelope and a round-trip test that saves, reloads and compares it. Prices live with the stock, never with a price tag: a card's own price (`CardController.ShopPrice`) is saved with the card (`price` in its record) and a booster's or pack's (`CardContainer.ShopPrice`) with the container (`Price` in its `SavedContainers` record), and each comes back with its item into its slot. A save that still holds an old tag markup (an id starting `slot_price:`) loads with that entry ignored, so its stock asks the market price, and a corrupt price logs a warning and falls back to the market price.

Decks and piles save under `bundles` in `GameSaveService`'s record (`SavedBundles`): each with its kind, pose and every
card in order (and a pile its seed, so it reloads the same heap), a card written the same way a cardboard box writes one (`SavedStoredItems`, which both share). A load
parses every record before it frees anything, then replaces the scene's decks and piles with the saved ones under the
current scene's root (`CardBundleSaveTest`). A record with an unknown kind, no cards or anything but cards fails the
load, which logs the error.

Furniture saves under UniqueID `furniture` (`FurnitureSaveService`) as one JSON string of `FurnitureRecord` entries: every
placed piece with its pose and node name, the scene's starting pieces included, and unopened furniture boxes with their
position. Every save sets `whole_layout`. A load of such a save makes the furniture match the records: a standing piece
whose record has its name and item is kept and moved to the saved pose (so its price tags and stock load onto the same
nodes), a record with no such piece makes a new one, and a standing piece no record keeps is freed. An older save without
`whole_layout` leaves the starting pieces standing and adds its records beside them. Boxes are always replaced, and the
navigation mesh is rebaked once. A save without the key leaves the scene as authored, and a record with an unknown id or a
non-finite coordinate is skipped with an error. A record whose pose no longer fits (a piece that grew since the save, a
lot not owned, an overlap) logs an error and comes back as its box on a free cell of the delivery area
(`FurnitureBoxSpawn.OnDeliveryArea`), so no update deletes a piece the player bought. `FurnitureSaveService` is an `ILoadsFirst`
(`addons/saveable`), so `SaveSystem` loads it before the rest of the tree and the restored shelves are in the tree when
their price tags and cards load. A restored piece keeps its saved name under `World/Furniture`
(`PlacedFurniture.StandUnder`; a new piece takes the first free `Furniture<n>`), which keeps those node-path save ids
stable, and restored boxes are tracked by the `DeliveryMarker` so a new order avoids them. Stock on a placed shelf
(cards and containers) is saved by `GameSaveService`, not in the furniture record, at the seat it is locked on.

A card or container saved on a slot's seat (`DeckSlot.SeatAt`, the nearest seat within 5 cm) is locked back into that
slot while the save loads (`SavedContainers.Restore`, `SavedCardRespawner`), before any physics step, lower seats first.
The slot's trigger cannot do it: a seat can lie inside its shelf's solid collider (the product shelf's does), which
pushes a free body off the seat, and out of the trigger, on the first step.
`ShelfStockSaveTest.StockIsBackInItsSlotsBeforeAnyPhysicsStep` pins it. The load first takes the scene's own cards and
containers out of the tree at once (`RemoveChild`, then `QueueFree`), and a slot releases any locked body that leaves the
tree, so a card-shelf slot, which holds one card, is free again before the saved copy of its card respawns
(`GameSaveServiceLoadTest.ReloadingPutsTheSavedCopyOfAShelvedCardIntoItsOneCardSlot`).

The starting product shelf and storage rack used to stand under `World/Storefront/ProductShelf` and
`World/RightStorage/StorageRack`, so a save from that layout keys their price tags by those paths and keeps their stock
at the old seats. `GameSaveService.LoadGame` passes every payload through `LegacyShelfSaveMigration.Migrate` first:
when the payload holds a tag price id under an old path (which only a save from that layout has), a container saved
within 0.25 m of an old seat moves to the same slot's seat on the moved shelf, which locks it on load. The old price
ids stay in the payload, and the load ignores them. A payload with no such id loads unchanged.

The game is not released, so no player has an old save to protect. A later change that moves or renames a saved node,
or moves a seat by more than 5 cm, gets no migration step: a save from before it may load that stock loose, or not at
all. New saved state still gets its round-trip test. `LegacyShelfSaveMigration` stays as it is.
