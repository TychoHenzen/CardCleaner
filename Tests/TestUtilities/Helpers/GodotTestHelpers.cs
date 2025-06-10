using GdUnit4;
using Godot;

namespace CardCleaner.Tests.TestUtilities.Helpers;

public static class GodotTestHelpers
{
    public static ISceneRunner MainScene => ISceneRunner.Load((Engine.GetMainLoop() as SceneTree)!.Root);
}