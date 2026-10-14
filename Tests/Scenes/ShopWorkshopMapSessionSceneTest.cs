using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Composition;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Both map kinds played end to end in the workshop on the one game loop: the shop scene's own registration, the kind
/// picked with the cabinet toggle, a map card dropped and the button pressed, then the session must run through
/// generation, exploration and combat to its end and lay out the loot cards. The kind toggle stays dark until the
/// reset press.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopMapSessionSceneTest
{
    // Wall-clock budget for the whole session, generation included (about 16 s of it for the grid on the dev machine).
    private const double SessionBudgetSeconds = 90;

    // Exploration steps take 0.1 s, irregular move animations 0.15 s and combat turns 1.0 s of game time, about 50 s
    // for the grid with this map card. The test runs game time faster instead of changing those rules; a step still
    // happens at most once per frame.
    private const double SessionTimeScale = 10;

    // The LootPoint sits 2.25 m left of and 2 m in front of the cabinet origin, and the row of at most 10 cards,
    // 0.5 m apart, runs 4.5 m to its right, so every card lands within about 3 m (XZ) of the cabinet origin. The
    // extra 1 m is margin for cards that drop from 1.5 m and settle.
    private const float LootReachMeters = 4f;

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private readonly List<Node> _coreServiceNodes = new();

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;

        // The core services the Services autoload composes in the game. Composing clears pending callbacks, so it
        // runs before the shop enters the tree; the shop's own providers then register as the game's do.
        CompositionRoot.ReinitializeServices();
        _coreServiceNodes.Add((Node)ServiceLocator.Get<IInputService>());
        _coreServiceNodes.Add((Node)ServiceLocator.Get<IPlayerResetService>());

        // The irregular kind plays on the small map the workshop suites use: the game's full-size map takes far longer
        // to explore than the budget allows. Registered before the shop enters the tree, so the cabinet resolves it.
        ServiceLocator.Container.RegisterSingleton<IMeshMapGenerator>(
            MeshMapGeneratorFixture.Create(WorkshopSceneRig.SmallMeshRings));

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _tree.Root.AddChild(_shop);
        _tree.CurrentScene = _shop;

        foreach (var node in _tree.GetNodesInGroup("service_providers"))
        {
            if (node is IServiceProvider provider)
                provider.RegisterServices(ServiceLocator.Container);
        }

        ServiceLocator.ExecutePendingCallbacks();
        await WorkshopSceneRig.Settle();
    }

    [AfterTest]
    public void Teardown()
    {
        ServiceLocator.ResetForTesting();
        _tree.CurrentScene = _previousScene;
        _tree.Root.RemoveChild(_shop);
        _shop.Free();
        foreach (var node in _coreServiceNodes.Where(GodotObject.IsInstanceValid))
            node.QueueFree();
        _coreServiceNodes.Clear();
        CardEffectNormalMapCache.ResetForTesting();
        Engine.TimeScale = 1;
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public async Task GridSessionReachesSessionCompleteAndSpawnsTheLoot() =>
        await PlaySessionToTheEnd(MapGenerationType.RegularGrid);

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public async Task IrregularSessionReachesSessionCompleteAndSpawnsTheLoot() =>
        await PlaySessionToTheEnd(MapGenerationType.IrregularMesh);

    private async Task PlaySessionToTheEnd(MapGenerationType kind)
    {
        var workshop = _shop.GetNode<Node3D>("World/Workshop");
        var controller = workshop.GetNode<DeckBuilderController>("DeckbuilderController");
        var button = workshop.GetNode<InteractableButton>("Cabinet/Assembly/Button/StaticBody3D");
        var toggle = workshop.GetNode<InteractableButton>("Cabinet/MapKindButton/StaticBody3D");
        var cardHolder = workshop.GetNode<DeckSlot>("Cabinet/Assembly/CardSlot");
        var session = workshop.GetNode<GameSessionService>("GameSessionService");
        var irregularScreen = workshop.GetNode<IrregularWorldMapScreen>("Cabinet/IrregularTileMapScreen");
        var lootParent = _shop.GetNode<CardSpawner>("World/Cards");
        AssertThat(controller.MapType).IsEqual(MapGenerationType.RegularGrid);
        if (controller.MapType != kind)
            toggle.Interact();
        AssertThat(controller.MapType).IsEqual(kind);

        var states = new List<SessionState>();
        var lootCount = -1;
        session.StateChanged += states.Add;
        session.LootGenerated += loot => lootCount = loot.Count;
        var startingEnemies = -1;
        var enemiesDefeated = 0;
        irregularScreen.MapGenerated += () => startingEnemies = irregularScreen.MapData!.EnemySpawnCells.Count;
        irregularScreen.EnemyDefeated += _ => enemiesDefeated++;
        var cardsBefore = lootParent.GetChildren().OfType<CardController>().ToList();

        var mapCard = ShopTestCards.Create(new CardSignature().With(Element.Febris, 0.4f));
        workshop.AddChild(mapCard);
        mapCard.GlobalPosition = cardHolder.GlobalPosition + cardHolder.PositionOffset;
        await WorkshopSceneRig.Settle();
        AssertBool(button.Enabled).IsTrue();
        AssertBool(toggle.Enabled).IsTrue();

        button.Interact();
        Engine.TimeScale = SessionTimeScale;
        var clock = Stopwatch.StartNew();
        var toggleLitDuringSession = false;
        while (!states.Contains(SessionState.SessionComplete) && clock.Elapsed.TotalSeconds < SessionBudgetSeconds)
        {
            toggleLitDuringSession |= toggle.Enabled;
            await ISceneRunner.SyncProcessFrame;
        }
        Engine.TimeScale = 1;
        await WorkshopSceneRig.Settle();

        var history = states.Count == 0 ? "no state change" : string.Join(" > ", states);
        GD.Print($"{kind} session ran {clock.Elapsed.TotalSeconds:F1} s: {history}");
        AssertBool(states.Contains(SessionState.SessionComplete))
            .OverrideFailureMessage(
                $"No SessionComplete within {SessionBudgetSeconds} s; last state reached " +
                $"{(states.Count == 0 ? session.CurrentState : states[^1])}, history: {history}")
            .IsTrue();
        AssertThat(lootCount)
            .OverrideFailureMessage($"The session completed without loot (combat lost?); history: {history}")
            .IsGreater(0);
        AssertBool(irregularScreen.IsInitialized)
            .OverrideFailureMessage("The irregular screen shows a map only for an irregular session")
            .IsEqual(kind == MapGenerationType.IrregularMesh);
        if (kind == MapGenerationType.IrregularMesh)
            AssertEveryEnemyFell(irregularScreen, states, startingEnemies, enemiesDefeated, history);

        AssertBool(toggleLitDuringSession)
            .OverrideFailureMessage("The kind toggle lit while the session ran")
            .IsFalse();
        AssertBool(toggle.Enabled).OverrideFailureMessage("The kind toggle lit before the reset press").IsFalse();

        var loot = lootParent.GetChildren().OfType<CardController>().Except(cardsBefore).ToList();
        AssertThat(loot.Count).IsEqual(lootCount);
        var cabinet = workshop.GetNode<Node3D>("Cabinet").GlobalPosition;
        foreach (var card in loot)
        {
            var offset = card.GlobalPosition - cabinet;
            AssertFloat(new Vector2(offset.X, offset.Z).Length())
                .OverrideFailureMessage($"Loot card at {card.GlobalPosition} is not laid out at the cabinet {cabinet}")
                .IsLessEqual(LootReachMeters);
        }

        AssertBool(button.Enabled).IsTrue();
        button.Interact();
        AssertBool(toggle.Enabled).IsTrue();
    }

    /// <summary>
    /// The irregular session fought before its loot: it entered combat, every enemy the map showed fell, the screen
    /// heard each defeat, and no enemy is left on the map.
    /// </summary>
    private static void AssertEveryEnemyFell(
        IrregularWorldMapScreen screen, List<SessionState> states, int startingEnemies, int enemiesDefeated,
        string history)
    {
        var combat = states.IndexOf(SessionState.InCombat);
        AssertBool(combat >= 0 && combat < states.IndexOf(SessionState.GeneratingLoot))
            .OverrideFailureMessage($"The irregular session reached its loot without a fight; history: {history}")
            .IsTrue();
        AssertInt(startingEnemies).OverrideFailureMessage("The irregular map showed no enemy").IsGreater(0);
        AssertInt(enemiesDefeated)
            .OverrideFailureMessage($"{enemiesDefeated} of {startingEnemies} enemies fell on the irregular screen")
            .IsEqual(startingEnemies);
        AssertInt(screen.MapData!.EnemySpawnCells.Count)
            .OverrideFailureMessage("A defeated enemy is still on the irregular map")
            .IsEqual(0);
    }
}
