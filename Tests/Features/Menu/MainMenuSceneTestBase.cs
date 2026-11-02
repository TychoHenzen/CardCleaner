using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Controllers;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// Shared fixture for the main menu scene suites. Slots live in an in-memory file system and the options in a
/// throw-away user:// file. ChangeScene is replaced with a recorder. The teardown restores the audio buses and the
/// window size.
/// </summary>
public abstract class MainMenuSceneTestBase
{
    protected const string ScenePath = "res://Scenes/UI/MainMenu.tscn";
    protected static readonly DateTime OlderSave = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime NewerSave = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private protected InMemorySlotFileSystem _files = null!;
    private protected SlotStore _store = null!;
    private protected ActiveSaveSlot _active = null!;
    private protected FakeImporter _importer = null!;
    private protected string _path = null!;
    private protected List<string> _changedScenes = null!;
    private Window _window = null!;
    private Vector2I _originalWindowSize;
    private MainMenuController? _node;

    [BeforeTest]
    public void Setup()
    {
        // Reinitialize clears the container and composes the autoload again; the registrations below replace it.
        TestServices.Reinitialize();
        _files = new InMemorySlotFileSystem();
        _store = new SlotStore(_files);
        _active = new ActiveSaveSlot();
        _importer = new FakeImporter();
        _path = $"user://test_main_menu_{Guid.NewGuid():N}.cfg";
        _changedScenes = new List<string>();

        TestServices.Container.RegisterSingleton<ISaveSlotCatalog>(_store);
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(_active);
        TestServices.Container.RegisterSingleton<ILegacySaveImporter>(_importer);
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_path));
        TestServices.Container.RegisterSingleton<OptionsApplier>(new OptionsApplier());

        _window = ((SceneTree)Engine.GetMainLoop()).Root;
        _originalWindowSize = _window.Size;
        _window.Size = new Vector2I(1280, 720);
    }

    [AfterTest]
    public void Teardown()
    {
        _node?.Free();
        _node = null;
        // Restore the buses through a sink that does not touch the window.
        new OptionsApplier(static (_, _) => { }).Apply(GameOptions.Defaults, _window);
        _window.Size = _originalWindowSize;
        TestServices.Reinitialize();
        if (FileAccess.FileExists(_path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_path));
    }

    protected static void Press(BaseButton button) => button.EmitSignal(BaseButton.SignalName.Pressed);

    protected static SaveSlotName Name(string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);
        return name;
    }

    protected T Get<T>(string path) where T : Node => _node!.GetNode<T>(path);

    protected int RowCount()
    {
        return Get<VBoxContainer>("Layout/SlotList").GetChildren().OfType<HBoxContainer>().Count();
    }

    protected HBoxContainer Row(string slotName)
    {
        foreach (var child in Get<VBoxContainer>("Layout/SlotList").GetChildren())
        {
            if (child is HBoxContainer row
                && row.GetNode<Label>("Label").Text.StartsWith(slotName + "  ", StringComparison.Ordinal))
                return row;
        }

        throw new InvalidOperationException($"No slot row for '{slotName}'.");
    }

    protected void SaveSlot(string raw, DateTime savedAtUtc)
    {
        _store.Save(Name(raw), "payload", savedAtUtc);
    }

    // A slot whose save file holds text that is not a save.
    protected void SeedUnreadableSlot(string raw)
    {
        _store.TryCreate(raw, out var slot, out _);
        _files.Seed(SaveSlotLayout.FileFor(slot), "not a save");
    }

    protected void AddMenu()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        _node = (MainMenuController)packed.Instantiate();
        _node.ChangeScene = path => _changedScenes.Add(path);
        TestServices.Inject(_node);
        Assertions.AddNode(_node, autoFree: false);
    }

    private protected sealed class FakeImporter : ILegacySaveImporter
    {
        public int Calls;

        public Action? Import;

        public bool TryImport()
        {
            Calls++;
            Import?.Invoke();
            return Import is not null;
        }
    }
}
