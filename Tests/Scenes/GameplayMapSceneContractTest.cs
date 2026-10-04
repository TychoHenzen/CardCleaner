using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The gameplay map scenes only serialize properties their scripts declare and do not carry
/// nodes that the screen scripts create themselves.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameplayMapSceneContractTest
{
    private const string SimpleScene = "res://Scenes/Gameplay/SimpleTileMapScreen.tscn";
    private const string IrregularScene = "res://Scenes/Gameplay/IrregularTileMapScreen.tscn";

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
}
