#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Reusable collapsible foldout container for editor UI.
/// Shows a header button that expands/collapses content.
/// </summary>
[Tool]
public partial class FoldoutContainer : VBoxContainer
{
    [Signal]
    public delegate void ToggledEventHandler(bool isExpanded);

    private string _title = "Foldout";
    private bool _isExpanded = true;
    private bool _showBorder = true;
    private Button? _headerButton;
    private VBoxContainer? _contentContainer;
    private PanelContainer? _borderPanel;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            UpdateHeader();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            UpdateVisibility();
            EmitSignal(SignalName.Toggled, _isExpanded);
        }
    }

    public bool ShowBorder
    {
        get => _showBorder;
        set
        {
            _showBorder = value;
            if (_borderPanel != null)
            {
                // Toggle between PanelContainer styling and plain container
                _borderPanel.AddThemeStyleboxOverride("panel", value ? CreateBorderStylebox() : null);
            }
        }
    }

    /// <summary>
    /// The container where child content should be added.
    /// Access this to add controls inside the foldout.
    /// </summary>
    public VBoxContainer Content => _contentContainer!;

    public FoldoutContainer() { }

    public FoldoutContainer(string title, bool startExpanded = true)
    {
        _title = title;
        _isExpanded = startExpanded;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Header button (expand/collapse)
        _headerButton = new Button
        {
            Alignment = HorizontalAlignment.Left,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _headerButton.AddThemeFontSizeOverride("font_size", 13);
        _headerButton.Pressed += ToggleExpanded;
        AddChild(_headerButton);

        // Border panel for visual grouping
        _borderPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        if (_showBorder)
            _borderPanel.AddThemeStyleboxOverride("panel", CreateBorderStylebox());
        AddChild(_borderPanel);

        // Content container inside the border
        _contentContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        // Add padding via margins
        _contentContainer.AddThemeConstantOverride("separation", 6);
        _borderPanel.AddChild(_contentContainer);

        // Add a margin container for padding
        var marginContainer = new MarginContainer();
        marginContainer.AddThemeConstantOverride("margin_left", 8);
        marginContainer.AddThemeConstantOverride("margin_right", 8);
        marginContainer.AddThemeConstantOverride("margin_top", 4);
        marginContainer.AddThemeConstantOverride("margin_bottom", 8);

        // Reparent content to margin
        _borderPanel.RemoveChild(_contentContainer);
        marginContainer.AddChild(_contentContainer);
        _borderPanel.AddChild(marginContainer);

        UpdateHeader();
        UpdateVisibility();
    }

    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    private void UpdateHeader()
    {
        if (_headerButton == null) return;
        var prefix = _isExpanded ? "▼" : "▶";
        _headerButton.Text = $"{prefix} {_title}";
    }

    private void UpdateVisibility()
    {
        if (_borderPanel != null)
            _borderPanel.Visible = _isExpanded;
        UpdateHeader();
    }

    private static StyleBoxFlat CreateBorderStylebox()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.18f, 0.5f),
            BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.8f),
            BorderWidthLeft = 1,
            BorderWidthTop = 0,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
    }

    /// <summary>
    /// Adds a horizontal row with a label and control.
    /// Convenience method for common property layouts.
    /// </summary>
    public HBoxContainer AddRow(string label, Control control)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var labelNode = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(120, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddChild(labelNode);

        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);

        Content.AddChild(row);
        return row;
    }

    /// <summary>
    /// Adds a separator line to the content.
    /// </summary>
    public void AddSeparator()
    {
        Content.AddChild(new HSeparator());
    }
}
#endif
