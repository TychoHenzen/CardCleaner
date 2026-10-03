using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Saveable;
using System.Collections.Generic;
using System.Linq;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Core.Services;

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

    private const string SavePath = "user://game_save.json";
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
        
        var recreatedCount = 0;
        var worldNode = GetTree().CurrentScene as Node3D;
        if (worldNode == null)
        {
            ILog.Error("CurrentScene is not a Node3D, cannot recreate cards");
            return;
        }

        foreach (var cardDataObj in _pendingCardData)
        {
            
            // Handle both Dictionary<string, object> and JObject (from Newtonsoft.Json)
            Dictionary<string, object>? cardData = null;
            
            if (cardDataObj is Dictionary<string, object> dict)
            {
                cardData = dict;
            }
            else if (cardDataObj?.GetType().Name == "JObject")
            {
                // Convert JObject to Dictionary using the Saveable plugin's method
                try
                {
                    var json = SaveExtension.SerializeObject(cardDataObj);
                    cardData = SaveExtension.DeserializeObject<Dictionary<string, object>>(json);
                }
                catch (Exception ex)
                {
                    ILog.Error($"Failed to convert JObject to Dictionary: {ex.Message}");
                    continue;
                }
            }
            
            if (cardData == null)
            {
                ILog.Print($"Could not convert card data object, skipping");
                continue;
            }
            
            
            try
            {
                // Extract card data - handle signature as float array
                var signatureElements = GetFloatArrayFromData(cardData["signature_elements"]);
                var signature = new CardSignature(signatureElements);
                
                var position = GetVector3FromData(cardData["position"]);
                
                var rotation = GetVector3FromData(cardData["rotation"]);
                
                var isHeld = Convert.ToBoolean(cardData["isHeld"]);
                
                var parentPath = cardData["parentPath"]?.ToString() ?? "";
                
                // Determine spawn parent
                Node3D spawnParent = worldNode;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentNode = GetTree().CurrentScene.GetNodeOrNull(parentPath);
                    if (parentNode is Node3D parent3D)
                        spawnParent = parent3D;
                }
                
                // Create spawn transform
                var spawnTransform = new Transform3D(Basis.FromEuler(rotation), position);
                
                // Spawn the card
                var cardInstance = _cardSpawningService.SpawnCard(signature, spawnTransform, spawnParent);
                
                // Handle held state if needed
                if (isHeld && cardInstance is CardController controller)
                {
                    // Find player camera and reparent if card was held
                    var playerCamera = GetTree()
                        .GetFirstNodeInGroup("player")
                        ?.GetNodeOrNull<Camera3D>("Head/Camera3D");
                    if (playerCamera != null)
                    {
                        CallDeferred(MethodName.ReparentToCamera, controller, playerCamera);
                    }
                }
                
                recreatedCount++;
            }
            catch (System.Exception ex)
            {
                ILog.Error($"Failed to recreate card: {ex.Message}");
            }
        }
        
        // Clear the pending data
        _pendingCardData = null;
        ILog.Print($"Recreated {recreatedCount} cards from save data");
    }
    
    private Vector3 GetVector3FromData(object vectorData)
    {
        if (vectorData is Vector3 vec)
            return vec;
        
        // Handle JSON object with x, y, z properties
        var json = SaveExtension.SerializeObject(vectorData);
        return SaveExtension.DeserializeObject<Vector3>(json);
    }
    
    private float[] GetFloatArrayFromData(object arrayData)
    {
        if (arrayData is float[] arr)
            return arr;

        // Handle JSON array
        var json = SaveExtension.SerializeObject(arrayData);
        return SaveExtension.DeserializeObject<float[]>(json) ?? [];
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
