using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The workshop scene wires the same exported node paths as StartScene, leaves no wired path empty, serializes
/// no runtime-only fields, and brings exactly one copy of each singleton the cabinet needs into the shop scene.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopSceneContractTest
{
    private const string WorkshopScenePath = "res://Scenes/Components/Workshop.tscn";
    private const string ShopScenePath = "res://Scenes/Gameplay/ShopScene.tscn";

    private PackedScene _packed = null!;
    private Node3D _workshop = null!;

    [BeforeTest]
    public void Setup()
    {
        _packed = GD.Load<PackedScene>(WorkshopScenePath);
        _workshop = _packed.Instantiate<Node3D>();
    }

    [AfterTest]
    public void Teardown()
    {
        _workshop.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ControllerExportsPointAtTheCabinetPartsLikeStartScene()
    {
        var controller = _workshop.GetNode<DeckBuilderController>("DeckbuilderController");

        AssertSame(controller.AbilityDeckSlot, _workshop.GetNode("Cabinet/Assembly/DeckSlot"));
        AssertSame(controller.MapCardSlot, _workshop.GetNode("Cabinet/Assembly/CardSlot"));
        AssertSame(controller.ActivateButton, _workshop.GetNode("Cabinet/Assembly/Button/StaticBody3D"));
        AssertSame(controller.WorldTileMapScreenScene, _workshop.GetNode("Cabinet/SimpleTileMapScreen"));
        AssertSame(controller.IrregularMapScreen, _workshop.GetNode("Cabinet/IrregularTileMapScreen"));
        AssertThat(controller.MapType).IsEqual(MapGenerationType.IrregularMesh);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreditCardSeederTargetsTheDeckHolderAndOwnsTheSpawnedCard()
    {
        var seeder = _workshop.GetNode<CreditCardSeeder>("CreditCardSeeder");

        AssertSame(seeder.Slot, _workshop.GetNode("Cabinet/Assembly/DeckSlot"));
        AssertSame(seeder.SpawnParent, _workshop);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NoWiredNodePathIsNull()
    {
        var checkedPaths = 0;
        // The return doorway's player and exit live in the shop scene, which wires them; see the test below.
        foreach (var node in OwnedNodes().Where(n => n is not WallSeam))
        {
            foreach (var property in WiredNodeProperties(node))
            {
                AssertThat(node.Get(property).Obj).IsNotNull();
                checkedPaths++;
            }
        }

        AssertBool(checkedPaths > 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneDoesNotSerializeRuntimeOnlyFields()
    {
        var state = _packed.GetState();

        for (var node = 0; node < state.GetNodeCount(); node++)
        {
            for (var i = 0; i < state.GetNodePropertyCount(node); i++)
            {
                var property = state.GetNodePropertyName(node, i).ToString();
                AssertBool(property.StartsWith('_')).IsFalse();
                AssertThat(property).IsNotEqual(nameof(CreditCardSeeder.Card));
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ShopSceneCarriesOneCopyOfEachCabinetSingletonAndNoSaveService()
    {
        var shop = GD.Load<PackedScene>(ShopScenePath).Instantiate<Node3D>();
        try
        {
            AssertThat(Count<GameSessionService>(shop)).IsEqual(1);
            AssertThat(Count<DeckBuilderController>(shop)).IsEqual(1);
            AssertThat(Count<CardSpawningService>(shop)).IsEqual(1);
            AssertThat(Count<GameSaveService>(shop)).IsEqual(0);
        }
        finally
        {
            shop.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeamTargetsTheWorkshopEntryMarker()
    {
        var shop = GD.Load<PackedScene>(ShopScenePath).Instantiate<Node3D>();
        try
        {
            var seam = shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam");

            AssertSame(seam.Exit, shop.GetNode("World/Workshop/WorkshopEntry"));
        }
        finally
        {
            shop.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ReturnDoorwayIsAlwaysOpenAndLeadsBackToTheBackoffice()
    {
        var shop = GD.Load<PackedScene>(ShopScenePath).Instantiate<Node3D>();
        try
        {
            var back = shop.GetNode<WallSeam>("World/Workshop/ReturnSeam");

            AssertBool(back.AlwaysOpen).IsTrue();
            AssertSame(back.Exit, shop.GetNode("World/Markers/ShopReturn"));
            AssertSame(back.Player, shop.GetNode("Player"));
            AssertSame(back.PlayerHolder, shop.GetNode("Player/CardInteraction/CardHolder"));
        }
        finally
        {
            shop.Free();
        }
    }

    private static void AssertSame(Node? actual, Node expected)
    {
        AssertThat(actual).IsNotNull();
        AssertBool(ReferenceEquals(actual, expected)).IsTrue();
    }

    private IEnumerable<Node> OwnedNodes()
    {
        var pending = new Stack<Node>([_workshop]);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node == _workshop || node.Owner == _workshop)
                yield return node;

            foreach (var child in node.GetChildren())
                pending.Push(child);
        }
    }

    private static IEnumerable<string> WiredNodeProperties(Node node)
    {
        return node.GetPropertyList()
            .Where(p => p["type"].AsInt32() == (int)Variant.Type.Object
                        && p["hint"].AsInt32() == (int)PropertyHint.NodeType
                        && (p["usage"].AsInt32() & (int)PropertyUsageFlags.ScriptVariable) != 0)
            .Select(p => p["name"].AsString());
    }

    private static int Count<T>(Node root) where T : Node
    {
        return root.FindChildren("*", owned: false).OfType<T>().Count();
    }
}
