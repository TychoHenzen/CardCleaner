#if TOOLS
using System;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// The control the plugin puts in its editor dock. It stays empty until it is first shown, then builds the
/// <see cref="TileEditorDock" /> inside itself. The dock loads the whole TileSet when it is built (576 textures,
/// about 1.3 GB, plus its panels), so an editor session that never opens the Tile Editor tab never pays for it.
/// </summary>
[Tool]
public partial class TileEditorDockHost : Control
{
    private const float MinimumHeight = 300f;

    internal TileEditorDock? Dock { get; private set; }

    /// <summary>Runs on the dock before it enters the tree, so a test can set its hooks first.</summary>
    internal Action<TileEditorDock>? ConfigureDock { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, MinimumHeight);
        VisibilityChanged += OnVisibilityChanged;
        // ASSUMPTION: a dock slot hides a tab that is not current in the same frame it adds it, so a check
        // deferred to the end of the frame sees whether the Tile Editor tab is really showing.
        CallDeferred(MethodName.BuildIfShown);
    }

    private void OnVisibilityChanged()
    {
        // Deferred for the same reason as in _Ready: a container can show a child for a moment while it settles.
        CallDeferred(MethodName.BuildIfShown);
    }

    private void BuildIfShown()
    {
        if (Dock != null || !IsInsideTree() || !IsVisibleInTree())
            return;

        var dock = new TileEditorDock();
        dock.SetAnchorsPreset(LayoutPreset.FullRect);
        ConfigureDock?.Invoke(dock);
        Dock = dock;
        AddChild(dock);
    }
}
#endif
