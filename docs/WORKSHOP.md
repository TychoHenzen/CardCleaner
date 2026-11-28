# Workshop

Read this when you change the workshop, its layout, seams or cabinet. Moved out of CLAUDE.md, which keeps only the rules
most tasks need. Notes about this area belong here.

The workshop (`Scenes/Components/Workshop.tscn`, instanced in `ShopScene.tscn`) is one 40 x 40 m warehouse: the cabinet room in the centre, a 3.2 m hallway or hall leaving each of its sides, ordering rooms (order office, delivery bay) to the west and build rooms to the east. The backoffice seam (a `PortalDoorway`, `Scenes/Portal/PortalDoorway.tscn`) only shows while the player stands in the backoffice holding a special card (press 4 in the shop to spawn one) and leads to `WorkshopEntry`; the always-open `ReturnSeam` on the cabinet room's south wall leads back to `World/Markers/ShopReturn`. `ReturnSeam` is a node of `ShopScene.tscn` (a `PortalDoorway` instance under `World/Workshop`), not of `Workshop.tscn`, because the shop decides where the door leads.

The walls are a 0.2 m `GridMap` (`WorkshopGrid`) baked from the spec in `Scripts/Features/Workshop/Models/WorkshopHallLayout.cs`. To change the layout, edit the spec, then press "Bake Layout" on the `WorkshopGrid` node in the inspector and save the scene; `WorkshopGridSceneContractTest` fails while the baked cells differ from the spec. Section markers under `WorkshopGrid/Sections` carry each room's sign (a hanging two-sided `SignBoard` that faces along the doorway the player enters through from the cabinet room) and light.

The cabinet, holders, button and wires are `Scenes/Workshop/CabinetAssembly.tscn`, shared by the workshop's cabinet and the orderable `ArcadeCabinet`. The map screens stay on the workshop cabinet only, because they are wired to its `DeckbuilderController`.
