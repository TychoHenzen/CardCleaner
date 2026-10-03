using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Full-screen visual preview showing estimated biome distribution as vertical strips.
/// Each biome gets a strip with width proportional to its percentage.
/// </summary>
public partial class BiomeDistributionPreview : Control
{
    private const int LabelFontSize = 12;
    private const int PercentageFontSize = 14;

    private static readonly Dictionary<string, Color> BiomeColors = new()
    {
        { "plains", new Color(0.3f, 0.8f, 0.3f) },
        { "forest", new Color(0.1f, 0.5f, 0.1f) },
        { "desert", new Color(0.9f, 0.8f, 0.4f) },
        { "tundra", new Color(0.7f, 0.9f, 1.0f) },
        { "mountains", new Color(0.5f, 0.5f, 0.5f) },
        { "swamp", new Color(0.3f, 0.4f, 0.2f) }
    };

    private readonly List<ColorRect> _strips = new();
    private readonly List<Label> _nameLabels = new();
    private readonly List<Label> _percentLabels = new();
    private Label? _titleLabel;
    private Label? _emptyLabel;
    private Control? _stripsContainer;

    public override void _Ready()
    {
        // Title at the top
        _titleLabel = new Label
        {
            Text = "Biome Distribution Preview",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        _titleLabel.AddThemeFontSizeOverride("font_size", 16);
        _titleLabel.SetAnchorsPreset(LayoutPreset.TopWide);
        _titleLabel.OffsetBottom = 30;
        AddChild(_titleLabel);

        // Empty state label (centered)
        _emptyLabel = new Label
        {
            Text = "Place cards to see biome distribution",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = true
        };
        _emptyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        _emptyLabel.AddThemeFontSizeOverride("font_size", 14);
        _emptyLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_emptyLabel);

        // Container for strips (fills most of the screen)
        _stripsContainer = new Control();
        _stripsContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        _stripsContainer.OffsetTop = 35;
        _stripsContainer.OffsetBottom = -30;
        AddChild(_stripsContainer);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            // Re-layout strips when container resizes
            RelayoutStrips();
        }
    }

    /// <summary>
    /// Updates the preview with new biome distribution data.
    /// </summary>
    /// <param name="distribution">Dictionary of biome IDs to percentages (0-1)</param>
    public void UpdateDistribution(Dictionary<string, float> distribution)
    {
        var hasData = distribution.Count > 0 && distribution.Values.Any(v => v > 0.001f);

        if (_emptyLabel != null)
        {
            _emptyLabel.Visible = !hasData;
        }

        if (_titleLabel != null)
        {
            _titleLabel.Visible = hasData;
        }

        ClearStrips();

        if (!hasData || _stripsContainer == null) return;

        // Sort biomes by percentage (largest first) for better visual
        var sortedBiomes = distribution
            .Where(kv => kv.Value > 0.001f)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        // Create strips for each biome
        foreach (var (biomeId, percentage) in sortedBiomes)
            AddBiomeStrip(_stripsContainer, biomeId, percentage);

        // Layout the strips
        RelayoutStrips();
    }

    private void ClearStrips()
    {
        foreach (var strip in _strips) strip.QueueFree();
        foreach (var label in _nameLabels) label.QueueFree();
        foreach (var label in _percentLabels) label.QueueFree();
        _strips.Clear();
        _nameLabels.Clear();
        _percentLabels.Clear();
    }

    private void AddBiomeStrip(Control container, string biomeId, float percentage)
    {
        var color = BiomeColors.GetValueOrDefault(biomeId, Colors.Magenta);

        // Color strip
        var strip = new ColorRect { Color = color };
        container.AddChild(strip);
        _strips.Add(strip);

        // Biome name label (at top of strip)
        var nameLabel = new Label
        {
            Text = biomeId,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        nameLabel.AddThemeColorOverride("font_color", GetContrastColor(color));
        nameLabel.AddThemeFontSizeOverride("font_size", LabelFontSize);
        container.AddChild(nameLabel);
        _nameLabels.Add(nameLabel);

        // Percentage label (centered in strip)
        var percentLabel = new Label
        {
            Text = $"{percentage * 100:F0}%",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        percentLabel.AddThemeColorOverride("font_color", GetContrastColor(color));
        percentLabel.AddThemeFontSizeOverride("font_size", PercentageFontSize);
        container.AddChild(percentLabel);
        _percentLabels.Add(percentLabel);
    }

    private void RelayoutStrips()
    {
        if (_stripsContainer == null || _strips.Count == 0) return;

        var containerSize = _stripsContainer.Size;
        if (containerSize.X <= 0 || containerSize.Y <= 0) return;

        // Calculate total percentage for normalization
        var totalPercentage = 0f;
        for (var i = 0; i < _strips.Count; i++)
        {
            var percentText = _percentLabels[i].Text.TrimEnd('%');
            if (float.TryParse(percentText, out var pct))
            {
                totalPercentage += pct;
            }
        }

        if (totalPercentage <= 0) return;

        // Position each strip
        var currentX = 0f;
        for (var i = 0; i < _strips.Count; i++)
        {
            var percentText = _percentLabels[i].Text.TrimEnd('%');
            if (!float.TryParse(percentText, out var pct)) continue;

            var stripWidth = (pct / totalPercentage) * containerSize.X;

            // Position and size the strip
            _strips[i].Position = new Vector2(currentX, 0);
            _strips[i].Size = new Vector2(stripWidth, containerSize.Y);

            // Position name label at top of strip
            _nameLabels[i].Position = new Vector2(currentX + 4, 4);
            _nameLabels[i].Size = new Vector2(stripWidth - 8, 20);

            // Position percentage label centered in strip
            _percentLabels[i].Position = new Vector2(currentX, containerSize.Y / 2 - 10);
            _percentLabels[i].Size = new Vector2(stripWidth, 20);

            currentX += stripWidth;
        }
    }

    /// <summary>
    /// Gets a contrasting text color (black or white) for readability.
    /// </summary>
    private static Color GetContrastColor(Color background)
    {
        // Calculate luminance
        var luminance = 0.299f * background.R + 0.587f * background.G + 0.114f * background.B;
        return luminance > 0.5f ? Colors.Black : Colors.White;
    }

    /// <summary>
    /// Clears the preview to empty state.
    /// </summary>
    public void Clear()
    {
        UpdateDistribution(new Dictionary<string, float>());
    }
}
