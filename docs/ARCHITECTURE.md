# Architecture

Read this when you add or resolve a service, touch `CompositionRoot` or `ServiceHost`, or add domain code that logs or
does math. Moved out of CLAUDE.md, which keeps only the rules most tasks need. Notes about this area belong here.

## Dependency Injection

The project uses a custom DI container with no static service state: an instance-owned host and a composition root:

- **ServiceHost** (`Scripts/Core/DependencyInjection/ServiceHost.cs`): a plain class that implements
  `IServiceResolver` and owns the container and the one pending-callback queue (`Container`, `Get<T>()`, `Has<T>()`,
  `Get<T>(callback)`, `ExecutePendingCallbacks()`, `Reset()`). It exists from construction, `Reset()` replaces its
  container and clears its queue but keeps the host, and it names no feature or composition type;
  `DependencyInjectionBoundaryTest` enforces that. It does not touch `ILog.Sink`.
- **CompositionRoot** (`Scripts/Composition/CompositionRoot.cs`): the "Services" autoload. It owns one `ServiceHost`
  (`Host`) and composes it in two scopes. The **app scope** (the plain `[Service]` classes of `CardCleaner.Scripts`,
  registered lazily, then the core services) is composed synchronously in the autoload's `_Ready`, before the main
  scene's `_Ready`, and lives for the whole run: the main menu resolves it directly, and the slot it activates on
  `ActiveSaveSlot` reaches the shop. The **scene scope** (every node in the `service_providers` group) is registered
  in the deferred flush after startup and on every `SceneTree.SceneChanged`, then the pending callbacks run. When the
  current scene leaves the tree (`ChangeSceneToFile` removes it at once), `ServiceHost.LeaveScene` drops its
  registrations, restores app registrations they replaced, forgets callbacks its nodes queued, and makes every
  scanned class built from a scene service (such as `DailyBillBook`) build again in the next scene
  (`ServiceContainer.SealAppScope` / `DropSceneScope`). A scene node that needs a provider-registered service still
  uses `Get<T>(callback)`; a scanned `[Service]` must never stand in for a node a scene provides, since a scene node
  could resolve it before the provider registers (`InstantCheckout` is therefore not scanned). It also sets
  `ILog.Sink` (a static property, because `ILog`'s static helpers have no instance) when it registers `ILog`;
  production never clears it, and only `TestServices.Reset` sets it back to null. Only that autoload composes and injects; any other instance logs an error
  and composes nothing.
- **ILog** (`Scripts/Core/Interfaces/ILog.cs`) names no Godot type: `ILog.Print` / `Warning` / `Error` go to
  `ILog.Sink` (the `LoggingService` the root installs, which prints through `GD`) and fall back to `Console.Out` /
  `Console.Error` while no sink is installed. Domain code logs through `ILog`, never `GD`. The node export check is
  the UI-layer static `NodeExports.ExportCheck` (`Scripts/Core/Utilities`).
- **CoreMath** (`Scripts/Core/Numerics/CoreMath.cs`): engine-free static math for domain code. Each member copies
  the GodotSharp 4.7.1 `Mathf` body of the overload callers bind to, and `CoreMathTest` compares each with
  `Godot.Mathf`. Domain classes call `CoreMath`, not `Mathf`; add a member only together with its first caller (an
  unused public member is a `dead-export` finding). It lives in `Core/Numerics`, not `Core/Math`, so the namespace
  does not shadow `System.Math`.
  A data struct that must stay pure data in the dependency gate (`CardSignature`) uses `System.Math` / `MathF`, not
  `CoreMath`, because a static function class is not data there.
- **ServiceContainer**: singleton registrations, plus constructor injection. `Construct<T>()` and
  `RegisterSingleton<TInterface, TImplementation>()` build a plain class through its public constructor with the most
  parameters, resolving each parameter from the container; a missing one throws `InvalidOperationException` naming
  the class and the service (`Cannot construct Foo: service IBar not registered`).
- **Lazy [Service] scan** (`ServiceClassScan.PlainServiceClassesIn` finds them, `RegisterServiceClasses` registers
  them): a non-abstract `[Service]` class that is not a Godot object is registered under its `[Service]` types (its interfaces when it names none). It counts as registered only once every
  parameter of its constructor is registered, so callbacks waiting for it stay pending until then; it is built once,
  on its first resolve, and shared by all its types. An explicit registration of the same type replaces it. `[Service]`
  nodes are never built by the container; `AutoServiceProvider` registers the live node.
- **Session factories**: `GameSessionService` is an `IDependent` that builds its per-session parts from two stateless
  registered factories, `ISessionFactory` (world and exploration session) and `ICombatSystemFactory` (one fight), found
  by the `[Service]` scan. UI code names the contracts `ISessionWorld`, `IExplorationSession` and `ICombatSystem`, never
  the concrete classes. The gate counts a static factory class as a concrete crossing, so none is used.
- **Tile catalog**: the map generators take an `ITileCatalog` (`Scripts/Features/Deckbuilder/Tiles`), which extends
  `IWfcTileCatalog`. The only concrete implementation is `TileCatalogData`, an immutable snapshot of the tiles in
  registry insertion order and their variation groups. `GridMapGenerator` and `MeshMapGenerator` are `[Service]`
  classes that the scan builds; `MeshMapGenerator`'s constructor that takes the ring count is internal.
  `TileCatalogData` is also a `[Service]`, with a public `TileCatalogData(ITileRegistry)` constructor that the scan
  builds once from the registered registry. `CompositionRoot` builds only the registry (`TileCatalogLoad.Load`) and
  the data it registers by instance. `TileCatalogConsumersTest` fails if a file under
  `Scripts/Features/Deckbuilder/Services` or `Scripts/Features/Worldgen/IrregularMesh` names `TileCatalogData`.
  The tile loaders (`TileCatalogLoad`, `TileDataLoader`, `TiledTilesetLoader`, `CompiledAtlasLoader`, `TileRegistry` and
  the TMX, Wang and atlas code behind them) live in `Scripts/Features/Deckbuilder/Tiles/Loading`, so `Scripts/Core`
  names no Deckbuilder type (`CoreDeckbuilderBoundaryTest`). The compiled atlas is loaded by the stateless
  `TileVisualLoader`, which returns a `TileVisualCatalog` that `TileRegistry` exposes as `Visuals` (UI users read the
  tileset path, atlas flag and `TilesetConfig` there). `AtlasMapping` holds no static cache: `Load` reads its file on
  every call, and `WorldMapComponents` passes the loaded mapping to `TerrainTransitions.Create`. The compiled
  atlas's TileSet (`CompiledAtlasLoader`, one per mapping file) creates its 25,700 transition tiles only when a cell
  first shows one (`CompiledAtlasTiles`), because creating them all took about 18 s: every cell painted on a world-map
  layer goes through `AtlasCells.Set`, never `TileMapLayer.SetCell`. `MeshStatistics` (a
  readonly struct) and `AutoTileFormatDefinition` (a sealed record) are immutable: every property is set once.
- **IServiceProvider**: Nodes implementing this interface and added to the `service_providers` group will have their services registered at startup
- **Tree injection**: a node implementing `IDependent` (`Scripts/Core/Interfaces`) gets `Inject(IServiceResolver)`
  from the autoload on `SceneTree.NodeAdded`: after the node's `_EnterTree`, before its `_Ready`, and again whenever it
  re-enters the tree (reparent). `Inject` only stores the resolver; the node does its wiring in `_Ready` through it.
  Nodes already in the tree when the autoload starts are injected once by a startup pass
  (`CompositionRoot.InjectExistingNodes`). The main scene has entered but is not ready then, so its nodes are
  injected before their `_Ready`; an autoload ordered before Services is already ready, so an `IDependent` must
  never be one.
  The injected resolver is the autoload's `ServiceHost`, which reads its current container at call time, so a
  resolver injected before a reset reads the replacement container and its `Get<T>(callback)` shares the one
  pending-callback queue.
- Services can use async resolution via `IServiceResolver.Get<T>(callback)` for services not yet registered

Nodes reach services only through the `IServiceResolver` they are injected with (`Get<T>()`, `Has<T>()`,
`Get<T>(callback)`); nothing in `Scripts/` looks a container up statically. Tests go through
`Tests/TestUtilities/TestServices`, which reads the autoload's host: `Container`, `Resolver` (the resolver the root
injects), `Reinitialize()` (composes the autoload again), `Reset()` (empties the container and the queue
and clears `ILog.Sink`), `RegisterGameServiceClasses(container)` (the root's `[Service]` scan into a container),
`ExecutePendingCallbacks()` (runs the queued `Get<T>(callback)` lookups, as the root does after composing) and
`Inject(node)` for a node built outside the tree. A unit test of the host itself builds one with `new ServiceHost()`.
