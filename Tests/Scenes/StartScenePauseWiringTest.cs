using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Controllers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The start scene wires Escape to the pause menu: the menu has a Resume button, the cursor owner sits under Services,
/// and the pause controller keeps its menu and in-game UI exports. The scene is never added to the tree.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StartScenePauseWiringTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ResumeButtonExistsUnderThePauseMenuPanel()
    {
        var scene = GD.Load<PackedScene>("res://Scenes/Gameplay/StartScene.tscn").Instantiate<Node>();
        try
        {
            var resume = scene.GetNode<Button>("Ui/PauseMenu/Panel/ResumeButton");

            AssertThat(resume.Text).IsEqual("Resume");
        }
        finally
        {
            scene.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CursorOwnerSitsUnderServices()
    {
        var scene = GD.Load<PackedScene>("res://Scenes/Gameplay/StartScene.tscn").Instantiate<Node>();
        try
        {
            AssertThat(scene.GetNodeOrNull<CursorOwner>("Services/CursorOwner")).IsNotNull();
        }
        finally
        {
            scene.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PauseControllerStillWired()
    {
        var scene = GD.Load<PackedScene>("res://Scenes/Gameplay/StartScene.tscn").Instantiate<Node>();
        try
        {
            var pause = scene.GetNode<PauseController>("Ui");

            AssertThat(pause.PauseMenuLayer).IsNotNull();
            AssertThat(pause.UILayer).IsNotNull();
        }
        finally
        {
            scene.Free();
        }
    }
}
