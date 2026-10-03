#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Creates the tile editor tab panels for one service and adds them to a tab container
/// in their display order.
/// </summary>
internal sealed class TileEditorPanels
{
    internal TileEditorPanels(TileEditorService service)
    {
        Atlas = new TileAtlasPanel(service) { Name = "Tile Browser" };
        Properties = new TilePropertiesPanel(service) { Name = "Properties" };
        BiomePool = new BiomePoolPanel(service) { Name = "Biomes" };
        AutoTilePreview = new AutoTilePreviewPanel(service) { Name = "Auto-Tile Preview" };
        TmxPreview = new TmxPreviewPanel(service) { Name = "TMX Preview" };
        FormatEditor = new AutoTileFormatEditorPanel(service) { Name = "Formats" };
        UnusedSources = new UnusedSourcesPanel(service) { Name = "Unused Sources" };
        TransitionCoverage = new TransitionCoveragePanel(service) { Name = "Transitions" };
    }

    internal TileAtlasPanel Atlas { get; }

    internal TilePropertiesPanel Properties { get; }

    internal BiomePoolPanel BiomePool { get; }

    internal AutoTilePreviewPanel AutoTilePreview { get; }

    internal TmxPreviewPanel TmxPreview { get; }

    internal AutoTileFormatEditorPanel FormatEditor { get; }

    internal UnusedSourcesPanel UnusedSources { get; }

    internal TransitionCoveragePanel TransitionCoverage { get; }

    /// <summary>
    /// Adds every panel as a tab. The tab order is the order of the properties above.
    /// </summary>
    internal void AddTo(TabContainer tabs)
    {
        tabs.AddChild(Atlas);
        tabs.AddChild(Properties);
        tabs.AddChild(BiomePool);
        tabs.AddChild(AutoTilePreview);
        tabs.AddChild(TmxPreview);
        tabs.AddChild(FormatEditor);
        tabs.AddChild(UnusedSources);
        tabs.AddChild(TransitionCoverage);
    }
}
#endif
