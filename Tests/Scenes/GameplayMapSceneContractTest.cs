using CardCleaner.Scripts.Features.Deckbuilder.Controllers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The gameplay map scenes only serialize properties their scripts declare and do not carry
/// nodes that the screen scripts create themselves. The scenes with a map cabinet leave the map kind to the
/// cabinet's toggle.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameplayMapSceneContractTest
{
    private const string SimpleScene = "res://Scenes/Gameplay/SimpleTileMapScreen.tscn";
    private const string IrregularScene = "res://Scenes/Gameplay/IrregularTileMapScreen.tscn";

    private static readonly string[] CabinetScenes =
    {
        "res://Scenes/Components/Workshop.tscn",
        "res://Scenes/Gameplay/StartScene.tscn"
    };

    private PackedScene _simplePacked = null!;
    private PackedScene _irregularPacked = null!;

    [BeforeTest]
    public void Setup()
    {
        _simplePacked = GD.Load<PackedScene>(SimpleScene);
        _irregularPacked = GD.Load<PackedScene>(IrregularScene);
    }

    [TestCase]
    public void SimpleMapScreenDoesNotSerializeMapType()
    {
        var state = _simplePacked.GetState();

        for (int i = 0; i < state.GetNodePropertyCount(0); i++)
        {
            AssertThat(state.GetNodePropertyName(0, i).ToString()).IsNotEqual("MapType");
        }
    }

    [TestCase]
    public void IrregularMapScreenHasNoScenePlayerSprite()
    {
        var screen = _irregularPacked.Instantiate<Node3D>();
        AddNode(screen);

        AssertThat(screen.GetNodeOrNull("SubViewport/PlayerAgent")).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CabinetScenesSetNoMapTypeAndWireTheKindToggle()
    {
        foreach (var path in CabinetScenes)
        {
            var packed = GD.Load<PackedScene>(path);
            var state = packed.GetState();
            for (var node = 0; node < state.GetNodeCount(); node++)
            {
                for (var i = 0; i < state.GetNodePropertyCount(node); i++)
                {
                    AssertThat(state.GetNodePropertyName(node, i).ToString())
                        .OverrideFailureMessage($"{path} sets MapType on {state.GetNodePath(node)}")
                        .IsNotEqual(nameof(DeckBuilderController.MapType));
                }
            }

            var root = packed.Instantiate();
            try
            {
                var controller = FindController(root);
                AssertThat(controller).OverrideFailureMessage($"{path} has no DeckBuilderController").IsNotNull();
                AssertThat(controller!.MapType).IsEqual(MapGenerationType.RegularGrid);
                AssertThat(controller.MapKindButton)
                    .OverrideFailureMessage($"{path} leaves MapKindButton empty")
                    .IsNotNull();
                AssertBool(ReferenceEquals(controller.MapKindButton, controller.ActivateButton)).IsFalse();
            }
            finally
            {
                root.Free();
            }
        }
    }

    private static DeckBuilderController? FindController(Node node)
    {
        if (node is DeckBuilderController controller) return controller;

        foreach (var child in node.GetChildren())
        {
            var found = FindController(child);
            if (found != null) return found;
        }

        return null;
    }
}
