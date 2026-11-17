#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

[Tool]
public partial class TileEditorPlugin : EditorPlugin
{
    private EditorDock? _editorDock;
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

            // The host builds the dock the first time its tab is shown; see TileEditorDockHost.
            _editorDock = new EditorDock { Title = "Tile Editor", DefaultSlot = EditorDock.DockSlot.Bottom };
            var host = new TileEditorDockHost();
            host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _editorDock.AddChild(host);
            AddDock(_editorDock);
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
        if (_editorDock != null)
        {
            try
            {
                if (IsInstanceValid(_editorDock))
                {
                    RemoveDock(_editorDock);
                    _editorDock.QueueFree();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[TileEditorPlugin] Cleanup warning: {ex.Message}");
            }
            _editorDock = null;
        }
    }

    public override void _ExitTree()
    {
        CleanupDock();
        _isInitialized = false;
    }
}
#endif
