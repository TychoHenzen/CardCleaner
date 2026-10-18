using System.Diagnostics;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using Godot;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Shared harness for the workshop suites: instantiates the shop scene, hands it a card spawner that builds
/// the same card bodies the shop tests use, and exposes the cabinet parts so each suite only states its scenario.
/// </summary>
public sealed class WorkshopSceneRig
{
    public const int SettleFrames = 6;

    // Frames to wait for the irregular map, which generates off the main thread, to show.
    public const int MapFrameBudget = 300;

    // Rings of the irregular map the workshop suites play: small, so a session runs to its end within seconds.
    public const int SmallMeshRings = 3;

    // Wall-clock budget for one irregular session on the small map, run at SessionTimeScale.
    public const double SessionBudgetSeconds = 60;

    // Exploration steps take 0.1 s, move animations 0.15 s and combat turns 1.0 s of game time; the suites run game
    // time faster instead of changing those rules. Their teardown sets the time scale back to 1.
    public const double SessionTimeScale = 10;

    private WorkshopSceneRig(Node3D shop)
    {
        Shop = shop;
        Workshop = shop.GetNode<Node3D>("World/Workshop");
        Entry = Workshop.GetNode<Marker3D>("WorkshopEntry");
        Cabinet = Workshop.GetNode<StaticBody3D>("Cabinet");
        DeckHolder = Workshop.GetNode<DeckSlot>("Cabinet/Assembly/DeckSlot");
        CardHolder = Workshop.GetNode<DeckSlot>("Cabinet/Assembly/CardSlot");
        Button = Workshop.GetNode<InteractableButton>("Cabinet/Assembly/Button/StaticBody3D");
        KindButton = Workshop.GetNode<InteractableButton>("Cabinet/MapKindButton/StaticBody3D");
        Controller = Workshop.GetNode<DeckBuilderController>("DeckbuilderController");
        Screen = Workshop.GetNode<IrregularWorldMapScreen>("Cabinet/IrregularTileMapScreen");
    }

    public Node3D Shop { get; }
    public Node3D Workshop { get; }
    public Marker3D Entry { get; }
    public StaticBody3D Cabinet { get; }
    public DeckSlot DeckHolder { get; }
    public DeckSlot CardHolder { get; }
    public InteractableButton Button { get; }
    public InteractableButton KindButton { get; }
    public DeckBuilderController Controller { get; }
    public IrregularWorldMapScreen Screen { get; }

    public static async Task<WorkshopSceneRig> Create()
    {
        var rig = new WorkshopSceneRig(GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>());

        // The test scene is not the current scene, so hand the services the cabinet needs over directly.
        TestServices.Reset();
        // The scene's GameSessionService composes from the factories when it enters the tree, so the game's
        // [Service] classes go in first; explicit registrations below replace any scanned type they share.
        TestServices.RegisterGameServiceClasses(TestServices.Container);
        TestServices.Container.RegisterSingleton<ICardSpawningService>(new TestCardSpawner());
        TestServices.Container.RegisterSingleton<ITileRegistry>(new TileRegistry(new GodotAssetPaths()));
        TestServices.Container.RegisterSingleton<IMeshMapGenerator>(MeshMapGeneratorFixture.Create(SmallMeshRings));
        TestServices.Container.RegisterSingleton<IGameSessionService>(
            rig.Workshop.GetNode<GameSessionService>("GameSessionService"));

        AddNode(rig.Shop);
        await Settle();
        return rig;
    }

    /// <summary>
    /// Presses the cabinet's map kind toggle until the controller has the wanted kind; the scene starts on the grid.
    /// </summary>
    public void PickKind(MapGenerationType kind)
    {
        for (var press = 0; press < 2 && Controller.MapType != kind; press++)
            KindButton.Interact();

        AssertThat(Controller.MapType).IsEqual(kind);
    }

    /// <summary>Drops a card on top of the holder, the way a hand releases one.</summary>
    public async Task<CardController> Place(DeckSlot holder, CardSignature signature)
    {
        var card = ShopTestCards.Create(signature);
        Workshop.AddChild(card);
        card.GlobalPosition = holder.GlobalPosition + holder.PositionOffset;
        await Settle();
        return card;
    }

    public static async Task Settle()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    /// <summary>
    /// Waits, at most <see cref="MapFrameBudget"/> frames, for the irregular map to show, and fails when it does not;
    /// it generates off the main thread, so it can arrive after <see cref="Settle"/>.
    /// </summary>
    public async Task WaitForMap()
    {
        for (var frame = 0; frame < MapFrameBudget && !Screen.IsInitialized; frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertBool(Screen.IsInitialized)
            .OverrideFailureMessage($"The irregular map did not show within {MapFrameBudget} frames")
            .IsTrue();
    }

    /// <summary>
    /// Runs the session the cabinet started to its end at <see cref="SessionTimeScale"/>, and fails when it does not
    /// complete within <see cref="SessionBudgetSeconds"/>.
    /// </summary>
    public async Task RunToCompletion()
    {
        var session = Workshop.GetNode<GameSessionService>("GameSessionService");
        var completed = false;
        void OnStateChanged(SessionState state) => completed |= state == SessionState.SessionComplete;

        session.StateChanged += OnStateChanged;
        Engine.TimeScale = SessionTimeScale;
        var clock = Stopwatch.StartNew();
        while (!completed && clock.Elapsed.TotalSeconds < SessionBudgetSeconds)
            await ISceneRunner.SyncProcessFrame;

        Engine.TimeScale = 1;
        session.StateChanged -= OnStateChanged;
        AssertBool(completed)
            .OverrideFailureMessage(
                $"The session did not complete within {SessionBudgetSeconds} s; last state {session.CurrentState}")
            .IsTrue();
    }

    private sealed class TestCardSpawner : ICardSpawningService
    {
        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
        {
            var card = ShopTestCards.Create(signature);
            parent.AddChild(card);
            card.GlobalTransform = spawnTransform;
            return card;
        }

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) =>
            SpawnCard(new CardSignature(), spawnTransform, parent);

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }
}
