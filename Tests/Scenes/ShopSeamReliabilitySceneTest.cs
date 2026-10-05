using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Portal.Models;
using CardCleaner.Tests.Features.Shop;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Odd states of the backoffice wall seam in the shop scene: one crossing only, dropping the card closes
/// the seam, a player pressed against the wall still crosses, and a blocked landing is refused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSeamReliabilitySceneTest
{
    private const int SettleFrames = 4;

    private static readonly Vector3 BackofficeMiddle = new(12f, 0.95f, -3f);
    private static readonly Vector3 NearTheSeam = new(12f, 0.95f, -5.5f);
    private static readonly Vector3 InTheDoorway = new(12.2f, 0.95f, -7.4f);

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private WallSeam _seam = null!;
    private Node3D _workshopEntry = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _seam = _shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam");
        _workshopEntry = _shop.GetNode<Node3D>("World/WorkshopPlaceholder/WorkshopEntry");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(_shop.GetNode<Node3D>("World/Cards") as ICardSpawner
            ?? throw new System.InvalidOperationException("World/Cards must be the card spawner"));

        AddNode(_shop);
        await Settle();
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CrossingOnceDoesNotTeleportAgainAndTheSeamCloses()
    {
        await MovePlayer(BackofficeMiddle);
        await Hold(Special());
        await MovePlayer(InTheDoorway);
        await Settle();
        await Settle();

        AssertThat(_seam.CrossingCount).IsEqual(1);
        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DroppingTheCardWhileTheSeamIsOpenClosesIt()
    {
        await MovePlayer(BackofficeMiddle);
        var card = await Hold(Special());
        await MovePlayer(NearTheSeam);
        AssertThat(_seam.Phase).IsEqual(SeamPhase.Open);

        _player.GetNode<CardCleaner.Scripts.Features.Card.Components.CardHolder>("CardInteraction/CardHolder")
            .RemoveCard(card);
        await Settle();

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerPressedAgainstTheWallStillCountsAsCrossing()
    {
        await MovePlayer(BackofficeMiddle);
        await Hold(Special());
        // The wall face is at z=-7.85 and the player capsule radius is 0.5, so this is as close as it gets.
        await MovePlayer(new Vector3(12f, 0.95f, -7.34f));

        AssertThat(_seam.CrossingCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DroppingTheSpecialCardOfAMixedHandClosesTheSeam()
    {
        await MovePlayer(BackofficeMiddle);
        var special = await Hold(Special());
        await Hold(new CardSignature());
        AssertThat(_seam.Phase).IsEqual(SeamPhase.Glowing);

        _player.GetNode<CardCleaner.Scripts.Features.Card.Components.CardHolder>("CardInteraction/CardHolder")
            .RemoveCard(special);
        await Settle();

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BlockedLandingRefusesTheCrossingAndKeepsThePlayerInTheShop()
    {
        var blocker = new StaticBody3D { Position = _workshopEntry.GlobalPosition };
        blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4f, 6f, 4f) } });
        _shop.AddChild(blocker);
        await MovePlayer(BackofficeMiddle);
        await Hold(Special());
        await MovePlayer(InTheDoorway);

        AssertThat(_seam.CrossingCount).IsEqual(0);
        AssertBool(_player.GlobalPosition.X < 20f).IsTrue();
    }

    private static CardSignature Special() => new() { Febris = 0.3f };

    private async Task<CardController> Hold(CardSignature signature)
    {
        var card = ShopTestCards.Create(signature);
        _shop.GetNode("World/Cards").AddChild(card);
        _player.GetNode<CardCleaner.Scripts.Features.Card.Components.CardHolder>("CardInteraction/CardHolder")
            .AddCard(card);
        await Settle();
        return card;
    }

    private async Task MovePlayer(Vector3 position)
    {
        _player.GlobalPosition = position;
        _player.Velocity = Vector3.Zero;
        await Settle();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
