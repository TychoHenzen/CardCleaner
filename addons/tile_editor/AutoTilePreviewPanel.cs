#if TOOLS
using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
///     Panel showing auto-tile variants in a contextual map-like preview.
///     Displays tiles as they would appear in-game with proper neighbor-based variant selection.
/// </summary>
[Tool]
public partial class AutoTilePreviewPanel : ScrollContainer
{
    private readonly TileEditorService _service;
    private OptionButton? _tileSelector;
    private OptionButton? _baseTileSelector;
    private HSlider? _scaleSlider;
    private Label? _scaleLabel;
    private Label? _infoLabel;
    private AutoTileMapPreview? _mapPreview;
    private string? _selectedTileId;
    private string? _selectedBaseTileId;

    public AutoTilePreviewPanel(TileEditorService service)
    {
        _service = service;
    }

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;

        var mainVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(mainVBox);

        // Header
        var header = new Label
        {
            Text = "Auto-Tile Preview",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        mainVBox.AddChild(header);

        mainVBox.AddChild(new HSeparator());

        // Tile selector row
        var tileRow = new HBoxContainer();
        tileRow.AddChild(new Label { Text = "Overlay tile:", CustomMinimumSize = new Vector2(100, 0) });
        _tileSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tileSelector.ItemSelected += OnTileSelected;
        tileRow.AddChild(_tileSelector);
        mainVBox.AddChild(tileRow);

        // Base tile selector row
        var baseRow = new HBoxContainer();
        baseRow.AddChild(new Label { Text = "Base tile:", CustomMinimumSize = new Vector2(100, 0) });
        _baseTileSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _baseTileSelector.ItemSelected += OnBaseTileSelected;
        baseRow.AddChild(_baseTileSelector);
        mainVBox.AddChild(baseRow);

        // Scale slider row
        var scaleRow = new HBoxContainer();
        scaleRow.AddChild(new Label { Text = "Scale:", CustomMinimumSize = new Vector2(100, 0) });
        _scaleSlider = new HSlider
        {
            MinValue = 2,
            MaxValue = 8,
            Step = 1,
            Value = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSlider.ValueChanged += OnScaleChanged;
        scaleRow.AddChild(_scaleSlider);
        _scaleLabel = new Label { Text = "4x", CustomMinimumSize = new Vector2(30, 0) };
        scaleRow.AddChild(_scaleLabel);
        mainVBox.AddChild(scaleRow);

        mainVBox.AddChild(new HSeparator());

        // Info label
        _infoLabel = new Label
        {
            Text = "Select a tile with auto-tile variants to preview.",
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        mainVBox.AddChild(_infoLabel);

        // Map preview in scroll container
        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto
        };

        _mapPreview = new AutoTileMapPreview(_service);
        _mapPreview.BitmaskChanged += OnBitmaskChanged;
        previewScroll.AddChild(_mapPreview);
        mainVBox.AddChild(previewScroll);

        // Populate dropdowns after service loads
        CallDeferred(MethodName.PopulateDropdowns);
    }

    private void OnBitmaskChanged(int bitmask, string description)
    {
        if (_infoLabel != null)
        {
            _infoLabel.Text = $"Click tiles to toggle. {description}";
        }
    }

    private void PopulateDropdowns()
    {
        PopulateTileSelector();
        PopulateBaseTileSelector();
    }

    private void PopulateTileSelector()
    {
        _tileSelector!.Clear();
        _tileSelector.AddItem("-- Select tile --", 0);

        var index = 1;
        foreach (var tile in _service.AllTiles)
        {
            if (tile.HasAutoTileVariants)
            {
                _tileSelector.AddItem($"{tile.Name} ({tile.Id})", index);
                _tileSelector.SetItemMetadata(index, tile.Id);
                index++;
            }
        }
    }

    private void PopulateBaseTileSelector()
    {
        _baseTileSelector!.Clear();
        _baseTileSelector.AddItem("-- Select base --", 0);

        var index = 1;
        foreach (var tile in _service.AllTiles)
        {
            if (tile.Layer.Equals("terrain", StringComparison.OrdinalIgnoreCase))
            {
                _baseTileSelector.AddItem($"{tile.Name} ({tile.Id})", index);
                _baseTileSelector.SetItemMetadata(index, tile.Id);
                index++;
            }
        }
    }

    private void OnTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedTileId = null;
            ClearPreview();
            return;
        }

        _selectedTileId = _tileSelector!.GetItemMetadata((int)index).AsString();
        RefreshPreview();
    }

    private void OnBaseTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedBaseTileId = null;
        }
        else
        {
            _selectedBaseTileId = _baseTileSelector!.GetItemMetadata((int)index).AsString();
        }
        RefreshPreview();
    }

    private void OnScaleChanged(double value)
    {
        _scaleLabel!.Text = $"{(int)value}x";
        _mapPreview?.SetScale((float)value);
    }

    private void ClearPreview()
    {
        _infoLabel!.Text = "Select a tile with auto-tile variants to preview.";
        _mapPreview?.ClearPreview();
    }

    private void RefreshPreview()
    {
        if (string.IsNullOrEmpty(_selectedTileId))
        {
            ClearPreview();
            return;
        }

        var tile = _service.GetTile(_selectedTileId);
        if (tile == null || !tile.HasAutoTileVariants)
        {
            _infoLabel!.Text = "Selected tile has no auto-tile variants.";
            _mapPreview?.ClearPreview();
            return;
        }

        var isBlob47 = tile.AutoTileFormat == "blob47";
        var formatName = isBlob47 ? "blob47" : "corner16";
        _infoLabel!.Text = $"Click tiles to toggle neighbors ({formatName}). Bitmask: 0";

        EditableTile? baseTile = null;
        if (!string.IsNullOrEmpty(_selectedBaseTileId))
        {
            baseTile = _service.GetTile(_selectedBaseTileId);
        }

        _mapPreview?.SetTiles(tile, baseTile, (float)_scaleSlider!.Value);
    }

    public void Refresh()
    {
        PopulateDropdowns();
        if (!string.IsNullOrEmpty(_selectedTileId))
        {
            RefreshPreview();
        }
    }
}

/// <summary>
///     Interactive 3x3 grid control for exploring auto-tile bitmask configurations.
///     Center tile is always the overlay; surrounding 8 tiles are toggleable.
/// </summary>
[Tool]
public partial class AutoTileMapPreview : Control
{
    /// <summary>
    ///     Event fired when the bitmask changes due to user interaction.
    /// </summary>
    [Signal]
    public delegate void BitmaskChangedEventHandler(int bitmask, string description);

    private readonly TileEditorService _service;
    private EditableTile? _overlayTile;
    private EditableTile? _baseTile;
    private float _scale = 4f;
    private Vector2I _tileSize = new(16, 16);

    // Toggle state for the 8 surrounding tiles (3x3 grid, center is always overlay)
    // Layout: [0]=NW, [1]=N, [2]=NE, [3]=W, [4]=E, [5]=SW, [6]=S, [7]=SE
    private readonly bool[] _neighborToggles = new bool[8];

    // Grid positions for each neighbor index (row, col offsets from center)
    private static readonly (int row, int col)[] NeighborOffsets =
    {
        (-1, -1), // 0: NW
        (-1, 0),  // 1: N
        (-1, 1),  // 2: NE
        (0, -1),  // 3: W
        (0, 1),   // 4: E
        (1, -1),  // 5: SW
        (1, 0),   // 6: S
        (1, 1)    // 7: SE
    };

    public AutoTileMapPreview(TileEditorService service)
    {
        _service = service;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void SetScale(float scale)
    {
        _scale = scale;
        UpdateSize();
        QueueRedraw();
    }

    public void SetTiles(EditableTile? overlayTile, EditableTile? baseTile, float scale)
    {
        _overlayTile = overlayTile;
        _baseTile = baseTile;
        _scale = scale;
        _tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        UpdateSize();
        QueueRedraw();
        EmitBitmaskChanged();
    }

    public void ClearPreview()
    {
        _overlayTile = null;
        _baseTile = null;
        UpdateSize();
        QueueRedraw();
    }

    /// <summary>
    ///     Get the current toggle state array for external display.
    /// </summary>
    public bool[] GetNeighborToggles() => _neighborToggles;

    private void UpdateSize()
    {
        if (_overlayTile == null)
        {
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        CustomMinimumSize = new Vector2(3 * scaledTileSize.X, 3 * scaledTileSize.Y);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_overlayTile == null) return;

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
            var col = (int)(mouseButton.Position.X / scaledTileSize.X);
            var row = (int)(mouseButton.Position.Y / scaledTileSize.Y);

            // Don't toggle center cell (1,1)
            if (row == 1 && col == 1) return;

            // Find which neighbor index this corresponds to
            var neighborIndex = GetNeighborIndex(row, col);
            if (neighborIndex >= 0)
            {
                _neighborToggles[neighborIndex] = !_neighborToggles[neighborIndex];
                QueueRedraw();
                EmitBitmaskChanged();
            }
        }
    }

    private int GetNeighborIndex(int row, int col)
    {
        for (var i = 0; i < NeighborOffsets.Length; i++)
        {
            // Center is at (1,1), so neighbor at offset (dr, dc) is at (1+dr, 1+dc)
            if (row == 1 + NeighborOffsets[i].row && col == 1 + NeighborOffsets[i].col)
                return i;
        }
        return -1;
    }

    private void EmitBitmaskChanged()
    {
        if (_overlayTile == null) return;

        var format = _overlayTile.AutoTileFormat?.ToLowerInvariant();
        var bitmask = ComputeCenterBitmask(format);
        var description = GetBitmaskDescription(bitmask, format);
        EmitSignal(SignalName.BitmaskChanged, bitmask, description);
    }

    public override void _Draw()
    {
        if (_overlayTile == null)
            return;

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

        var overlayTexture = _service.GetTileTexture(_overlayTile);
        Texture2D? baseTexture = null;
        if (_baseTile != null)
        {
            baseTexture = _service.GetTileTexture(_baseTile);
        }

        var format = _overlayTile.AutoTileFormat?.ToLowerInvariant();
        var blobMasks = format == "blob47" ? NeighborBitmask8.GetValid47Masks() : null;

        // Draw 3x3 grid
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var destRect = new Rect2(col * scaledTileSize.X, row * scaledTileSize.Y, scaledTileSize.X, scaledTileSize.Y);
                var isCenter = row == 1 && col == 1;
                var hasOverlay = isCenter || IsNeighborToggled(row, col);

                // Draw base tile first (always, for background)
                if (baseTexture != null && _baseTile != null)
                {
                    var baseCoords = new Vector2I(_baseTile.AtlasX, _baseTile.AtlasY);
                    var baseSrcRect = new Rect2(baseCoords.X * _tileSize.X, baseCoords.Y * _tileSize.Y, _tileSize.X, _tileSize.Y);
                    DrawTextureRectRegion(baseTexture, destRect, baseSrcRect);
                }

                // Draw overlay tile with appropriate variant
                if (hasOverlay && overlayTexture != null)
                {
                    var bitmask = ComputeBitmaskForCell(row, col, format);
                    var variantCoords = GetVariantCoords(bitmask, format, blobMasks);
                    var srcRect = new Rect2(variantCoords.X * _tileSize.X, variantCoords.Y * _tileSize.Y, _tileSize.X, _tileSize.Y);
                    DrawTextureRectRegion(overlayTexture, destRect, srcRect);
                }

                // Draw toggle indicator for non-center cells
                if (!isCenter)
                {
                    var indicatorColor = hasOverlay
                        ? new Color(0.2f, 0.8f, 0.2f, 0.3f)  // Green tint for toggled on
                        : new Color(0.8f, 0.2f, 0.2f, 0.15f); // Red tint for toggled off
                    DrawRect(destRect, indicatorColor);
                }
            }
        }

        // Draw grid lines
        var gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        for (var col = 0; col <= 3; col++)
        {
            var x = col * scaledTileSize.X;
            DrawLine(new Vector2(x, 0), new Vector2(x, 3 * scaledTileSize.Y), gridColor, 1.0f);
        }
        for (var row = 0; row <= 3; row++)
        {
            var y = row * scaledTileSize.Y;
            DrawLine(new Vector2(0, y), new Vector2(3 * scaledTileSize.X, y), gridColor, 1.0f);
        }

        // Highlight center cell
        var centerRect = new Rect2(scaledTileSize.X, scaledTileSize.Y, scaledTileSize.X, scaledTileSize.Y);
        DrawRect(centerRect, new Color(1.0f, 1.0f, 0.0f, 0.2f)); // Yellow highlight for center
    }

    private bool IsNeighborToggled(int row, int col)
    {
        var idx = GetNeighborIndex(row, col);
        return idx >= 0 && _neighborToggles[idx];
    }

    private int ComputeBitmaskForCell(int row, int col, string? format)
    {
        // Build a 3x3 pattern from toggle state
        var pattern = BuildPatternFromToggles();
        return format switch
        {
            "blob47" => ComputeBlob47Bitmask(pattern, row, col),
            "edge16" => ComputeEdge16Bitmask(pattern, row, col),
            _ => ComputeCorner16Bitmask(pattern, row, col)
        };
    }

    private int ComputeCenterBitmask(string? format)
    {
        return ComputeBitmaskForCell(1, 1, format);
    }

    private int[,] BuildPatternFromToggles()
    {
        var pattern = new int[3, 3];
        pattern[1, 1] = 1; // Center always overlay

        for (var i = 0; i < 8; i++)
        {
            var (dr, dc) = NeighborOffsets[i];
            pattern[1 + dr, 1 + dc] = _neighborToggles[i] ? 1 : 0;
        }

        return pattern;
    }

    private int ComputeBlob47Bitmask(int[,] pattern, int row, int col)
    {
        // 8-bit blob: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128
        int mask = 0;

        bool hasN = row > 0 && pattern[row - 1, col] == 1;
        bool hasE = col < 2 && pattern[row, col + 1] == 1;
        bool hasS = row < 2 && pattern[row + 1, col] == 1;
        bool hasW = col > 0 && pattern[row, col - 1] == 1;
        bool hasNE = row > 0 && col < 2 && pattern[row - 1, col + 1] == 1;
        bool hasSE = row < 2 && col < 2 && pattern[row + 1, col + 1] == 1;
        bool hasSW = row < 2 && col > 0 && pattern[row + 1, col - 1] == 1;
        bool hasNW = row > 0 && col > 0 && pattern[row - 1, col - 1] == 1;

        if (hasN) mask |= 1;
        if (hasE) mask |= 4;
        if (hasS) mask |= 16;
        if (hasW) mask |= 64;

        // Corners only count if both adjacent edges are present
        if (hasNE && hasN && hasE) mask |= 2;
        if (hasSE && hasS && hasE) mask |= 8;
        if (hasSW && hasS && hasW) mask |= 32;
        if (hasNW && hasN && hasW) mask |= 128;

        return mask;
    }

    private int ComputeCorner16Bitmask(int[,] pattern, int row, int col)
    {
        // 4-bit corner: NE=1, SE=2, SW=4, NW=8
        int mask = 0;

        bool hasNE = row > 0 && col < 2 && pattern[row - 1, col + 1] == 1;
        bool hasSE = row < 2 && col < 2 && pattern[row + 1, col + 1] == 1;
        bool hasSW = row < 2 && col > 0 && pattern[row + 1, col - 1] == 1;
        bool hasNW = row > 0 && col > 0 && pattern[row - 1, col - 1] == 1;

        if (hasNE) mask |= 1;
        if (hasSE) mask |= 2;
        if (hasSW) mask |= 4;
        if (hasNW) mask |= 8;

        return mask;
    }

    private int ComputeEdge16Bitmask(int[,] pattern, int row, int col)
    {
        // 4-bit edge: N=1, E=2, S=4, W=8
        int mask = 0;

        bool hasN = row > 0 && pattern[row - 1, col] == 1;
        bool hasE = col < 2 && pattern[row, col + 1] == 1;
        bool hasS = row < 2 && pattern[row + 1, col] == 1;
        bool hasW = col > 0 && pattern[row, col - 1] == 1;

        if (hasN) mask |= 1;
        if (hasE) mask |= 2;
        if (hasS) mask |= 4;
        if (hasW) mask |= 8;

        return mask;
    }

    private string GetBitmaskDescription(int bitmask, string? format)
    {
        return format switch
        {
            "blob47" => $"Bitmask: {bitmask} (index {NeighborBitmask8.GetBlobIndex(bitmask)}/46)",
            "edge16" => $"Bitmask: {bitmask} ({NeighborBitmask.GetDescription(bitmask)})",
            _ => $"Bitmask: {bitmask} ({NeighborBitmaskCorner.GetDescription(bitmask)})"
        };
    }

    private Vector2I GetVariantCoords(int bitmask, string? format, IReadOnlyList<int>? blobMasks)
    {
        if (_overlayTile?.AutoTileVariants == null)
        {
            return new Vector2I(_overlayTile?.AtlasX ?? 0, _overlayTile?.AtlasY ?? 0);
        }

        int variantIndex;
        if (format == "blob47" && blobMasks != null)
        {
            variantIndex = blobMasks.IndexOf(bitmask);
            if (variantIndex < 0) variantIndex = 0;
        }
        else
        {
            // Both corner16 and edge16 use bitmask directly as index (0-15)
            variantIndex = bitmask;
        }

        if (variantIndex >= 0 && variantIndex < _overlayTile.AutoTileVariants.Length &&
            _overlayTile.AutoTileVariants[variantIndex].HasValue)
        {
            return _overlayTile.AutoTileVariants[variantIndex]!.Value;
        }

        return new Vector2I(_overlayTile.AtlasX, _overlayTile.AtlasY);
    }
}

/// <summary>
///     Extension method to find index in IReadOnlyList
/// </summary>
internal static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item) where T : IEquatable<T>
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Equals(item))
                return i;
        }
        return -1;
    }
}
#endif
