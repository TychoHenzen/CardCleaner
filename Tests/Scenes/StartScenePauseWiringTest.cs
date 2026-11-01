using System;
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
    private const string ScenePath = "res://Scenes/Gameplay/StartScene.tscn";

    [TestCase]
    [TestCategory("Unit")]
    public static void ResumeButtonExistsUnderThePauseMenuPanel() =>
        WithStartScene(scene =>
            AssertThat(scene.GetNode<Button>("Ui/PauseMenu/Panel/ResumeButton").Text).IsEqual("Resume"));

    [TestCase]
    [TestCategory("Unit")]
    public static void CursorOwnerSitsUnderServices() =>
        WithStartScene(scene =>
            AssertThat(scene.GetNodeOrNull<CursorOwner>("Services/CursorOwner")).IsNotNull());

    [TestCase]
    [TestCategory("Unit")]
    public static void PauseControllerStillWired() =>
        WithStartScene(scene =>
        {
            var pause = scene.GetNode<PauseController>("Ui");

            AssertThat(pause.PauseMenuLayer).IsNotNull();
            AssertThat(pause.UILayer).IsNotNull();
        });

    private static void WithStartScene(Action<Node> check)
    {
        var scene = GD.Load<PackedScene>(ScenePath).Instantiate<Node>();
        try
        {
            check(scene);
        }
        finally
        {
            scene.Free();
        }
    }
}
