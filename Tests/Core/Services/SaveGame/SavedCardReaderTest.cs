using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.Services.SaveGame;

[TestSuite]
[RequireGodotRuntime]
public class SavedCardReaderTest
{
    private static readonly float[] SavedElements = [0.1f, -0.2f, 0.3f, -0.4f, 0.5f, -0.6f, 0.7f, -0.8f];

    [TestCase]
    [TestCategory("Unit")]
    public static void Read_DictionaryEntry_ReturnsEverySavedField()
    {
        var entry = new Dictionary<string, object>
        {
            ["signature_elements"] = SavedElements,
            ["position"] = new Vector3(1f, 2f, 3f),
            ["rotation"] = new Vector3(-4f, 5f, -6f),
            ["isHeld"] = true,
            ["parentPath"] = "World/Camera",
        };

        var record = SavedCardReader.Read(entry);

        AssertBool(record is not null).IsTrue();
        AssertThat(record!.Signature.Elements).IsEqual(SavedElements);
        AssertThat(record.Position).IsEqual(new Vector3(1f, 2f, 3f));
        AssertThat(record.Rotation).IsEqual(new Vector3(-4f, 5f, -6f));
        AssertThat(record.IsHeld).IsTrue();
        AssertThat(record.ParentPath).IsEqual("World/Camera");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Read_UnconvertibleEntry_ReturnsNull()
    {
        var record = SavedCardReader.Read("not a saved card");

        AssertThat(record).IsNull();
    }
}
