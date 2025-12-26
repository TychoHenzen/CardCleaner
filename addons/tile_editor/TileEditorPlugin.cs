#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorPlugin : EditorPlugin
{
    private Control? _dock;

    public override void _EnterTree()
    {
        try
        {
            _dock = new TileEditorDock();
            AddControlToBottomPanel(_dock, "Tile Editor");
            GD.Print("[TileEditorPlugin] Plugin loaded successfully");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorPlugin] Failed to initialize: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }

    public override void _ExitTree()
    {
        if (_dock != null)
        {
            RemoveControlFromBottomPanel(_dock);
            _dock.QueueFree();
            _dock = null;
        }
    }
}
#endif
