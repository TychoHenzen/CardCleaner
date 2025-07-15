using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

public partial class RealtimePersistenceService : Node
{
    [Export] public float SaveInterval { get; set; } = 3.0f;
    [Export] public string SavePath { get; set; } = "user://world_state.tscn";
    [Export] public Node WorldNode { get; set; }
    
    private Timer _saveTimer;

    public override void _Ready()
    {
        if (ILog.ExportCheck(WorldNode, nameof(WorldNode), this))
            return;
            
        CallDeferred(MethodName.LoadWorld);
        SetupSaveTimer();
    }

    private void SetupSaveTimer()
    {
        _saveTimer = new Timer();
        AddChild(_saveTimer);
        _saveTimer.WaitTime = SaveInterval;
        _saveTimer.Timeout += SaveIfDirty;
        _saveTimer.Start();
    }

    private void SaveIfDirty()
    {
        if (WorldNode == null)
        {
            ILog.Error("WorldNode reference is null, cannot save");
            return;
        }

        try
        {
            // Get the parent and position info before removing
            var parent = WorldNode.GetParent();
            var nodeIndex = WorldNode.GetIndex();
            var nodeName = WorldNode.Name;
            
            if (parent == null)
            {
                ILog.Error("WorldNode has no parent, cannot save safely");
                return;
            }

            // Temporarily remove the node from the tree to avoid the ownership error
            parent.RemoveChild(WorldNode);
            
            // Now pack the disconnected node
            var packedScene = new PackedScene();
            var packResult = packedScene.Pack(WorldNode);
            
            // Add the node back to its original position
            parent.AddChild(WorldNode);
            parent.MoveChild(WorldNode, nodeIndex);
            WorldNode.Name = nodeName; // Ensure name is preserved
            
            if (packResult != Error.Ok)
            {
                ILog.Error($"Failed to pack world node: {packResult}");
                return;
            }
            
            var error = ResourceSaver.Save(packedScene, SavePath);
            
            if (error != Error.Ok)
            {
                ILog.Error($"Failed to save world state: {error}");
                return;
            }
            
            ILog.Print($"World state saved to {SavePath}");
        }
        catch (Exception ex)
        {
            ILog.Error($"Failed to save world state: {ex.Message}");
        }
    }

    public void LoadWorld()
    {
        if (!FileAccess.FileExists(SavePath)) 
        {
            ILog.Print("No world state save file found");
            return;
        }

        if (WorldNode == null)
        {
            ILog.Error("WorldNode reference is null, cannot load");
            return;
        }

        try
        {
            var savedScene = GD.Load<PackedScene>(SavePath);
            if (savedScene == null)
            {
                ILog.Error("Failed to load saved world state");
                return;
            }

            // Get the parent to add the replacement to
            var parent = WorldNode.GetParent();
            var nodeIndex = WorldNode.GetIndex();
            
            if (parent == null)
            {
                ILog.Error("WorldNode has no parent, cannot replace");
                return;
            }

            // Remove existing world node
            WorldNode.QueueFree();
            
            // Wait for the node to be freed before adding the new one
            CallDeferred(MethodName.InstantiateWorld, savedScene, parent, nodeIndex);
        }
        catch (Exception ex)
        {
            ILog.Error($"Failed to load world state: {ex.Message}");
        }
    }
    
    private void InstantiateWorld(PackedScene savedScene, Node parent, int nodeIndex)
    {
        var newWorld = savedScene.Instantiate();
        newWorld.Name = WorldNode.Name; // Keep the same name
        parent.AddChild(newWorld);
        parent.MoveChild(newWorld, nodeIndex); // Restore original position
        
        // Update our reference to point to the new node
        WorldNode = newWorld;
        
        ILog.Print("World state loaded successfully");
    }
}