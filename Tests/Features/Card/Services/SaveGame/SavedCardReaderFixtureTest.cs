using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using GdUnit4;
using Saveable;
using Saveable.Extensions;

namespace CardCleaner.Tests.Features.Card.Services.SaveGame;

/// <summary>
/// Reads the committed save written by the save addon (#172 U4). The fixture holds no CLR type names, so moving the
/// save classes to another namespace cannot break a save the player already has.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SavedCardReaderFixtureTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void FixtureCarriesNoClrTypeNames()
    {
        AssertBool(SavedCardFixture.ReadText().Contains("$type")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FixtureLoadsEverySavedCardFieldThroughTheSaveAddon()
    {
        var tree = SaveExtension.DeserializeObject<TreeSave>(SavedCardFixture.ReadText());

        AssertBool(tree!.TryGetCollection("game_save_service", out var save)).IsTrue();
        AssertBool(save!.TryGetProperty<List<object>>("cards", out var entries)).IsTrue();
        var records = entries!.Select(SavedCardReader.Read).ToList();

        AssertThat(records).HasSize(2);
        var held = records[0]!;
        AssertThat(held.Signature.Elements).IsEqual(SavedCardFixture.HeldElements);
        AssertThat(held.Position).IsEqual(SavedCardFixture.HeldPosition);
        AssertThat(held.Rotation).IsEqual(SavedCardFixture.HeldRotation);
        AssertBool(held.IsHeld).IsTrue();
        AssertThat(held.ParentPath).IsEqual(SavedCardFixture.HeldParentPath);

        var placed = records[1]!;
        AssertThat(placed.Signature.Elements).IsEqual(SavedCardFixture.PlacedElements);
        AssertThat(placed.Position).IsEqual(SavedCardFixture.PlacedPosition);
        AssertThat(placed.Rotation).IsEqual(SavedCardFixture.PlacedRotation);
        AssertBool(placed.IsHeld).IsFalse();
        AssertThat(placed.ParentPath).IsEqual(SavedCardFixture.PlacedParentPath);
    }
}
