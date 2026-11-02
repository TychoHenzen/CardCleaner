using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Services;

[TestSuite]
[RequireGodotRuntime]
public class LegacySaveImporterTest
{
    private const string Legacy = "{\"tree\":1}";

    private static SaveSlotName Name(string raw)
    {
        SaveSlotName.TryCreate(raw, out var name, out _);
        return name;
    }

    private static SaveSlotName ImportedName() => Name("imported");

    [TestCase]
    [TestCategory("Unit")]
    public static void ImportWhenNoSlotsWritesLegacyPayloadToImportedSlot()
    {
        var files = new InMemorySlotFileSystem();
        files.Seed(LegacySaveImporter.LegacyPath, Legacy);
        var importer = new LegacySaveImporter(new SlotStore(files), files);

        AssertThat(importer.TryImport()).IsTrue();

        var result = new SlotStore(files).Load(ImportedName());
        AssertThat(result.IsLoaded).IsTrue();
        AssertThat(result.Envelope!.Payload).IsEqual(Legacy);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LegacyBytesAreIdenticalAfterImport()
    {
        var files = new InMemorySlotFileSystem();
        files.Seed(LegacySaveImporter.LegacyPath, Legacy);
        var importer = new LegacySaveImporter(new SlotStore(files), files);

        importer.TryImport();

        AssertThat(files.Peek(LegacySaveImporter.LegacyPath)).IsEqual(Legacy);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SecondCallReturnsFalseAndCreatesNothing()
    {
        var files = new InMemorySlotFileSystem();
        files.Seed(LegacySaveImporter.LegacyPath, Legacy);
        var importer = new LegacySaveImporter(new SlotStore(files), files);
        importer.TryImport();
        var writesAfterFirstCall = files.WriteCalls;

        AssertThat(importer.TryImport()).IsFalse();

        AssertThat(files.WriteCalls).IsEqual(writesAfterFirstCall);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoImportWhenAnySlotExists()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        store.Save(Name("shop"), "{}", System.DateTime.UtcNow);
        files.Seed(LegacySaveImporter.LegacyPath, Legacy);
        var importer = new LegacySaveImporter(store, files);

        AssertThat(importer.TryImport()).IsFalse();

        AssertThat(files.Exists(SaveSlotLayout.FileFor(ImportedName()))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoImportWhenLegacyFileIsAbsent()
    {
        var files = new InMemorySlotFileSystem();
        var importer = new LegacySaveImporter(new SlotStore(files), files);

        AssertThat(importer.TryImport()).IsFalse();

        AssertThat(new SlotStore(files).List().Count).IsEqual(0);
    }
}
