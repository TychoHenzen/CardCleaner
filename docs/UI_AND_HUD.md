# Main Menu, Options and the Aim HUD

Read this when you change the main menu, save slot screens, options, the crosshair, click prompts or key hints. Moved
out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

## Main Menu and Options

`Scenes/UI/MainMenu.tscn` (`MainMenuController`) lists the save slots and starts the shop on one of them. New game opens `NewGameDialog`, which asks for the slot name with the field focused; Enter in it confirms, as OK does. The slot menu names only `ISaveSlotCatalog` (List, TryCreate, Delete over `SaveSlotInfo`), `ILegacySaveImporter` and `IActiveSaveSlot` (Activate; the shop's `GameSaveService` then loads the active slot). Options live in `user://settings.cfg`, separate from the slots. `GameOptions` is the pure model. `OptionsApplier.Apply(options, window)` drives the `Master`, `Music` and `SFX` audio buses and the window (engine-global, in any scene). It sends the window mode, size and vsync to the engine as a `DisplayPlan` (`OptionsApplier.DisplayFor`) through a display sink whose test constructor is internal, so fullscreen, resolution and vsync are proven headless by recording the plan. Options code never touches `user://saves` (`OptionsSlotIsolationTest`). `OptionsApplier.ApplyMouse(options, IGameSettings)` sets the per-scene mouse values, and the shop's `MouseOptionsStartup` node applies the saved mouse options to the shop's `GameSettings` when the shop starts. The pause menu's Options button opens the same options screen (`Scenes/UI/OptionsMenu.tscn`), and its Quit to menu button saves the active slot and then changes to the main menu.

## Aim HUD and Control Hints

The player scene (`Scenes/Components/player.tscn`) has an `AimHud` CanvasLayer (layer 5) holding `InteractionHud`
(`Scripts/Features/Player/Ui`): a crosshair dot, a gold ring while something is aimed at, the click prompt under it
(`LMB` plus `IInteractable.Prompt` of `InteractionSystem.Target`) and a row of key chips at the bottom of the screen.
It hides while any other owner holds `ICursorOwner` (a menu, the PC, the payment screen). `InteractionSystem.Target`
is only an interactable a click would use now: it is judged again on every 0.05 s scan (alive, `CanInteract`, within
`InteractionRange`), so the prompt never offers a click that does nothing. An interactable names its click in
`Prompt` (default "Use"); a card's reach is 4.5 m. A target the hand router claims (`IClickRouter`, see Moving items: one
rule in `docs/CONTAINERS_AND_HAND.md`) is judged, prompted and clicked through the router's plan for the point the aim hit (`InteractionSystem.AimPoint`)
instead of its own `CanInteract`, `Prompt` and `Interact`. The key chips come from `IControlHints` (`ControlHintBoard`, a
Godot-free `[Service]` in `Scripts/Core/Services`): each source publishes its own list (`Publish(source, hints)`, an
empty list or `Clear(source)` removes it) and the board shows them in publishing order. The sources are
`CardHighlighter` (the hand, `HandHints`: what the right button and Q do to the front item, the wheel once more than one
item is held, the strap key once two cards are held, Shift+click when it would take more than one item, and on an open
cardboard box the click that sets one item down or, once it is empty, the trash can; none while a booster opens in
hand) and `FurniturePlacementController` (`PlacementHints`). Shift and the wheel reach a mouse action through
`MouseActionArgs` (pressed, shift) from `IInputService`; a wheel event is sent as a press and then a release. A new held-item mode publishes its keys the same way and clears them in `_ExitTree`.

While any owner holds a cursor claim, `CursorOwner` sets `IInputService.ActionsSuspended`, so a key or click made on a
menu, the PC or the payment screen never also acts in the world behind it (no card is thrown, no piece placed). Releases
still reach mouse actions, and raw events and the polled movement are not gated. `CursorOwner` clears the flag when it
leaves the tree, because the input service lives in the app scope and outlasts the scene.
