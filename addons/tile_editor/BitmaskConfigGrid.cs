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
        var validBitmasks = BitmaskTypeCatalog.GetValidBitmasks(_bitmaskType);

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
            .GroupBy(BitmaskTypeCatalog.GetEdgeCount)
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
        preview.SetMask(bitmask, BitmaskTypeCatalog.GetPreviewFormat(_bitmaskType));
        cell.AddChild(preview);
        _previews[bitmask] = preview;

        // Checkbox with label
        var checkButton = new CheckButton
        {
            Text = bitmask.ToString(),
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

    /// <summary>
    /// Selects all bitmasks.
    /// </summary>
    public void SelectAll()
    {
        if (_isReadOnly) return;

        _isUpdating = true;
        var validBitmasks = BitmaskTypeCatalog.GetValidBitmasks(_bitmaskType);
        _allowedBitmasks.Clear();
        _allowedBitmasks.UnionWith(validBitmasks);

        foreach (var checkButton in _checkButtons.Values)
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
