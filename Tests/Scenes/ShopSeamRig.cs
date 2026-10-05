using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Tests.Features.Shop;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Shared harness for the shop scene seam suites: instantiates the shop scene, hands the scene-owned
/// services to the service locator, and moves the player and holds cards so each suite only states its
/// scenario.
/// </summary>
public sealed class ShopSeamRig
{
    public const int SettleFrames = 4;

    public static readonly Vector3 BackofficeMiddle = new(12f, 0.95f, -3f);
    public static readonly Vector3 NearTheSeam = new(12f, 0.95f, -5.5f);
    public static readonly Vector3 InTheDoorway = new(12.2f, 0.95f, -7.4f);

    private ShopSeamRig(Node3D shop)
    {
        Shop = shop;
        Player = shop.GetNode<PlayerController>("Player");
        Seam = shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam");
        WorkshopEntry = shop.GetNode<Node3D>("World/Workshop/WorkshopEntry");
        Holder = Player.GetNode<CardHolder>("CardInteraction/CardHolder");
    }

    public Node3D Shop { get; }
    public PlayerController Player { get; }
    public WallSeam Seam { get; }
    public Node3D WorkshopEntry { get; }
    public CardHolder Holder { get; }

    public static async Task<ShopSeamRig> Create()
    {
        var rig = new ShopSeamRig(GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>());

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(rig.Shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(rig.Shop.GetNode<Node3D>("World/Cards") as ICardSpawner
            ?? throw new System.InvalidOperationException("World/Cards must be the card spawner"));

        AddNode(rig.Shop);
        await Settle();
        return rig;
    }

    public static CardSignature Special() => new() { Febris = 0.3f };

    public async Task<CardController> Hold(CardSignature signature)
    {
        var card = ShopTestCards.Create(signature);
        Shop.GetNode("World/Cards").AddChild(card);
        Holder.AddCard(card);
        await Settle();
        return card;
    }

    public async Task Drop(CardController card)
    {
        Holder.RemoveCard(card);
        await Settle();
    }

    public async Task MovePlayer(Vector3 position)
    {
        Player.GlobalPosition = position;
        Player.Velocity = Vector3.Zero;
        await Settle();
    }

    public static async Task Settle()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
