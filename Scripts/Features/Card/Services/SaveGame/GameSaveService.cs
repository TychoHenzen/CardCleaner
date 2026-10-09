using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Saveable;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

[Service]
public partial class GameSaveService : Node, ISaveable
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

    private const string DefaultSavePath = "user://game_save.json";
    // Tests point this at their own GUID-named file. _ExitTree saves here, so a test that frees the service must not
    // leave the player's real save overwritten.
    internal string SavePath { get; set; } = DefaultSavePath;
    private Timer? _autoSaveTimer;
    private ICardSpawningService? _cardSpawningService;
    private List<object>? _pendingCardData;
    
    // ISaveable implementation for managing dynamic cards
    public StringName UniqueID => "game_save_service";
    
    public override void _Ready()
    {
        ServiceLocator.Get<ICardSpawningService>(service => _cardSpawningService = service);
        
        SetupAutoSave();
        
        if (AutoLoadOnStart)
        {
            // Defer loading to ensure scene is fully ready
            CallDeferred(MethodName.LoadGame);
        }
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
        // Find all cards in the scene and save their spawn data
        var cardData = new List<Dictionary<string, object>>();
        var cards = GetTree().GetNodesInGroup("Cards").OfType<CardController>();
        
        foreach (var card in cards)
        {
            var data = new Dictionary<string, object>
            {
                ["signature_elements"] = card.Signature.Elements, // Store the float array directly
                ["position"] = card.GlobalPosition,
                ["rotation"] = card.GlobalRotation,
                ["isHeld"] = card.GetParent() is Camera3D,
                ["parentPath"] = card.GetParent()?.GetPath().ToString() ?? ""
            };
            cardData.Add(data);
        }
        
        save.SetOrAddProperty("cards", cardData);
        ILog.Print($"Saved {cardData.Count} cards to persistence data");
    }
    
    public void Load(NodeSave save)
    {
        ILog.Print("GameSaveService Load() called");
        
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
    
    public void SaveGame()
    {
        try
        {
            var root = GetTree().CurrentScene;
            SaveSystem.SaveFile(SavePath, root);
            ILog.Print($"🔄 Game auto-saved to {SavePath}");
        }
        catch (System.Exception ex)
        {
            ILog.Error($"Failed to save game: {ex.Message}");
        }
    }
    
    public void LoadGame()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            ILog.Print("No save file found - starting fresh");
            return;
        }
        
        try
        {
            var root = GetTree().CurrentScene;
            SaveSystem.LoadFile(SavePath, root);
            ILog.Print("📁 Game loaded from save file");
        }
        catch (System.Exception ex)
        {
            ILog.Error($"Failed to load game: {ex.Message}");
        }
    }
    
    public bool HasSaveFile()
    {
        return FileAccess.FileExists(SavePath);
    }
    
    public void ManualSave()
    {
        SaveGame();
        ILog.Print("💾 Manual save completed");
    }
    
    public override void _ExitTree()
    {
        // Final save when exiting
        if (HasSaveFile() || GetTree().CurrentScene.GetChildCount() > 0)
        {
            SaveGame();
            ILog.Print("🏁 Final save on exit");
        }
    }
}
