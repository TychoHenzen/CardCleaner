#if TOOLS
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class UnusedSourcesPanel
{
    private void RefreshDisplay()
    {
        if (_container == null || _summaryLabel == null)
            return;

        UpdateActionButtonStates();
        ClearSourceEntries();
        var allSources = _service!.GetAvailableAtlasSources();
        var usedSourceIds = _service.GetUsedSourceIds();
        _currentUnusedSources = allSources
            .Where(source => !usedSourceIds.Contains(source.SourceId))
            .OrderBy(source => source.SourceId)
            .ToList();
        if (_currentUnusedSources.Count == 0)
            ShowNoUnusedSources(allSources.Count);
        else
            ShowUnusedSources();
    }

    private void UpdateActionButtonStates()
    {
        if (_compactIdsButton != null)
        {
            var isContiguous = _service!.AreSourceIdsContiguous();
            _compactIdsButton.Disabled = isContiguous;
            _compactIdsButton.TooltipText = isContiguous
                ? "Atlas source IDs are already contiguous"
                : "Renumber atlas source IDs to be contiguous (0, 1, 2, ...) and update tiles.json";
        }

        if (_removeDuplicatesButton != null)
        {
            var duplicateGroups = _service!.FindDuplicateSources();
            var hasDuplicates = duplicateGroups.Count > 0;
            var duplicateCount = duplicateGroups.Sum(group => group.Count - 1);
            _removeDuplicatesButton.Disabled = !hasDuplicates;
            _removeDuplicatesButton.TooltipText = hasDuplicates
                ? $"Remove {duplicateCount} duplicate source(s) with identical texture content"
                : "No duplicate sources found";
        }
    }

    private void ClearSourceEntries()
    {
        var children = _container!.GetChildren().ToList();
        for (var index = 4; index < children.Count; index++)
            children[index].QueueFree();
    }

    private void ShowNoUnusedSources(int sourceCount)
    {
        _summaryLabel!.Text = "All sources are in use";
        _summaryLabel.Modulate = new Color(0.5f, 0.9f, 0.5f);
        var infoLabel = new Label
        {
            Text = $"All {sourceCount} atlas sources have at least one tile referencing them.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        infoLabel.AddThemeFontSizeOverride("font_size", 12);
        _container!.AddChild(infoLabel);
    }

    private void ShowUnusedSources()
    {
        _summaryLabel!.Text = $"{_currentUnusedSources.Count} unused source(s) found";
        _summaryLabel.Modulate = new Color(0.9f, 0.7f, 0.3f);
        var infoLabel = new Label
        {
            Text = "These atlas sources exist in the TileSet but have no tiles using them.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        infoLabel.AddThemeFontSizeOverride("font_size", 11);
        _container!.AddChild(infoLabel);
        AddRemoveAllButton();
        _container.AddChild(new HSeparator());
        foreach (var source in _currentUnusedSources)
            _container.AddChild(new UnusedSourceEntry(source));
    }

    private void AddRemoveAllButton()
    {
        var removeAllButton = new Button
        {
            Text = $"Remove All {_currentUnusedSources.Count} Unused Sources",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        removeAllButton.AddThemeColorOverride(
            "font_color",
            new Color(1f, 0.4f, 0.4f));
        removeAllButton.Pressed += OnRemoveAllPressed;
        _container!.AddChild(removeAllButton);
    }
}
#endif
