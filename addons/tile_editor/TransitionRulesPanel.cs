#if TOOLS
using System;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for editing transition rules between terrain groups.
/// Shows rule list on left, edge tile editor on right.
/// Bitmask bits: N=1, E=2, S=4, W=8 (16 combinations total)
/// </summary>
[Tool]
public partial class TransitionRulesPanel : HSplitContainer
{
    private readonly TileEditorService _service;
    private ItemList? _ruleList;
    private GridContainer? _edgeTileGrid;
    private OptionButton? _fromGroupOption;
    private OptionButton? _toGroupOption;
    private Button? _addRuleButton;
    private Button? _removeRuleButton;
    private int _selectedRuleIndex = -1;
    private bool _isUpdating;

    // Edge tile buttons for each bitmask value (0-15)
    private readonly Button[] _edgeTileButtons = new Button[16];

    // Bitmask direction names for display
    private static readonly string[] BitmaskNames =
    [
        "None (0)",      // 0000
        "N (1)",         // 0001
        "E (2)",         // 0010
        "NE (3)",        // 0011
        "S (4)",         // 0100
        "NS (5)",        // 0101
        "ES (6)",        // 0110
        "NES (7)",       // 0111
        "W (8)",         // 1000
        "NW (9)",        // 1001
        "EW (10)",       // 1010
        "NEW (11)",      // 1011
        "SW (12)",       // 1100
        "NSW (13)",      // 1101
        "ESW (14)",      // 1110
        "NESW (15)"      // 1111
    ];

    [Signal]
    public delegate void TilePickerRequestedEventHandler(int ruleIndex, int bitmask);

    public TransitionRulesPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.TransitionRuleModified += OnRuleModified;
        _service.TransitionRuleAdded += OnRuleAdded;
        _service.TransitionRuleRemoved += OnRuleRemoved;
        _service.TerrainGroupAdded += _ => RefreshGroupOptions();
        _service.TerrainGroupRemoved += _ => RefreshGroupOptions();

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        SetupUI();
    }

    private void SetupUI()
    {
        // Left panel: Rule list
        var leftPanel = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(250, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(leftPanel);

        var ruleLabel = new Label { Text = "Transition Rules" };
        leftPanel.AddChild(ruleLabel);

        _ruleList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _ruleList.ItemSelected += OnRuleSelected;
        leftPanel.AddChild(_ruleList);

        // Add rule controls
        var addRuleContainer = new VBoxContainer();
        leftPanel.AddChild(addRuleContainer);

        var fromRow = new HBoxContainer();
        addRuleContainer.AddChild(fromRow);
        fromRow.AddChild(new Label { Text = "From:", CustomMinimumSize = new Vector2(40, 0) });
        _fromGroupOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fromRow.AddChild(_fromGroupOption);

        var toRow = new HBoxContainer();
        addRuleContainer.AddChild(toRow);
        toRow.AddChild(new Label { Text = "To:", CustomMinimumSize = new Vector2(40, 0) });
        _toGroupOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toRow.AddChild(_toGroupOption);

        var ruleButtons = new HBoxContainer();
        addRuleContainer.AddChild(ruleButtons);

        _addRuleButton = new Button { Text = "Add Rule" };
        _addRuleButton.Pressed += OnAddRulePressed;
        ruleButtons.AddChild(_addRuleButton);

        _removeRuleButton = new Button { Text = "Remove", Disabled = true };
        _removeRuleButton.Pressed += OnRemoveRulePressed;
        ruleButtons.AddChild(_removeRuleButton);

        // Right panel: Edge tile editor
        var rightPanel = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(rightPanel);

        var edgeLabel = new Label { Text = "Edge Tiles (click to assign)" };
        rightPanel.AddChild(edgeLabel);

        var scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rightPanel.AddChild(scrollContainer);

        _edgeTileGrid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        scrollContainer.AddChild(_edgeTileGrid);

        // Create 16 buttons for each bitmask value
        for (int i = 0; i < 16; i++)
        {
            var container = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(80, 80)
            };
            _edgeTileGrid.AddChild(container);

            var label = new Label
            {
                Text = BitmaskNames[i],
                HorizontalAlignment = HorizontalAlignment.Center
            };
            container.AddChild(label);

            var button = new Button
            {
                Text = "[None]",
                CustomMinimumSize = new Vector2(75, 50),
                Disabled = true
            };
            var bitmask = i; // Capture for closure
            button.Pressed += () => OnEdgeTileButtonPressed(bitmask);
            container.AddChild(button);

            _edgeTileButtons[i] = button;
        }
    }

    private void RefreshDisplay()
    {
        _isUpdating = true;
        try
        {
            RefreshGroupOptions();
            RefreshRuleList();
            _selectedRuleIndex = -1;
            UpdateEdgeTileDisplay();
            UpdateButtonStates();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void RefreshGroupOptions()
    {
        _fromGroupOption!.Clear();
        _toGroupOption!.Clear();

        foreach (var group in _service.AllTerrainGroups.OrderBy(g => g.Name))
        {
            _fromGroupOption.AddItem(group.Name);
            _fromGroupOption.SetItemMetadata(_fromGroupOption.ItemCount - 1, group.Id);

            _toGroupOption.AddItem(group.Name);
            _toGroupOption.SetItemMetadata(_toGroupOption.ItemCount - 1, group.Id);
        }
    }

    private void RefreshRuleList()
    {
        _ruleList!.Clear();

        var rules = _service.AllTransitionRules;
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var fromGroup = _service.GetTerrainGroup(rule.FromGroupId);
            var toGroup = _service.GetTerrainGroup(rule.ToGroupId);

            var fromName = fromGroup?.Name ?? rule.FromGroupId;
            var toName = toGroup?.Name ?? rule.ToGroupId;
            var edgeCount = rule.EdgeTiles.Count(kvp => !string.IsNullOrEmpty(kvp.Value));

            _ruleList.AddItem($"{fromName} → {toName} ({edgeCount}/16)");
            _ruleList.SetItemMetadata(_ruleList.ItemCount - 1, i);
        }
    }

    private void OnRuleSelected(long index)
    {
        if (_isUpdating) return;

        _selectedRuleIndex = _ruleList!.GetItemMetadata((int)index).AsInt32();
        UpdateEdgeTileDisplay();
        UpdateButtonStates();
    }

    private void UpdateEdgeTileDisplay()
    {
        if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _service.TransitionRuleCount)
        {
            // Disable all buttons and show [None]
            for (int i = 0; i < 16; i++)
            {
                _edgeTileButtons[i].Text = "[None]";
                _edgeTileButtons[i].Disabled = true;
            }
            return;
        }

        var rule = _service.GetTransitionRule(_selectedRuleIndex);
        if (rule == null) return;

        for (int i = 0; i < 16; i++)
        {
            var tileId = rule.GetEdgeTile(i);
            if (string.IsNullOrEmpty(tileId))
            {
                _edgeTileButtons[i].Text = "[None]";
            }
            else
            {
                var tile = _service.GetTile(tileId);
                _edgeTileButtons[i].Text = tile?.Name ?? tileId;
            }
            _edgeTileButtons[i].Disabled = false;
        }
    }

    private void UpdateButtonStates()
    {
        _removeRuleButton!.Disabled = _selectedRuleIndex < 0;
        _addRuleButton!.Disabled = _fromGroupOption!.ItemCount == 0 || _toGroupOption!.ItemCount == 0;
    }

    private void OnAddRulePressed()
    {
        if (_fromGroupOption!.Selected < 0 || _toGroupOption!.Selected < 0) return;

        var fromGroupId = _fromGroupOption.GetItemMetadata(_fromGroupOption.Selected).AsString();
        var toGroupId = _toGroupOption.GetItemMetadata(_toGroupOption.Selected).AsString();

        // Check if rule already exists
        var existing = _service.AllTransitionRules.FirstOrDefault(r =>
            r.FromGroupId == fromGroupId && r.ToGroupId == toGroupId);

        if (existing != null)
        {
            GD.PrintErr($"[TransitionRulesPanel] Rule {fromGroupId} -> {toGroupId} already exists");
            return;
        }

        var newRule = new EditableTransitionRule
        {
            FromGroupId = fromGroupId,
            ToGroupId = toGroupId,
            EdgeTiles = new System.Collections.Generic.Dictionary<int, string?>()
        };

        var index = _service.AddTransitionRule(newRule);
        RefreshRuleList();

        // Select the new rule
        for (int i = 0; i < _ruleList!.ItemCount; i++)
        {
            if (_ruleList.GetItemMetadata(i).AsInt32() == index)
            {
                _ruleList.Select(i);
                OnRuleSelected(i);
                break;
            }
        }
    }

    private void OnRemoveRulePressed()
    {
        if (_selectedRuleIndex < 0) return;

        var rule = _service.GetTransitionRule(_selectedRuleIndex);
        if (rule == null) return;

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Delete transition rule '{rule.FromGroupId} → {rule.ToGroupId}'?"
        };

        dialog.Confirmed += () =>
        {
            _service.RemoveTransitionRule(_selectedRuleIndex);
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnEdgeTileButtonPressed(int bitmask)
    {
        if (_selectedRuleIndex < 0) return;
        EmitSignal(SignalName.TilePickerRequested, _selectedRuleIndex, bitmask);
    }

    public void SetEdgeTile(int ruleIndex, int bitmask, string? tileId)
    {
        var rule = _service.GetTransitionRule(ruleIndex);
        if (rule == null) return;

        rule.SetEdgeTile(bitmask, tileId);
        _service.UpdateTransitionRule(ruleIndex, rule);
        UpdateEdgeTileDisplay();
        RefreshRuleList();
    }

    private void OnRuleModified(int ruleIndex)
    {
        RefreshRuleList();
        if (_selectedRuleIndex == ruleIndex)
        {
            UpdateEdgeTileDisplay();
        }
    }

    private void OnRuleAdded(int ruleIndex)
    {
        RefreshRuleList();
    }

    private void OnRuleRemoved(int ruleIndex)
    {
        if (_selectedRuleIndex == ruleIndex)
        {
            _selectedRuleIndex = -1;
        }
        else if (_selectedRuleIndex > ruleIndex)
        {
            _selectedRuleIndex--;
        }
        RefreshRuleList();
        UpdateEdgeTileDisplay();
        UpdateButtonStates();
    }
}
#endif
