#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Builds the tile editor toolbar (save, reload, TMX tools and status label) and exposes its
/// controls so the owning dock can connect the actions.
/// </summary>
internal sealed class TileEditorToolbar
{
    internal required Button SaveButton { get; init; }

    internal required Button ReloadButton { get; init; }

    internal required Button CompileTmxButton { get; init; }

    internal required Button InsertPropsButton { get; init; }

    internal required Label StatusLabel { get; init; }

    /// <summary>
    /// Creates the toolbar controls and adds them to <paramref name="parent"/> in a new row.
    /// </summary>
    internal static TileEditorToolbar CreateIn(Control parent)
    {
        var toolbar = new HBoxContainer();
        parent.AddChild(toolbar);

        var saveButton = new Button { Text = "Save", Disabled = true };
        toolbar.AddChild(saveButton);

        var reloadButton = new Button { Text = "Reload" };
        toolbar.AddChild(reloadButton);

        toolbar.AddChild(new VSeparator());

        // TMX tools
        var compileTmxButton = new Button
        {
            Text = "Compile from TMX",
            TooltipText = "Compile atlas from TMX/TSX files"
        };
        toolbar.AddChild(compileTmxButton);

        var insertPropsButton = new Button
        {
            Text = "Insert TSX Props",
            TooltipText = "Insert default properties into TSX files"
        };
        toolbar.AddChild(insertPropsButton);

        toolbar.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.Expand });

        var statusLabel = new Label { Text = "Loading..." };
        toolbar.AddChild(statusLabel);

        return new TileEditorToolbar
        {
            SaveButton = saveButton,
            ReloadButton = reloadButton,
            CompileTmxButton = compileTmxButton,
            InsertPropsButton = insertPropsButton,
            StatusLabel = statusLabel
        };
    }
}
#endif
