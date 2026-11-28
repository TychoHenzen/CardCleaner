# Game Session Flow

Read this when you touch `GameSessionService`, exploration or combat in `Scripts/Features/Deckbuilder`. Moved out of
CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

The `GameSessionService` manages the game loop through states: `WaitingForCards` -> `GeneratingMap` -> `Exploring` ->
`InCombat` -> `GeneratingLoot` -> `SessionComplete`. Key supporting systems:

- **GridMapGenerator**: Creates tile-based maps influenced by card signatures
- **ExplorationAI**: A* pathfinding for autonomous exploration
  - Operates in two modes: `FrontierExploration` and `PathToEnemy`
  - Emits events for path updates, visited tiles, player movement, and enemy encounters
- **FrontierExplorationBehavior**: Intelligent exploration using frontier expansion
  - Tracks seen vs. visited tiles using visibility checking
  - Automatically marks trivially visible tiles as visited
- **IVisibilityChecker**: Interface for line-of-sight calculations with configurable vision range (default 5)
- **SimpleCombatSystem**: Turn-based combat using card signatures

**Debug Visualization**: Overlay layer system allows non-destructive visualization of exploration data (current path, target tile, visited tiles).

**Key Exploration Events**: `PathUpdated`, `VisitedCellsUpdated`, `PlayerMoved`, `EnemyEncountered`, `EnemySpotted`
