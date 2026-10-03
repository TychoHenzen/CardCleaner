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
    private readonly TileEditorService? _service;
    private VBoxContainer? _container;
    private Label? _summaryLabel;
    private Button? _compactIdsButton;
    private Button? _removeDuplicatesButton;
    private List<AtlasSourceInfo> _currentUnusedSources = new();

    // Required by Godot for [Tool] classes
    public UnusedSourcesPanel() { }

    public UnusedSourcesPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.TileModified += _ => RefreshDisplay();
        _service.TileAdded += _ => RefreshDisplay();
        _service.TileRemoved += _ => RefreshDisplay();
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
            DialogText =
                $"Remove {_currentUnusedSources.Count} unused source(s) from the TileSet?\n\n" +
                $"{sourceNames}\n\nThis will modify and save the TileSet resource.",
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

    private void OnCompactIdsPressed()
    {
        if (_service.AreSourceIdsContiguous())
        {
            var infoDialog = new AcceptDialog
            {
                DialogText = "Atlas source IDs are already contiguous.",
                Title = "No Changes Needed"
            };
            infoDialog.Confirmed += () => infoDialog.QueueFree();
            AddChild(infoDialog);
            infoDialog.PopupCentered();
            return;
        }

        var allSources = _service.GetAvailableAtlasSources();
        var sourceList = string.Join("\n", allSources.Select(s => $"  [{s.SourceId}] → [{allSources.IndexOf(s)}]"));

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Compact {allSources.Count} atlas source IDs to be contiguous?\n\n" +
                         $"ID remapping:\n{sourceList}\n\n" +
                         "This will:\n" +
                         "• Renumber all atlas sources in the TileSet\n" +
                         "• Update all tile sourceId references in tiles.json\n\n" +
                         "Both the TileSet and tiles.json will be saved.",
            Title = "Confirm Compact Atlas IDs",
            Size = new Vector2I(500, 350)
        };
        dialog.Confirmed += () =>
        {
            ExecuteCompactIds();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ExecuteCompactIds()
    {
        var mapping = _service.CompactAtlasSourceIds();

        if (mapping.Count == 0)
        {
            var infoDialog = new AcceptDialog
            {
                DialogText = "No changes were made. IDs were already contiguous or an error occurred.",
                Title = "Compact Complete"
            };
            infoDialog.Confirmed += () => infoDialog.QueueFree();
            AddChild(infoDialog);
            infoDialog.PopupCentered();
            return;
        }

        // Save tiles.json with updated sourceIds
        var (success, message) = _service.SaveTiles();

        if (success)
        {
            var successDialog = new AcceptDialog
            {
                DialogText = $"Successfully compacted {mapping.Count} atlas source IDs.\n\n" +
                             $"Remapped IDs:\n" +
                             string.Join("\n", mapping.Select(kvp => $"  {kvp.Key} → {kvp.Value}")) +
                             "\n\nBoth TileSet and tiles.json have been saved.",
                Title = "Compact Complete"
            };
            successDialog.Confirmed += () => successDialog.QueueFree();
            AddChild(successDialog);
            successDialog.PopupCentered();
        }
        else
        {
            var errorDialog = new AcceptDialog
            {
                DialogText = $"TileSet was updated but failed to save tiles.json:\n{message}",
                Title = "Partial Success"
            };
            errorDialog.Confirmed += () => errorDialog.QueueFree();
            AddChild(errorDialog);
            errorDialog.PopupCentered();
        }

        RefreshDisplay();
    }

    private void OnRemoveDuplicatesPressed()
    {
        var duplicateGroups = _service.FindDuplicateSources();
        if (duplicateGroups.Count == 0)
        {
            var infoDialog = new AcceptDialog
            {
                DialogText = "No duplicate sources found. All textures have unique content.",
                Title = "No Duplicates"
            };
            infoDialog.Confirmed += () => infoDialog.QueueFree();
            AddChild(infoDialog);
            infoDialog.PopupCentered();
            return;
        }

        var allSources = _service.GetAvailableAtlasSources();
        var duplicateCount = duplicateGroups.Sum(g => g.Count - 1);

        // Build a description of what will be removed
        var groupDescriptions = duplicateGroups.Select(group =>
        {
            var keepId = group[0];
            var keepSource = allSources.FirstOrDefault(s => s.SourceId == keepId);
            var keepName = keepSource?.Source?.Texture?.ResourcePath?.GetFile() ?? $"Source {keepId}";

            var removeIds = string.Join(", ", group.Skip(1));
            return $"  Keep [{keepId}] {keepName}, remove: [{removeIds}]";
        });

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Found {duplicateGroups.Count} group(s) with duplicate textures.\n" +
                         $"Will remove {duplicateCount} source(s) with higher IDs:\n\n" +
                         string.Join("\n", groupDescriptions) + "\n\n" +
                         "Tiles using removed sources will be reassigned to the kept source.\n" +
                         "Both the TileSet and tiles.json will be saved.",
            Title = "Confirm Remove Duplicates",
            Size = new Vector2I(550, 350)
        };
        dialog.Confirmed += () =>
        {
            ExecuteRemoveDuplicates();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ExecuteRemoveDuplicates()
    {
        var removedCount = _service.RemoveDuplicateSources();

        if (removedCount == 0)
        {
            var infoDialog = new AcceptDialog
            {
                DialogText = "No duplicates were removed. An error may have occurred.",
                Title = "Remove Duplicates"
            };
            infoDialog.Confirmed += () => infoDialog.QueueFree();
            AddChild(infoDialog);
            infoDialog.PopupCentered();
            return;
        }

        // Save tiles.json with updated sourceIds
        var (success, message) = _service.SaveTiles();

        if (success)
        {
            var successDialog = new AcceptDialog
            {
                DialogText = $"Successfully removed {removedCount} duplicate source(s).\n\n" +
                             "Tiles have been reassigned to use the sources with lower IDs.\n" +
                             "Both TileSet and tiles.json have been saved.",
                Title = "Duplicates Removed"
            };
            successDialog.Confirmed += () => successDialog.QueueFree();
            AddChild(successDialog);
            successDialog.PopupCentered();
        }
        else
        {
            var errorDialog = new AcceptDialog
            {
                DialogText = $"TileSet was updated but failed to save tiles.json:\n{message}",
                Title = "Partial Success"
            };
            errorDialog.Confirmed += () => errorDialog.QueueFree();
            AddChild(errorDialog);
            errorDialog.PopupCentered();
        }

        RefreshDisplay();
    }
}

#endif
