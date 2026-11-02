using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using Godot;
using Saveable;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

// Names ISaveable (the save file's owner) and ISaveGameCommand (the pause menu's Save button).
[Service(typeof(ISaveable), typeof(ISaveGameCommand))]
public partial class GameSaveService : Node, ISaveable, ISaveGameCommand, IDependent
{
    // Default values for exported properties
    private const float DefaultAutoSaveInterval = 10.0f;
    private const bool DefaultAutoLoadOnStart = true;

    [Export] public float AutoSaveInterval { get; set; } = DefaultAutoSaveInterval; // Save every 10 seconds
    [Export] public bool AutoLoadOnStart { get; set; } = DefaultAutoLoadOnStart;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(AutoSaveInterval) => true,
            nameof(AutoLoadOnStart) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(AutoSaveInterval) => DefaultAutoSaveInterval,
            nameof(AutoLoadOnStart) => DefaultAutoLoadOnStart,
            _ => base._PropertyGetRevert(property)
        };
    }

    private Timer? _autoSaveTimer;
    private ICardSpawningService? _cardSpawningService;
    private List<object>? _pendingCardData;
    private IServiceResolver? _services;

    // The slot files this service reads and writes. Production resolves them in _Ready; tests pass them to the
    // internal constructor, which keeps every save inside their in-memory file system.
    private ISlotFileSystem? _files;
    private IActiveSaveSlot? _activeSlot;
    private SlotStore? _store;
    private LegacySaveImporter? _importer;

    // Godot instantiates scenes through this constructor, and the DI scan does not build Godot objects.
    public GameSaveService()
    {
    }

    internal GameSaveService(ISlotFileSystem files, IActiveSaveSlot activeSlot)
    {
        _files = files;
        _activeSlot = activeSlot;
        _store = new SlotStore(files);
        _importer = new LegacySaveImporter(_store, files, activeSlot);
    }

    // ISaveable implementation for managing dynamic cards
    public StringName UniqueID => "game_save_service";

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        ResolveServices(_services);

        // Bring a pre-slot game_save.json across before the first load reads the active slot.
        _importer?.TryImport();

        SetupAutoSave();

        if (AutoLoadOnStart)
        {
            // Defer loading to ensure scene is fully ready
            CallDeferred(MethodName.LoadGame);
        }
    }
    
    // ASSUMPTION: these services are registered before this node readies, so each callback runs synchronously and
    // the ??= below leaves the seam collaborators of a test untouched. A pending callback only fills a null field.
    private void ResolveServices(IServiceResolver services)
    {
        services.Get<ICardSpawningService>(service => _cardSpawningService = service);
        services.Get<IActiveSaveSlot>(slot => _activeSlot ??= slot);
        services.Get<ISlotFileSystem>(files => _files ??= files);
        services.Get<SlotStore>(store => _store ??= store);
        services.Get<LegacySaveImporter>(importer => _importer ??= importer);
    }

    private void SetupAutoSave()
    {
        // Ensure AutoSaveInterval is valid (Godot Timer requires WaitTime > 0)
        if (AutoSaveInterval <= 0)
        {
            AutoSaveInterval = 10.0f;
            ILog.Print($"AutoSaveInterval was <= 0, reset to default: {AutoSaveInterval}s");
        }

        _autoSaveTimer = new Timer();
        AddChild(_autoSaveTimer);
        _autoSaveTimer.WaitTime = AutoSaveInterval;
        _autoSaveTimer.Timeout += SaveGame;
        _autoSaveTimer.Start();

        ILog.Print($"Auto-save enabled: every {AutoSaveInterval} seconds");
    }
    
    public void Save(NodeSave save)
    {
        var cardData = CaptureCards();
        save.SetOrAddProperty("cards", cardData);
        ILog.Print($"Saved {cardData.Count} cards to persistence data");

        var containers = SavedContainers.Capture(GetTree());
        save.SetOrAddProperty("containers", containers);
        ILog.Print($"Saved {containers.Count} containers to persistence data");
    }

    private List<Dictionary<string, object?>> CaptureCards()
    {
        return GetTree().GetNodesInGroup("Cards").OfType<CardController>()
            .Select(card => new Dictionary<string, object?>
            {
                ["signature_elements"] = card.HasSignature ? card.Signature.Elements : null,
                ["look_seed"] = card.LookSeed,
                ["position"] = card.GlobalPosition,
                ["rotation"] = card.GlobalRotation,
                ["isHeld"] = card.GetParent() is Camera3D,
                ["parentPath"] = card.GetParent()?.GetPath().ToString() ?? ""
            })
            .ToList();
    }

    public void Load(NodeSave save)
    {
        ILog.Print("GameSaveService Load() called");

        if (save.TryGetProperty<List<object>>("containers", out var containerDataRaw))
            RestoreContainers(containerDataRaw ?? new List<object>(), GetTree().CurrentScene as Node3D);

        if (!save.TryGetProperty<List<object>>("cards", out var cardDataRaw))
        {
            ILog.Print("No 'cards' property found in save data");
            return;
        }
        
        if (_cardSpawningService == null)
        {
            ILog.Print("CardSpawningService not available");
            return;
        }
        
        ILog.Print($"Found {cardDataRaw?.Count ?? 0} cards in save data");
        
        // Clear existing cards first
        var existingCards = GetTree().GetNodesInGroup("Cards").OfType<CardController>().ToList();
        ILog.Print($"Clearing {existingCards.Count} existing cards");
        foreach (var card in existingCards)
        {
            card.QueueFree();
        }
        
        // Store data in field and defer recreation
        _pendingCardData = cardDataRaw;
        CallDeferred(MethodName.RecreateCards);
    }
    
    // Every container record is checked before the first node is freed, so a malformed slot leaves the scene as it was.
    private void RestoreContainers(List<object> containerDataRaw, Node3D? world)
    {
        var saved = SavedContainers.Parse(containerDataRaw);
        if (world == null)
        {
            ILog.Error("CurrentScene is not a Node3D, cannot restore containers");
            return;
        }

        SavedContainers.Restore(GetTree(), saved, world);
    }

    private void RecreateCards()
    {
        ILog.Print("RecreateCards() called");

        if (_cardSpawningService == null)
        {
            ILog.Print("CardSpawningService is null");
            return;
        }

        if (_pendingCardData == null)
        {
            ILog.Print("PendingCardData is null");
            return;
        }

        ILog.Print($"Processing {_pendingCardData.Count} card entries");

        var worldNode = GetTree().CurrentScene as Node3D;
        if (worldNode == null)
        {
            ILog.Error("CurrentScene is not a Node3D, cannot recreate cards");
            return;
        }

        var respawner = new SavedCardRespawner(
            GetTree(),
            _cardSpawningService,
            worldNode,
            (card, camera) => CallDeferred(MethodName.ReparentToCamera, card, camera));

        var recreatedCount = 0;
        foreach (var cardDataObj in _pendingCardData)
        {
            if (TryRecreateCard(respawner, cardDataObj))
                recreatedCount++;
        }

        // Clear the pending data
        _pendingCardData = null;
        ILog.Print($"Recreated {recreatedCount} cards from save data");
    }

    private static bool TryRecreateCard(SavedCardRespawner respawner, object cardDataObj)
    {
        try
        {
            var saved = SavedCardReader.Read(cardDataObj);
            if (saved == null)
                return false;

            respawner.Respawn(saved);
            return true;
        }
        catch (Exception ex)
        {
            ILog.Error($"Failed to recreate card: {ex.Message}");
            return false;
        }
    }

    private void ReparentToCamera(CardController card, Camera3D camera)
    {
        if (card.GetParent() != camera)
        {
            card.GetParent()?.RemoveChild(card);
            camera.AddChild(card);
        }
    }
    
    // The autosave timer's Timeout handler; the bool result is for ManualSave only.
    public void SaveGame() => TrySave();

    private bool TrySave()
    {
        if (_store == null || _activeSlot == null)
            return false;

        try
        {
            var slot = _activeSlot.Current;
            var payload = SaveSystem.SerializeTree(GetTree().CurrentScene);
            if (!_store.Save(slot, payload, DateTime.UtcNow))
                return false;

            ILog.Print($"Game auto-saved to slot {slot.Value}");
            return true;
        }
        catch (System.Exception ex)
        {
            ILog.Error($"Failed to save game: {ex.Message}");
            return false;
        }
    }

    public void LoadGame()
    {
        if (_store == null || _activeSlot == null)
            return;

        var result = _store.Load(_activeSlot.Current);
        if (result.IsMissing)
        {
            ILog.Print("No save file found - starting fresh");
            return;
        }

        // A refused file is logged by the store and left in place; nothing is loaded from it.
        if (!result.IsLoaded)
            return;

        try
        {
            var root = GetTree().CurrentScene;
            SaveSystem.LoadTreeFromJson(result.Envelope!.Payload, root);
            ILog.Print("📁 Game loaded from save file");
        }
        catch (System.Exception ex)
        {
            ILog.Error($"Failed to load game: {ex.Message}");
        }
    }

    public bool HasSaveFile()
    {
        return _files != null && _activeSlot != null && _files.Exists(SaveSlotLayout.FileFor(_activeSlot.Current));
    }
    
    public bool ManualSave()
    {
        var saved = TrySave();
        if (saved)
            ILog.Print("Manual save completed");
        return saved;
    }
    
    public override void _ExitTree()
    {
        // Final save when exiting; without a store there is no slot to write to
        if (_store == null)
            return;

        if (HasSaveFile() || GetTree().CurrentScene.GetChildCount() > 0)
        {
            SaveGame();
            ILog.Print("🏁 Final save on exit");
        }
    }
}
