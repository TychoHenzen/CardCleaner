using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Pause.Controllers;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The pause menu's Save button writes the active slot of the shop's save service and reports the result in the
/// status line. The slot files live in an in-memory file system, so no user:// file is touched.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSaveButtonTest
{
    private static readonly DateTime OldSavedAtUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private PauseController _pause = null!;
    private Button _saveButton = null!;
    private Label _statusLabel = null!;
    private InMemorySlotFileSystem _files = null!;
    private SaveSlotName _slot;

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;

        // The rig resets the container, so the save services are registered after it and before the shop enters
        // the tree; the shop's GameSaveService and PauseController resolve them in their _Ready.
        _shop = ShopOrderingRig.LoadShopWithServices();

        _files = new InMemorySlotFileSystem();
        var slots = new ActiveSaveSlot();
        if (!SaveSlotName.TryCreate("a", out _slot, out var reason))
            throw new InvalidOperationException(reason);
        slots.Select(_slot);

        var saveService = _shop.GetNode<GameSaveService>("Services/GameSaveService");
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(slots);
        TestServices.Container.RegisterSingleton<ISlotFileSystem>(_files);
        TestServices.Container.RegisterSingleton<SlotStore>(new SlotStore(_files));
        TestServices.Container.RegisterSingleton<ISaveGameCommand>(saveService);

        _pause = _shop.GetNode<PauseController>("Pause");
        _saveButton = _shop.GetNode<Button>("Pause/PauseMenu/Panel/SaveButton");
        _statusLabel = _shop.GetNode<Label>("Pause/PauseMenu/Panel/SaveStatusLabel");

        AddNode(_shop, autoFree: false);
        _tree.CurrentScene = _shop;
        await ShopPauseRig.SettleAsync();
    }

    [AfterTest]
    public void Teardown()
    {
        _tree.CurrentScene = _previousScene;
        _shop.Free();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SaveButtonWritesTheActiveSlotAndReportsSaved()
    {
        var path = SaveSlotLayout.FileFor(_slot);
        var previousText = SaveEnvelope.Write("previous", OldSavedAtUtc);
        _files.Seed(path, previousText);
        var previousStamp = SaveEnvelope.Read(previousText).Envelope!.SavedAtUtc;
        _pause._UnhandledInput(ShopPauseRig.EscapeEvent());

        _saveButton.EmitSignal(BaseButton.SignalName.Pressed);

        AssertBool(_files.Exists(path)).IsTrue();
        var saved = SaveEnvelope.Read(_files.Peek(path));
        AssertBool(saved.IsOk).IsTrue();
        AssertThat(saved.Envelope!.SavedAtUtc).IsNotEqual(previousStamp);
        AssertThat(_statusLabel.Text).IsEqual("Saved");
    }
}
