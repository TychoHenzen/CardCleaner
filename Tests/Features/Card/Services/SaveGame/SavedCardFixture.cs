using System;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services.SaveGame;

/// <summary>
/// The committed save written by the save addon (#172 U1, U4): two cards, the first one held by the player camera.
/// The expected values here match the JSON; the fixture was generated once with SaveExtension.SerializeObject.
/// </summary>
internal static class SavedCardFixture
{
    internal const string FilePath =
        "res://Tests/Features/Card/Services/SaveGame/Fixtures/game_save_two_cards.json";

    internal static readonly float[] HeldElements = [0.1f, -0.2f, 0.3f, -0.4f, 0.5f, -0.6f, 0.7f, -0.8f];
    internal static readonly float[] PlacedElements = [-0.9f, 0.8f, -0.7f, 0.6f, -0.5f, 0.4f, -0.3f, 0.2f];
    internal static readonly Vector3 HeldPosition = new(1.5f, 2.25f, -3f);
    internal static readonly Vector3 HeldRotation = new(0.5f, -1f, 0.25f);
    internal static readonly Vector3 PlacedPosition = new(-4f, 0.5f, 6.75f);
    internal static readonly Vector3 PlacedRotation = new(0f, 1.5f, 0f);
    internal const string HeldParentPath = "/root/SaveFixtureWorld/Player/Head/Camera3D";
    internal const string PlacedParentPath = "/root/SaveFixtureWorld";

    internal static string ReadText()
    {
        var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"Cannot open {FilePath}");
        var text = file.GetAsText();
        file.Close();
        return text;
    }
}
