# Shop Scene, Licensed Assets and Signs

Read this when you change `ShopScene.tscn`, its roof, street or furniture, `ShopArtSlot`, or text shown in the world.
Moved out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

## Shop Scene and Licensed Assets

`Scenes/Gameplay/ShopScene.tscn` is the graybox shop (a storefront at x 0..16, z 0..12; one storage and backoffice room along its back and right sides, 24 m wide at the back; a road door in the right arm at x 18.05 that joins the room to the street; and a locked expansion lot to the left of the storefront, sealed by the single wall at x = 0). `Scenes/UI/MainMenu.tscn` is the main scene, so the game starts at the main menu, which opens `ShopScene.tscn` on a save slot (see Main Menu and Options in `docs/UI_AND_HUD.md`). `Scenes/Gameplay/StartScene.tscn` is a legacy scene and is no longer the main scene. Wall, floor, door and counter art comes from the licensed Synty packs. Only the Synty files exported through the Asset Manager are committed, and only in the private submodule, never in this repo:

- To add Synty files, export them through the Asset Manager tab (export roots are in `project.godot`), open the project in Godot so it imports the FBX files, then commit the new files and their `.import` files inside `Assets/` first and the `Assets` pointer here second.
- Each shop issue exports its own assets.
- `ShopArtSlot` nodes load a pack mesh at runtime and hide their graybox placeholder. Without the import the scene still loads and shows the placeholders. The script is a `[Tool]`, so the editor viewport shows the real art too; the placeholder is restored around every editor save (`NotificationEditorPreSave` / `PostSave`) so no scene ever stores it hidden, and the loaded `Art` child has no owner and is never saved. Changing `ArtPath` or `ArtScale` in the inspector reloads the preview. The pack FBX files have no usable texture and are single-sided, so a slot applies its pack's colour atlas on a double-sided material (`PackMaterialCatalog` maps a pack folder to its atlas, both the `res://Assets/Synty/` packs and the Asset Manager's exports under `res://Assets/Models/`, which keep the library's folder layout; add a new pack there, and `ShopArtMaterialSceneTest` fails when a Synty model in the shop is in no known folder). A slot's `Tint` is multiplied into that atlas (the shop's walls are cream and its floor tiles warm); slots of one pack and tint share one material. `ArtOffset` moves a pack model authored far from its origin back onto the slot (it is not scaled by `ArtScale`). The storage rack shows `SM_Rack_B` (PolygonSurvivalWorkshop), the product shelf `SM_Food_Shelf_02` (PolygonSurvivalConstruction) and a furniture box `SM_Cardboard_Box_01_Closed` (PolygonSurvivalTools). Collision is hand-authored and independent of the art.

**Roof, lights and sign.** `Scenes/Shop/ShopRoof.tscn` (instanced as `World/Roof`) closes the building: a ceiling of world-space tiles (`Shaders/ceiling_tiles.gdshader`) at 3.5 m, the top of the wall art, so no gap shows above a wall; a roof slab; a fascia with the shop name over the street door; and the ceiling light fixtures (an emissive panel and one shadowless `OmniLight3D` each) that light every room, the expansion lot included. The roof covers the whole footprint x -8..24, z -8..12; a room or lot added outside it needs the ceiling and a fixture grid extended. The ceiling collides on the default layer but is not in `shop_navigation`, so the navigation bake never walks on it. `ShopRoofSceneTest` fails when a floor tile lies outside the ceiling. The shop sign (`World/Storefront/ShopSign`) is a lit two-sided board on a post by the door (a screen `SignBoard`) that reads OPEN in green or CLOSED in red; the click prompt says what a click does. Every aim highlight (sign, counter, PC, price tags, boxes, packs, boosters) uses `Data/Ui/AimHighlightMaterial.tres`, a box drawn as glowing edges (`Shaders/highlight_edges.gdshader`), so the object inside stays readable.

**Street and neighbourhood.** `Scenes/Shop/Neighbourhood.tscn` is visual only: it has no collision. Its `ShopArtSlot` nodes load the POLYGON Mini City pack from `Assets/Synty/PolygonMiniCity`, and each slot keeps a `BoxMesh` placeholder that shows only while the pack file is missing, so the scene loads and the layout holds without the pack. The kerb is the one box primitive that is not a slot (the sky is set in `ShopScene.tscn`). The road, kerb, far sidewalk and facades lie across the road from the storefront, a neighbour flanks each side, and the scenery sits outside the street bounds and the workshop footprint. `NeighbourhoodSceneTest`, `NeighbourhoodArtSceneTest` and `NeighbourhoodScenerySceneTest` enforce this.

**Furniture.** The PC desktop's Furniture app (a second `StockApp` titled Furniture, catalog
`Data/Shop/FurnitureOrderCatalog.tres`) sells four pieces: `shelf_unit` (card shelf), `product_shelf`, `storage_rack` and
`bulk_packing_station`. Each is delivered as a `FurnitureBox` (`ShelfBox.tscn`, `ProductShelfBox.tscn`,
`StorageRackBox.tscn`, `PackingStationBox.tscn`; group `furniture_boxes`). Interacting with a box hands it to
`FurniturePlacementController`, which shows a `FurnitureGhost`. The keys T (turn), E (confirm), C (cancel), G (pick an
aimed piece up to move it) and B (pack an aimed piece back into its box where it stood, `FurnitureBoxSpawn`) work the
furniture; G and B refuse a shelf that still holds stock. A confirmed piece becomes a `PlacedFurniture` under
`World/Furniture`, and `FurnitureProbe` holds its physics checks. Its aim ray excludes `CameraRay.OwnBodies(camera)`
(`Scripts/Core/Utilities`), the bodies the camera hangs under, because the player's capsule is on the furniture layer
and the camera sits on its top; the e2e view's `looking_at` ray (`AutomationView.LookHit`) excludes them the same way. A piece stands only on the shop's floor rooms
(`FurnitureFloor.Shop`, a `FurnitureFloorArea` of the storefront, the back room and the right arm, widened by every owned
expansion lot). The starting card shelf, product shelf and storage rack are `PlacedFurniture` too
(`World/Furniture/CardShelf`, `ProductShelf` and `StorageRack`), so they move, pack and save like bought pieces. The
product shelf stands against the left wall at x 1, facing into the room, clear of the checkout queue lane
(`ShopSceneStockContractTest.NoStartingFurnitureStandsInTheCheckoutQueue`). Each
change rebakes `ShopNavigationRegion`. Its first bake waits for the scene root's `Ready`, because a placed piece
builds its footprint box in its own `_Ready`, after the region's (`TheFirstBakeLeavesNoFloorUnderTheStartingFurniture`). A piece being moved stays in the tree, hidden, off the collision layer and out of
the physics space (`PlacedFurniture.SetCarried` sets `ProcessMode` to Disabled, which removes it and every body, slot
and price tag under it from the space), so the ghost can go back down on the spot the piece left and a save during the
move keeps it at its old pose. A delivered box is lifted the same way for its placement (`FurnitureBox.SetCarried`), so
its piece can stand where the box stood; a cancel sets the box down again, a confirm frees it. `FurnitureSaveCodec` skips a
record it cannot read with a logged error and keeps the rest. The Stock app sells only boxes, packs and boosters.

## Signs and World Text

Text in the world is printed on something, never floating in the air or turned to the camera. `SignBoard`
(`Scripts/Features/Signage`) is a board with its text printed flat on the face: fixed to a wall, hung on two rods
(`Mount` Hanging, `MountLength` up to the ceiling) or stood on a post, one- or two-sided, and as a `Screen` a dark
panel whose text glows (the register display, the OPEN/CLOSED sign). Setting `Text` or `TextColor` repaints both
faces; the other exports rebuild it. It builds its board, faces and mount as internal children, which no scene stores,
so a scene holds only its exports; it is a `[Tool]` so the editor shows it, and it changes no saved node.
`NoFloatingTextSceneTest` fails on a billboard `Label3D` in any scene outside `Scenes/Debug`.
