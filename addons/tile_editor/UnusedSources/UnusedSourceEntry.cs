#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Entry for a single unused atlas source.
/// </summary>
[Tool]
public partial class UnusedSourceEntry : PanelContainer
{
    private readonly AtlasSourceInfo? _source;

    public UnusedSourceEntry() { }

    public UnusedSourceEntry(AtlasSourceInfo source)
    {
        _source = source;
    }

    public override void _Ready()
    {
        if (_source == null)
            return;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var hbox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(hbox);
        AddSourceIdLabel(hbox);
        AddTextureNameLabel(hbox);
        AddStatusLabel(hbox);
    }

    private void AddSourceIdLabel(HBoxContainer hbox)
    {
        var idLabel = new Label
        {
            Text = $"[{_source!.SourceId}]",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        idLabel.AddThemeFontSizeOverride("font_size", 12);
        idLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.9f));
        hbox.AddChild(idLabel);
    }

    private void AddTextureNameLabel(HBoxContainer hbox)
    {
        var texturePath = _source!.Source?.Texture?.ResourcePath ?? "Unknown texture";
        var textureName = texturePath.Contains('/') ? texturePath.GetFile() : texturePath;
        var nameLabel = new Label
        {
            Text = textureName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = texturePath
        };
        hbox.AddChild(nameLabel);
    }

    private void AddStatusLabel(HBoxContainer hbox)
    {
        var statusLabel = new Label
        {
            Text = "0 tiles",
            Modulate = new Color(0.8f, 0.4f, 0.4f)
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        hbox.AddChild(statusLabel);
    }
}
#endif
