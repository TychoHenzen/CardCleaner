using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Models;

[TestSuite]
[RequireGodotRuntime]
public class SaveSlotLayoutTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void FileForNamedSlotIsItsFolderSaveJson()
    {
        SaveSlotName.TryCreate("My Shop", out var name, out _);

        var file = SaveSlotLayout.FileFor(name);

        AssertThat(file).IsEqual("user://saves/my shop/save.json");
        AssertThat(file.Contains("..")).IsFalse();
    }
}
