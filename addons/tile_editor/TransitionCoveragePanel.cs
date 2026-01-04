#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel displaying terrain transition coverage matrix.
/// Shows which terrain pairs have transitions defined (fixed or compositable) and which are missing.
/// </summary>
[Tool]
public partial class TransitionCoveragePanel : ScrollContainer
{
    private readonly TileEditorService? _service;
    private VBoxContainer? _container;
    private Label? _summaryLabel;
    private GridContainer? _matrixGrid;
    private TransitionCoverageMatrix? _coverageMatrix;

    // Required by Godot for [Tool] classes
    public TransitionCoveragePanel() { }

    public TransitionCoveragePanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.TileModified += _ => RefreshDisplay();
        _service.TileAdded += _ => RefreshDisplay();
        _service.TileRemoved += _ => RefreshDisplay();
    }

    public override void _Ready()
    {
        // Guard for Godot's parameterless constructor case
        if (_service == null) return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Auto;

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

        // Legend
        var legendBox = new HBoxContainer();
        legendBox.AddChild(CreateLegendItem("Compositable", new Color(0.3f, 0.7f, 0.3f)));
        legendBox.AddChild(CreateLegendItem("Fixed", new Color(0.3f, 0.5f, 0.8f)));
        legendBox.AddChild(CreateLegendItem("Missing", new Color(0.7f, 0.3f, 0.3f)));
        legendBox.AddChild(CreateLegendItem("Self (N/A)", new Color(0.3f, 0.3f, 0.3f)));
        _container.AddChild(legendBox);

        _container.AddChild(new HSeparator());

        // Matrix grid placeholder
        _matrixGrid = new GridContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _container.AddChild(_matrixGrid);

        RefreshDisplay();
    }

    private static Control CreateLegendItem(string text, Color color)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        var colorRect = new ColorRect
        {
            CustomMinimumSize = new Vector2(12, 12),
            Color = color
        };
        hbox.AddChild(colorRect);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 11);
        hbox.AddChild(label);

        var spacer = new Control { CustomMinimumSize = new Vector2(16, 0) };
        hbox.AddChild(spacer);

        return hbox;
    }

    private void RefreshDisplay()
    {
        if (_service == null || _container == null || _summaryLabel == null || _matrixGrid == null) return;

        // Calculate coverage matrix
        _coverageMatrix = CalculateCoverageMatrix();

        // Update summary
        var total = _coverageMatrix.TotalTransitions;
        var covered = _coverageMatrix.CoveredCount;
        var missing = _coverageMatrix.MissingCount;

        if (total == 0)
        {
            _summaryLabel.Text = "No terrain tiles found";
            _summaryLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
        }
        else if (missing == 0)
        {
            _summaryLabel.Text = $"All {covered} transitions covered";
            _summaryLabel.Modulate = new Color(0.5f, 0.9f, 0.5f);
        }
        else
        {
            var percentage = (float)covered / total * 100;
            _summaryLabel.Text = $"{covered}/{total} transitions covered ({percentage:F0}%), {missing} missing";
            _summaryLabel.Modulate = new Color(0.9f, 0.7f, 0.3f);
        }

        // Clear and rebuild matrix grid
        foreach (var child in _matrixGrid.GetChildren())
        {
            child.QueueFree();
        }

        if (_coverageMatrix.TerrainIds.Count == 0) return;

        // Set columns: 1 for row header + N terrain columns
        var terrainCount = _coverageMatrix.TerrainIds.Count;
        _matrixGrid.Columns = terrainCount + 1;

        // Header row: empty corner cell + column headers
        _matrixGrid.AddChild(CreateHeaderCell("Inner→Outer"));
        foreach (var terrainId in _coverageMatrix.TerrainIds)
        {
            _matrixGrid.AddChild(CreateHeaderCell(terrainId));
        }

        // Data rows
        foreach (var innerTerrain in _coverageMatrix.TerrainIds)
        {
            // Row header
            _matrixGrid.AddChild(CreateHeaderCell(innerTerrain));

            // Cells for each outer terrain
            foreach (var outerTerrain in _coverageMatrix.TerrainIds)
            {
                var status = _coverageMatrix.GetStatus(innerTerrain, outerTerrain);
                _matrixGrid.AddChild(CreateStatusCell(status, innerTerrain, outerTerrain));
            }
        }
    }

    private static Control CreateHeaderCell(string text)
    {
        var label = new Label
        {
            Text = text.Length > 10 ? text[..10] + "…" : text,
            TooltipText = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(70, 24)
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        return label;
    }

    private static Control CreateStatusCell(TransitionStatus status, string inner, string outer)
    {
        var color = status switch
        {
            TransitionStatus.Compositable => new Color(0.3f, 0.7f, 0.3f),
            TransitionStatus.Fixed => new Color(0.3f, 0.5f, 0.8f),
            TransitionStatus.Missing => new Color(0.7f, 0.3f, 0.3f),
            TransitionStatus.Self => new Color(0.3f, 0.3f, 0.3f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        var symbol = "▓";

        var tooltip = status switch
        {
            TransitionStatus.Compositable => $"{inner}→{outer}: Covered by compositable auto-tile",
            TransitionStatus.Fixed => $"{inner}→{outer}: Covered by fixed transition",
            TransitionStatus.Missing => $"{inner}→{outer}: No transition defined",
            TransitionStatus.Self => $"{inner}→{outer}: Same terrain (no transition needed)",
            _ => ""
        };

        var cell = new Label
        {
            Text = symbol,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(70, 20),
            Modulate = color,
            TooltipText = tooltip
        };
        cell.AddThemeFontSizeOverride("font_size", 14);
        return cell;
    }

    private TransitionCoverageMatrix CalculateCoverageMatrix()
    {
        var matrix = new TransitionCoverageMatrix();
        var allTiles = _service.AllTiles.ToList();

        // Identify base terrain tiles (simple terrain layer tiles without auto-tile variants)
        var baseTerrains = allTiles
            .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
            .Select(t => t.Id)
            .ToHashSet();

        // Identify auto-tiles and collect all unique inner terrain IDs
        var autoTiles = allTiles.Where(t => t.HasAutoTileVariants).ToList();
        var innerTerrainIds = new HashSet<string>();

        foreach (var autoTile in autoTiles)
        {
            // When InnerTerrainId is null, the auto-tile uses its own ID as the inner terrain
            var innerTerrain = autoTile.InnerTerrainId ?? autoTile.Id;
            innerTerrainIds.Add(innerTerrain);
        }

        // Matrix includes both base terrains AND auto-tile inner terrains
        // (auto-tiles using themselves as inner terrain should appear in the matrix)
        var allTerrainIds = baseTerrains.Union(innerTerrainIds).OrderBy(id => id).ToList();
        matrix.TerrainIds = allTerrainIds;

        // Now calculate coverage
        foreach (var autoTile in autoTiles)
        {
            var innerTerrain = autoTile.InnerTerrainId ?? autoTile.Id;

            if (autoTile.IsCompositable)
            {
                // Compositable covers this inner terrain with ALL terrains in the matrix
                // (both base terrains and other auto-tile inner terrains - dominance determines which is on top)
                foreach (var outerTerrain in allTerrainIds)
                {
                    if (innerTerrain != outerTerrain)
                    {
                        // Mark both directions - a terrain boundary only needs one transition
                        // (the higher-dominance terrain's border is used)
                        matrix.SetCoverage(innerTerrain, outerTerrain, TransitionStatus.Compositable);
                        matrix.SetCoverage(outerTerrain, innerTerrain, TransitionStatus.Compositable);
                    }
                }
            }
            else if (autoTile.IsFixedTransition)
            {
                // Fixed covers only the specific pair (both directions)
                var outerTerrain = autoTile.OuterTerrainId!;
                if (allTerrainIds.Contains(outerTerrain) && innerTerrain != outerTerrain)
                {
                    matrix.SetCoverage(innerTerrain, outerTerrain, TransitionStatus.Fixed);
                    matrix.SetCoverage(outerTerrain, innerTerrain, TransitionStatus.Fixed);
                }
            }
        }

        return matrix;
    }
}

/// <summary>
/// Status of a terrain transition pair.
/// </summary>
public enum TransitionStatus
{
    /// <summary>No transition defined for this pair.</summary>
    Missing,
    /// <summary>Covered by a compositable auto-tile.</summary>
    Compositable,
    /// <summary>Covered by a fixed transition auto-tile.</summary>
    Fixed,
    /// <summary>Same terrain - no transition needed.</summary>
    Self
}

/// <summary>
/// Matrix tracking transition coverage between terrain pairs.
/// </summary>
public class TransitionCoverageMatrix
{
    public List<string> TerrainIds { get; set; } = new();
    private readonly Dictionary<(string inner, string outer), TransitionStatus> _coverage = new();

    public int TotalTransitions => TerrainIds.Count > 0 ? TerrainIds.Count * (TerrainIds.Count - 1) : 0;

    public int CoveredCount => _coverage.Count(kvp =>
        kvp.Value is TransitionStatus.Compositable or TransitionStatus.Fixed);

    public int MissingCount => TotalTransitions - CoveredCount;

    public void SetCoverage(string innerTerrain, string outerTerrain, TransitionStatus status)
    {
        _coverage[(innerTerrain, outerTerrain)] = status;
    }

    public TransitionStatus GetStatus(string innerTerrain, string outerTerrain)
    {
        if (innerTerrain == outerTerrain)
            return TransitionStatus.Self;

        return _coverage.TryGetValue((innerTerrain, outerTerrain), out var status)
            ? status
            : TransitionStatus.Missing;
    }
}
#endif
