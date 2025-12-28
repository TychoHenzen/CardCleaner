#if TOOLS
using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
///     Panel showing all valid auto-tile permutations for a selected tile rendered over a base tile.
/// </summary>
[Tool]
public partial class AutoTilePreviewPanel : ScrollContainer
{
    private readonly TileEditorService _service;
    private OptionButton? _tileSelector;
    private OptionButton? _baseTileSelector;
    private GridContainer? _permutationGrid;
    private Label? _infoLabel;
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
        tileRow.AddChild(new Label { Text = "Tile with variants:", CustomMinimumSize = new Vector2(120, 0) });
        _tileSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tileSelector.ItemSelected += OnTileSelected;
        tileRow.AddChild(_tileSelector);
        mainVBox.AddChild(tileRow);

        // Base tile selector row
        var baseRow = new HBoxContainer();
        baseRow.AddChild(new Label { Text = "Base tile:", CustomMinimumSize = new Vector2(120, 0) });
        _baseTileSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _baseTileSelector.ItemSelected += OnBaseTileSelected;
        baseRow.AddChild(_baseTileSelector);
        mainVBox.AddChild(baseRow);

        mainVBox.AddChild(new HSeparator());

        // Info label
        _infoLabel = new Label
        {
            Text = "Select a tile with auto-tile variants to preview.",
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        mainVBox.AddChild(_infoLabel);

        // Grid for permutations
        var gridScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto
        };

        _permutationGrid = new GridContainer
        {
            Columns = 8,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _permutationGrid.AddThemeConstantOverride("h_separation", 8);
        _permutationGrid.AddThemeConstantOverride("v_separation", 8);
        gridScroll.AddChild(_permutationGrid);
        mainVBox.AddChild(gridScroll);

        // Populate dropdowns after service loads
        CallDeferred(MethodName.PopulateDropdowns);
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
            // Show terrain tiles as base options
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

    private void ClearPreview()
    {
        if (_permutationGrid == null) return;

        foreach (var child in _permutationGrid.GetChildren())
        {
            child.QueueFree();
        }

        _infoLabel!.Text = "Select a tile with auto-tile variants to preview.";
    }

    private void RefreshPreview()
    {
        if (_permutationGrid == null || string.IsNullOrEmpty(_selectedTileId)) return;

        // Clear existing
        foreach (var child in _permutationGrid.GetChildren())
        {
            child.QueueFree();
        }

        var tile = _service.GetTile(_selectedTileId);
        if (tile == null || !tile.HasAutoTileVariants)
        {
            _infoLabel!.Text = "Selected tile has no auto-tile variants.";
            return;
        }

        // Determine format and get valid masks
        var isBlob47 = tile.AutoTileFormat == "blob47";
        var variantCount = isBlob47 ? 47 : 16;
        IReadOnlyList<int>? blobMasks = isBlob47 ? NeighborBitmask8.GetValid47Masks() : null;

        _infoLabel!.Text = $"Showing {variantCount} permutations ({(isBlob47 ? "8-bit Blob" : "4-bit Corner")} format)";
        _permutationGrid.Columns = isBlob47 ? 8 : 4;

        // Get textures
        var tileTexture = _service.GetTileTexture(tile);
        EditableTile? baseTile = null;
        Texture2D? baseTexture = null;
        if (!string.IsNullOrEmpty(_selectedBaseTileId))
        {
            baseTile = _service.GetTile(_selectedBaseTileId);
            if (baseTile != null)
                baseTexture = _service.GetTileTexture(baseTile);
        }

        var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);

        for (var i = 0; i < variantCount; i++)
        {
            var container = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(isBlob47 ? 48 : 64, isBlob47 ? 72 : 88),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };

            // Label
            int maskValue = blobMasks != null ? blobMasks[i] : i;
            string labelText;
            if (isBlob47)
            {
                labelText = $"{i}: {maskValue}";
            }
            else
            {
                labelText = $"{i}: {GetCornerLabel(i)}";
            }

            var label = new Label
            {
                Text = labelText,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 8);
            container.AddChild(label);

            // Shape preview
            var shapePreview = new TileShapePreview
            {
                CustomMinimumSize = new Vector2(24, 24),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            shapePreview.SetMask(maskValue, isBlob47 ? TileShapePreview.Format.Blob47 : TileShapePreview.Format.Corner16);
            container.AddChild(shapePreview);

            // Tile preview (base + variant layered)
            var previewContainer = new Control
            {
                CustomMinimumSize = new Vector2(32, 32),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };

            // Base tile layer
            if (baseTexture != null && baseTile != null)
            {
                var baseRect = new TextureRect
                {
                    CustomMinimumSize = new Vector2(32, 32),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                };
                var baseCoords = new Vector2I(baseTile.AtlasX, baseTile.AtlasY);
                var baseRegion = new Rect2I(baseCoords * tileSize, tileSize);
                baseRect.Texture = new AtlasTexture { Atlas = baseTexture, Region = baseRegion };
                previewContainer.AddChild(baseRect);
            }

            // Variant tile layer
            if (tileTexture != null && tile.AutoTileVariants != null && i < tile.AutoTileVariants.Length)
            {
                var variantRect = new TextureRect
                {
                    CustomMinimumSize = new Vector2(32, 32),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                };

                Vector2I variantCoords;
                if (tile.AutoTileVariants[i].HasValue)
                {
                    variantCoords = tile.AutoTileVariants[i]!.Value;
                }
                else
                {
                    variantCoords = new Vector2I(tile.AtlasX, tile.AtlasY);
                }

                var variantRegion = new Rect2I(variantCoords * tileSize, tileSize);
                variantRect.Texture = new AtlasTexture { Atlas = tileTexture, Region = variantRegion };
                previewContainer.AddChild(variantRect);
            }

            container.AddChild(previewContainer);
            _permutationGrid.AddChild(container);
        }
    }

    private static string GetCornerLabel(int mask)
    {
        return mask switch
        {
            0 => "None",
            1 => "NE",
            2 => "SE",
            3 => "NE+SE",
            4 => "SW",
            5 => "NE+SW",
            6 => "SE+SW",
            7 => "NE+SE+SW",
            8 => "NW",
            9 => "NE+NW",
            10 => "SE+NW",
            11 => "NE+SE+NW",
            12 => "SW+NW",
            13 => "NE+SW+NW",
            14 => "SE+SW+NW",
            15 => "All",
            _ => $"({mask})"
        };
    }

    /// <summary>
    ///     Refresh dropdowns when tiles are reloaded.
    /// </summary>
    public void Refresh()
    {
        PopulateDropdowns();
        if (!string.IsNullOrEmpty(_selectedTileId))
        {
            RefreshPreview();
        }
    }
}
#endif
