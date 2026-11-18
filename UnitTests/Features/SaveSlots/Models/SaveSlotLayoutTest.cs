using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Models;

[TestSuite]
public class SaveSlotLayoutTest
{
    [TestCase]
    public static void FileForNamedSlotIsItsFolderSaveJson()
    {
        SaveSlotName.TryCreate("My Shop", out var name, out _);

        var file = SaveSlotLayout.FileFor(name);

        AssertThat(file).IsEqual("user://saves/my shop/save.json");
        AssertThat(file.Contains("..")).IsFalse();
    }
}
