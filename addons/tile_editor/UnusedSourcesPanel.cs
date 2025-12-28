#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel displaying TileSet atlas sources that are not referenced by any tiles.
/// Helps identify cleanup opportunities for removing unused textures.
/// </summary>
[Tool]
public partial class UnusedSourcesPanel : ScrollContainer
{
    private readonly TileEditorService _service;
    private VBoxContainer? _container;
    private Label? _summaryLabel;
    private List<AtlasSourceInfo> _currentUnusedSources = new();

    public UnusedSourcesPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.TileModified += _ => RefreshDisplay();
        _service.TileAdded += _ => RefreshDisplay();
        _service.TileRemoved += _ => RefreshDisplay();
    }

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;

        _container = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_container);

        // Summary label at the top
        _summaryLabel = new Label
        {
            Text = "Loading...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _summaryLabel.AddThemeFontSizeOverride("font_size", 14);
        _container.AddChild(_summaryLabel);

        _container.AddChild(new HSeparator());
    }

    private void RefreshDisplay()
    {
        if (_container == null || _summaryLabel == null) return;

        // Clear existing entries (keep summary label and separator)
        var children = _container.GetChildren().ToList();
        for (var i = 2; i < children.Count; i++)
        {
            children[i].QueueFree();
        }

        var allSources = _service.GetAvailableAtlasSources();
        var usedSourceIds = _service.GetUsedSourceIds();

        _currentUnusedSources = allSources
            .Where(s => !usedSourceIds.Contains(s.SourceId))
            .OrderBy(s => s.SourceId)
            .ToList();

        if (_currentUnusedSources.Count == 0)
        {
            _summaryLabel.Text = "All sources are in use";
            _summaryLabel.Modulate = new Color(0.5f, 0.9f, 0.5f);

            var infoLabel = new Label
            {
                Text = $"All {allSources.Count} atlas sources have at least one tile referencing them.",
                AutowrapMode = TextServer.AutowrapMode.Word,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            infoLabel.AddThemeFontSizeOverride("font_size", 12);
            _container.AddChild(infoLabel);
        }
        else
        {
            _summaryLabel.Text = $"{_currentUnusedSources.Count} unused source(s) found";
            _summaryLabel.Modulate = new Color(0.9f, 0.7f, 0.3f);

            var infoLabel = new Label
            {
                Text = "These atlas sources exist in the TileSet but have no tiles using them.",
                AutowrapMode = TextServer.AutowrapMode.Word,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Modulate = new Color(0.7f, 0.7f, 0.7f)
            };
            infoLabel.AddThemeFontSizeOverride("font_size", 11);
            _container.AddChild(infoLabel);

            // Remove All button
            var removeAllButton = new Button
            {
                Text = $"Remove All {_currentUnusedSources.Count} Unused Sources",
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            removeAllButton.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            removeAllButton.Pressed += OnRemoveAllPressed;
            _container.AddChild(removeAllButton);

            _container.AddChild(new HSeparator());

            foreach (var source in _currentUnusedSources)
            {
                var entry = new UnusedSourceEntry(source);
                _container.AddChild(entry);
            }
        }
    }

    private void OnRemoveAllPressed()
    {
        if (_currentUnusedSources.Count == 0) return;

        var sourceNames = string.Join("\n", _currentUnusedSources.Select(s =>
        {
            var path = s.Source?.Texture?.ResourcePath ?? "Unknown";
            var name = path.Contains('/') ? path.GetFile() : path;
            return $"  [{s.SourceId}] {name}";
        }));

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Remove {_currentUnusedSources.Count} unused source(s) from the TileSet?\n\n{sourceNames}\n\nThis will modify and save the TileSet resource.",
            Title = "Confirm Removal",
            Size = new Vector2I(450, 300)
        };
        dialog.Confirmed += () =>
        {
            RemoveAllUnusedSources();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void RemoveAllUnusedSources()
    {
        var sourceIds = _currentUnusedSources.Select(s => s.SourceId).ToList();
        var removed = 0;
        var failed = 0;

        foreach (var sourceId in sourceIds)
        {
            if (_service.RemoveSource(sourceId))
                removed++;
            else
                failed++;
        }

        GD.Print($"[UnusedSourcesPanel] Removed {removed} sources, {failed} failed");
        RefreshDisplay();
    }
}

/// <summary>
/// Entry for a single unused atlas source
/// </summary>
[Tool]
public partial class UnusedSourceEntry : PanelContainer
{
    private readonly AtlasSourceInfo _source;

    public UnusedSourceEntry(AtlasSourceInfo source)
    {
        _source = source;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var hbox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(hbox);

        // Source ID badge
        var idLabel = new Label
        {
            Text = $"[{_source.SourceId}]",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        idLabel.AddThemeFontSizeOverride("font_size", 12);
        idLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.9f));
        hbox.AddChild(idLabel);

        // Texture name
        var texturePath = _source.Source?.Texture?.ResourcePath ?? "Unknown texture";
        var textureName = texturePath.Contains('/') ? texturePath.GetFile() : texturePath;

        var nameLabel = new Label
        {
            Text = textureName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = texturePath
        };
        hbox.AddChild(nameLabel);

        // Status indicator
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
