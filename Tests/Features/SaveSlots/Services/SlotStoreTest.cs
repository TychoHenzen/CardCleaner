using System;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Services;

[TestSuite]
[RequireGodotRuntime]
public class SlotStoreTest
{
    private static readonly DateTime SavedAt = new(2026, 11, 2, 7, 0, 0, DateTimeKind.Utc);

    private static SaveSlotName Name(string raw)
    {
        SaveSlotName.TryCreate(raw, out var name, out _);
        return name;
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SaveThenLoadRoundTripsPayload()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);

        AssertThat(store.Save(Name("My Shop"), "{\"tree\":1}", SavedAt)).IsTrue();

        var result = store.Load(Name("My Shop"));
        AssertThat(result.IsLoaded).IsTrue();
        AssertThat(result.Envelope!.Payload).IsEqual("{\"tree\":1}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LoadOfNewerVersionIsRefusedAndBytesUnchanged()
    {
        var files = new InMemorySlotFileSystem();
        const string text = "{\"Version\":2,\"SavedAtUtc\":\"2026-11-02T07:00:00Z\",\"Payload\":\"{}\"}";
        files.Seed(SaveSlotLayout.FileFor(Name("shop")), text);
        var store = new SlotStore(files);

        var result = store.Load(Name("shop"));

        AssertThat(result.IsLoaded).IsFalse();
        AssertThat(result.Refusal).Contains("newer");
        AssertThat(files.Peek(SaveSlotLayout.FileFor(Name("shop")))).IsEqual(text);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LoadOfInvalidJsonIsRefusedAndBytesUnchanged()
    {
        var files = new InMemorySlotFileSystem();
        files.Seed(SaveSlotLayout.FileFor(Name("shop")), "{");
        var store = new SlotStore(files);

        var result = store.Load(Name("shop"));

        AssertThat(result.Refusal).IsEqual("unreadable");
        AssertThat(files.Peek(SaveSlotLayout.FileFor(Name("shop")))).IsEqual("{");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LoadOfEmptyFileIsRefused()
    {
        var files = new InMemorySlotFileSystem();
        files.Seed(SaveSlotLayout.FileFor(Name("shop")), string.Empty);
        var store = new SlotStore(files);

        AssertThat(store.Load(Name("shop")).Refusal).IsEqual("empty");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FailedWriteLeavesPreviousSaveLoadable()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        store.Save(Name("shop"), "first", SavedAt);
        files.FailWriteOnCall = files.WriteCalls + 1;

        AssertThat(store.Save(Name("shop"), "second", SavedAt)).IsFalse();

        AssertThat(store.Load(Name("shop")).Envelope!.Payload).IsEqual("first");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FailedRenameLeavesPreviousSaveLoadable()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        store.Save(Name("shop"), "first", SavedAt);
        files.FailRename = true;

        AssertThat(store.Save(Name("shop"), "second", SavedAt)).IsFalse();

        AssertThat(store.Load(Name("shop")).Envelope!.Payload).IsEqual("first");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NextSaveAfterFailureOverwritesStaleTemp()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        files.FailRename = true;
        store.Save(Name("shop"), "failed", SavedAt);
        AssertThat(files.Exists(SaveSlotLayout.TempFor(Name("shop")))).IsTrue();
        files.FailRename = false;

        AssertThat(store.Save(Name("shop"), "second", SavedAt)).IsTrue();

        AssertThat(files.Exists(SaveSlotLayout.TempFor(Name("shop")))).IsFalse();
        AssertThat(store.Load(Name("shop")).Envelope!.Payload).IsEqual("second");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ListSkipsFoldersWithInvalidNames()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        store.Save(Name("good shop"), "a", SavedAt);
        files.EnsureFolder(SaveSlotLayout.Root + "/bad.");

        var list = store.List();

        AssertThat(list.Count).IsEqual(1);
        AssertThat(list[0].Key).IsEqual("good shop");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ListIsNewestFirst()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        store.Save(Name("old shop"), "a", SavedAt.AddDays(-1));
        store.Save(Name("new shop"), "b", SavedAt);

        var list = store.List();

        AssertThat(list.Count).IsEqual(2);
        AssertThat(list[0].Key).IsEqual("new shop");
        AssertThat(list[1].Key).IsEqual("old shop");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DeleteRemovesFolder()
    {
        var files = new InMemorySlotFileSystem();
        var store = new SlotStore(files);
        store.Save(Name("shop"), "a", SavedAt);

        store.Delete(Name("shop"));

        AssertThat(files.Exists(SaveSlotLayout.FileFor(Name("shop")))).IsFalse();
        AssertThat(store.List().Count).IsEqual(0);
    }
}
