using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The game starts at the main menu: project.godot names it as the run scene and the file exists.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainSceneContractTest
{
    private const string MainMenuPath = "res://Scenes/UI/MainMenu.tscn";

    [TestCase]
    [TestCategory("Unit")]
    public static void ProjectMainSceneIsTheMainMenu()
    {
        var mainScene = ProjectSettings.GetSetting("application/run/main_scene").AsString();

        Assertions.AssertString(mainScene).IsEqual(MainMenuPath);
        Assertions.AssertBool(FileAccess.FileExists(MainMenuPath)).IsTrue();
    }
}
