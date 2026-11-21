# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Players who want one loop made of two halves of equal weight: running a trading-card shop in first person, and
sending the cards they collect into machines that fight on their own. Neither half is the frame for the other. The
shop brings cards in, and the card side turns them into loot that goes back into the shop.

## Product Purpose

Octarine Codex is a single-player PC game. The player owns a card shop: they order stock on the backoffice PC, stock
and price shelves, open booster packs in hand, ring up customers at the checkout, pay the day's bills at closing, hire
staff (a cashier, a restocker) and buy lots to grow the store. In the workshop behind the shop, cards go into arcade
cabinets. A cabinet builds a map from its cards' signatures, explores it, fights what it finds without the player
steering, and pays out loot cards.

Open decision: what success means for a player (a win condition, an end state, a long-term goal) is not decided yet.

## Positioning

- **The card game is not a card game.** It plays like an auto-battler, and the goal is for it to become more like an
  idle ARPG than a card game. The player's choices are which cards, which machines and how they are wired, not
  turn-by-turn play.
- **Card signatures drive everything.** Every card carries an 8-element signature (Solidum, Febris, Ordinem, Lumines,
  Varias, Inertiae, Subsidium, Spatium, each from -1 to 1). It sets the card's rarity, look, flavor sentence and
  market price, and the maps, fights and loot it generates. Nothing about a card is authored by hand.
- **Everything is a physical object.** Every card, booster pack, booster box and cardboard box is an object the player
  holds, fans out, stacks, opens and shelves in first person. Text in the world is printed on something.
- **Wirable machines.** Arcade cabinets are built from card slots, buttons and screens that the player buys and wires
  together with cables, so several machines run side by side. One workshop cabinet exists today; the device and cable
  system is planned (`docs/MODULAR_DEVICE_ARCHITECTURE.md`, design phase).

## Operating Context

- Played on Windows with mouse and keyboard, in first person, in a 3D shop: the storefront, a storage and backoffice
  room, a street outside, locked expansion lots, and a workshop warehouse reached through a seam in the backoffice
  wall.
- The day is the rhythm: open the shop, serve customers, close, settle bills (rent, lot rent, wages, carried debt),
  read the day report, save.
- Screens the player meets: the main menu with save slots, options, pause menu, the aim HUD (crosshair, click prompt,
  key-chip hints), the shop PC used fullscreen as a desktop ("Card Shop Manager", with the Stock, Furniture,
  Expansions, Staff and Bills apps), the pricing screen, the checkout payment screen (card keypad, cash change picker),
  the end-of-day report, and the map and combat screens on a cabinet.

## Capabilities and Constraints

- Built in Godot 4.7.1 with C# on .NET 8. `web` above is the closest platform value Impeccable supports; there is no
  browser build. The UI is Godot scenes (`.tscn`) and C# Controls themed by `Data/Ui/GameTheme.tres`, which the
  Impeccable detector and live mode cannot read. Review game UI by reading those scenes and the in-game screenshots
  from `node tools/e2e/e2e.mjs`.
- Terminology the player reads: a "cardboard box" holds stock, a "booster box" holds 8 booster packs, a "booster pack"
  holds 8 cards. Rarities are Common, Uncommon, Rare, Epic and Legendary, counted from a card's pure elements.
- World text is printed on boards, screens and packaging, never floating or turned to the camera.
- The game is not released and has no players yet.
- Open decisions: the win condition or long-term goal; how far the idle ARPG direction reshapes the card side.

## Brand Commitments

- The game is named Octarine Codex. The in-world card brand is "OCTARINE CARDS", printed on every booster, booster box,
  cardboard box and the shop's fascia.
- Art comes from licensed Synty packs and other assets kept in the private `Assets/` submodule. Licensed files never
  enter this repository.

## Evidence on Hand

- In-game art, models and card visuals in the private `Assets/` submodule.
- The rendered game can be driven and screenshotted on a hidden desktop with `node tools/e2e/e2e.mjs`.
- None found in the repository: screenshots for marketing, a store page, trailers, testimonials, press, player counts
  or reviews. Future work must not invent them.

## Product Principles

1. One loop: the shop and the cards feed each other, so a feature on one side hands something to the other.
2. Signatures, not authoring: a card's qualities come from its signature, so new content is new rules, not new rows.
3. Physical first: stock is held, placed and opened as objects, and information is printed on things in the world.
4. Hands off the fight: the card side plays itself; the player's skill is in what they feed it and how they build it.
