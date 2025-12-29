#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorPlugin : EditorPlugin
{
    private Control? _dock;
    private bool _isInitialized;

    public override void _EnterTree()
    {
        // Defer initialization to avoid issues during assembly reload
        CallDeferred(MethodName.InitializePlugin);
    }

    private void InitializePlugin()
    {
        if (_isInitialized) return;

        try
        {
            // Clean up any stale references first
            CleanupDock();

            _dock = new TileEditorDock();
            AddControlToBottomPanel(_dock, "Tile Editor");
            _isInitialized = true;
            GD.Print("[TileEditorPlugin] Plugin loaded successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorPlugin] Failed to initialize: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
            _isInitialized = false;
        }
    }

    private void CleanupDock()
    {
        if (_dock != null)
        {
            try
            {
                if (IsInstanceValid(_dock))
                {
                    RemoveControlFromBottomPanel(_dock);
                    _dock.QueueFree();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[TileEditorPlugin] Cleanup warning: {ex.Message}");
            }
            _dock = null;
        }
    }

    public override void _ExitTree()
    {
        CleanupDock();
        _isInitialized = false;
    }
}
#endif
