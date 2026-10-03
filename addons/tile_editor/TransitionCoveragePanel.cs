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

}
#endif
