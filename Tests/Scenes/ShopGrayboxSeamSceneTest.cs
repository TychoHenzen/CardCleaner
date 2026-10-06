using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Portal.Models;
using CardCleaner.Tests.Features.Shop;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxSeamSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";

    private Node3D _shop = null!;
    private CharacterBody3D _player = null!;
    private CardHolder _holder = null!;
    private WallSeam _seam = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<CharacterBody3D>("Player");
        _holder = _shop.GetNode<CardHolder>("Player/CardInteraction/CardHolder");
        _seam = _shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam");

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(_shop.GetNode("World/Cards") as ICardSpawner
            ?? throw new System.InvalidOperationException("Graybox card spawner is missing"));

        AddNode(_shop);
        await Settle();
    }

    [AfterTest]
    public void Teardown()
    {
        if (GodotObject.IsInstanceValid(_shop))
            _shop.Free();
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SeamNeedsASpecialCardAndBackofficePosition()
    {
        await Move(new Vector3(8, 0.9f, 8));
        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_seam.HumRequested).IsFalse();

        await HoldSpecial();
        AssertThat(_seam.Phase).IsEqual(SeamPhase.Glowing);
        AssertBool(_seam.Glow!.Visible).IsTrue();
        AssertBool(_seam.HumRequested).IsTrue();

        await Move(new Vector3(0, 0.9f, 8));
        AssertThat(_seam.Phase).IsEqual(SeamPhase.Hidden);
        AssertBool(_seam.HumRequested).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ApproachOpensDoorAndCrossingPreservesFacingAtWorkshopEntry()
    {
        await Move(new Vector3(8, 0.9f, 8));
        await HoldSpecial();
        await Move(new Vector3(8, 0.9f, 11.1f));

        AssertThat(_seam.Phase).IsEqual(SeamPhase.Open);
        AssertBool(_seam.Door!.Visible).IsTrue();

        _player.RotationDegrees = new Vector3(0, 30, 0);
        await Move(new Vector3(8.1f, 0.9f, 12.0f));

        AssertThat(_seam.CrossingCount).IsEqual(1);
        AssertBool(_player.GlobalPosition.X > 50).IsTrue();
        AssertBool(Mathf.IsEqualApprox(_player.GlobalRotationDegrees.Y, 30)).IsTrue();
    }

    private async Task HoldSpecial()
    {
        var card = ShopTestCards.Create(new CardSignature { Febris = 0.3f });
        _shop.GetNode("World/Cards").AddChild(card);
        _holder.AddCard(card);
        await Settle();
    }

    private async Task Move(Vector3 position)
    {
        _player.GlobalPosition = position;
        _player.Velocity = Vector3.Zero;
        await Settle();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
