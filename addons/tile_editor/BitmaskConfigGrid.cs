#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Visual grid component for configuring which bitmask values are allowed in an auto-tile format.
/// Displays toggleable cells with neighbor pattern visualization for each bitmask.
/// </summary>
[Tool]
public partial class BitmaskConfigGrid : VBoxContainer
{
    [Signal]
    public delegate void BitmaskToggledEventHandler(int bitmask, bool allowed);

    [Signal]
    public delegate void AllowedBitmasksChangedEventHandler();

    private BitmaskType _bitmaskType = BitmaskType.Corner4;
    private readonly HashSet<int> _allowedBitmasks = new();
    private GridContainer? _grid;
    private readonly Dictionary<int, CheckButton> _checkButtons = new();
    private readonly Dictionary<int, TileShapePreview> _previews = new();
    private bool _isUpdating;
    private bool _isReadOnly;

    // Labels for 4-bit corner format (NE=1, SE=2, SW=4, NW=8)
    private static readonly string[] CornerBitmaskLabels =
    {
        "None", "NE", "SE", "NE+SE", "SW", "NE+SW", "SE+SW", "NE+SE+SW",
        "NW", "NE+NW", "SE+NW", "NE+SE+NW", "SW+NW", "NE+SW+NW", "SE+SW+NW", "All"
    };

    // Labels for 4-bit edge format (N=1, E=2, S=4, W=8)
    private static readonly string[] EdgeBitmaskLabels =
    {
        "None", "N", "E", "N+E", "S", "N+S", "E+S", "N+E+S",
        "W", "N+W", "E+W", "N+E+W", "S+W", "N+S+W", "E+S+W", "All"
    };

    // Required by Godot for [Tool] classes
    public BitmaskConfigGrid() { }

    /// <summary>
    /// Gets or sets whether the grid is read-only (for built-in formats).
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            _isReadOnly = value;
            foreach (var btn in _checkButtons.Values)
            {
                btn.Disabled = value;
            }
        }
    }

    /// <summary>
    /// Gets the current set of allowed bitmasks.
    /// </summary>
    public IReadOnlySet<int> AllowedBitmasks => _allowedBitmasks;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    /// <summary>
    /// Configures the grid for a specific bitmask type and initial allowed values.
    /// </summary>
    public void Configure(BitmaskType bitmaskType, IEnumerable<int> allowedBitmasks, bool readOnly = false)
    {
        _bitmaskType = bitmaskType;
        _isReadOnly = readOnly;
        _allowedBitmasks.Clear();
        _allowedBitmasks.UnionWith(allowedBitmasks);

        RebuildGrid();
    }

    /// <summary>
    /// Updates the allowed bitmasks without rebuilding the grid.
    /// </summary>
    public void SetAllowedBitmasks(IEnumerable<int> allowedBitmasks)
    {
        _isUpdating = true;
        _allowedBitmasks.Clear();
        _allowedBitmasks.UnionWith(allowedBitmasks);

        foreach (var (bitmask, checkButton) in _checkButtons)
        {
            checkButton.ButtonPressed = _allowedBitmasks.Contains(bitmask);
        }
        _isUpdating = false;
    }

    private void RebuildGrid()
    {
        // Clear existing content
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }
        _checkButtons.Clear();
        _previews.Clear();

        // Get valid bitmasks for this type
        var validBitmasks = GetValidBitmasksForType(_bitmaskType);

        // For Full8 (blob47), group by edge count to reduce overwhelm
        if (_bitmaskType == BitmaskType.Full8)
        {
            BuildGroupedGrid(validBitmasks);
        }
        else
        {
            BuildSimpleGrid(validBitmasks);
        }
    }

    private void BuildSimpleGrid(int[] validBitmasks)
    {
        _grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        AddChild(_grid);

        foreach (var bitmask in validBitmasks)
        {
            var cell = CreateBitmaskCell(bitmask);
            _grid.AddChild(cell);
        }
    }

    private void BuildGroupedGrid(int[] validBitmasks)
    {
        // Group by edge count (number of set bits in cardinal directions)
        var groups = validBitmasks
            .GroupBy(GetEdgeCount)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var group in groups)
        {
            // Section header
            var header = new Label
            {
                Text = $"{group.Key} Edge{(group.Key != 1 ? "s" : "")} ({group.Count()} patterns)"
            };
            header.AddThemeFontSizeOverride("font_size", 12);
            AddChild(header);

            // Grid for this group
            var grid = new GridContainer
            {
                Columns = 6,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);

            foreach (var bitmask in group.OrderBy(m => m))
            {
                var cell = CreateBitmaskCell(bitmask);
                grid.AddChild(cell);
            }

            AddChild(grid);
            AddChild(new HSeparator());
        }
    }

    private VBoxContainer CreateBitmaskCell(int bitmask)
    {
        var cell = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(60, 70)
        };

        // Shape preview
        var preview = new TileShapePreview
        {
            CustomMinimumSize = new Vector2(30, 30),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        preview.SetMask(bitmask, BitmaskTypeToPreviewFormat(_bitmaskType));
        cell.AddChild(preview);
        _previews[bitmask] = preview;

        // Checkbox with label
        var checkButton = new CheckButton
        {
            Text = GetBitmaskLabel(bitmask),
            ButtonPressed = _allowedBitmasks.Contains(bitmask),
            Disabled = _isReadOnly,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        checkButton.AddThemeFontSizeOverride("font_size", 9);

        var capturedBitmask = bitmask;
        checkButton.Toggled += (pressed) => OnBitmaskToggled(capturedBitmask, pressed);

        cell.AddChild(checkButton);
        _checkButtons[bitmask] = checkButton;

        return cell;
    }

    private void OnBitmaskToggled(int bitmask, bool pressed)
    {
        if (_isUpdating) return;

        if (pressed)
            _allowedBitmasks.Add(bitmask);
        else
            _allowedBitmasks.Remove(bitmask);

        EmitSignal(SignalName.BitmaskToggled, bitmask, pressed);
        EmitSignal(SignalName.AllowedBitmasksChanged);
    }

    private string GetBitmaskLabel(int bitmask)
    {
        return _bitmaskType switch
        {
            BitmaskType.Corner4 when bitmask < 16 => $"{bitmask}",
            BitmaskType.Edge4 when bitmask < 16 => $"{bitmask}",
            BitmaskType.Full8 => $"{bitmask}",
            _ => bitmask.ToString()
        };
    }

    private static TileShapePreview.Format BitmaskTypeToPreviewFormat(BitmaskType type)
    {
        return type switch
        {
            BitmaskType.Corner4 => TileShapePreview.Format.Corner16,
            BitmaskType.Edge4 => TileShapePreview.Format.Edge16,
            BitmaskType.Full8 => TileShapePreview.Format.Blob47,
            _ => TileShapePreview.Format.Corner16
        };
    }

    private static int[] GetValidBitmasksForType(BitmaskType type)
    {
        return type switch
        {
            BitmaskType.Corner4 => Enumerable.Range(0, 16).ToArray(),
            BitmaskType.Edge4 => Enumerable.Range(0, 16).ToArray(),
            BitmaskType.Full8 => GetValidBlobBitmasks(),
            _ => Enumerable.Range(0, 16).ToArray()
        };
    }

    /// <summary>
    /// Gets the 47 valid 8-bit blob bitmasks (where corners are only valid if adjacent edges are set).
    /// </summary>
    private static int[] GetValidBlobBitmasks()
    {
        var valid = new List<int>();

        for (var mask = 0; mask < 256; mask++)
        {
            var isValid = true;

            // NE corner (2) requires N (1) and E (4)
            if ((mask & 2) != 0 && ((mask & 1) == 0 || (mask & 4) == 0))
                isValid = false;
            // SE corner (8) requires E (4) and S (16)
            if ((mask & 8) != 0 && ((mask & 4) == 0 || (mask & 16) == 0))
                isValid = false;
            // SW corner (32) requires S (16) and W (64)
            if ((mask & 32) != 0 && ((mask & 16) == 0 || (mask & 64) == 0))
                isValid = false;
            // NW corner (128) requires W (64) and N (1)
            if ((mask & 128) != 0 && ((mask & 64) == 0 || (mask & 1) == 0))
                isValid = false;

            if (isValid)
                valid.Add(mask);
        }

        return valid.ToArray();
    }

    /// <summary>
    /// Gets the number of cardinal edges set in a blob bitmask.
    /// N=1, E=4, S=16, W=64
    /// </summary>
    private static int GetEdgeCount(int bitmask)
    {
        var count = 0;
        if ((bitmask & 1) != 0) count++;   // N
        if ((bitmask & 4) != 0) count++;   // E
        if ((bitmask & 16) != 0) count++;  // S
        if ((bitmask & 64) != 0) count++;  // W
        return count;
    }

    /// <summary>
    /// Selects all bitmasks.
    /// </summary>
    public void SelectAll()
    {
        if (_isReadOnly) return;

        _isUpdating = true;
        var validBitmasks = GetValidBitmasksForType(_bitmaskType);
        _allowedBitmasks.Clear();
        _allowedBitmasks.UnionWith(validBitmasks);

        foreach (var (bitmask, checkButton) in _checkButtons)
        {
            checkButton.ButtonPressed = true;
        }
        _isUpdating = false;

        EmitSignal(SignalName.AllowedBitmasksChanged);
    }

    /// <summary>
    /// Deselects all bitmasks.
    /// </summary>
    public void DeselectAll()
    {
        if (_isReadOnly) return;

        _isUpdating = true;
        _allowedBitmasks.Clear();

        foreach (var checkButton in _checkButtons.Values)
        {
            checkButton.ButtonPressed = false;
        }
        _isUpdating = false;

        EmitSignal(SignalName.AllowedBitmasksChanged);
    }
}
#endif
