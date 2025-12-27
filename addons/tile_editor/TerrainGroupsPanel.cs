#if TOOLS
using System;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for editing terrain groups and their tile membership.
/// Shows group list on left, group members on right.
/// </summary>
[Tool]
public partial class TerrainGroupsPanel : HSplitContainer
{
    private readonly TileEditorService _service;
    private ItemList? _groupList;
    private ItemList? _memberList;
    private LineEdit? _groupNameEdit;
    private SpinBox? _prioritySpinBox;
    private Button? _addGroupButton;
    private Button? _removeGroupButton;
    private Button? _addTileButton;
    private Button? _removeTileButton;
    private EditableTerrainGroup? _selectedGroup;
    private bool _isUpdating;

    [Signal]
    public delegate void TilePickerRequestedEventHandler(string groupId);

    public TerrainGroupsPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.TerrainGroupModified += OnGroupModified;
        _service.TerrainGroupAdded += OnGroupAdded;
        _service.TerrainGroupRemoved += OnGroupRemoved;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        SetupUI();
    }

    private void SetupUI()
    {
        // Left panel: Group list
        var leftPanel = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(200, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(leftPanel);

        var groupLabel = new Label { Text = "Terrain Groups" };
        leftPanel.AddChild(groupLabel);

        _groupList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _groupList.ItemSelected += OnGroupSelected;
        leftPanel.AddChild(_groupList);

        var groupButtons = new HBoxContainer();
        leftPanel.AddChild(groupButtons);

        _addGroupButton = new Button { Text = "+", TooltipText = "Add terrain group" };
        _addGroupButton.Pressed += OnAddGroupPressed;
        groupButtons.AddChild(_addGroupButton);

        _removeGroupButton = new Button { Text = "-", TooltipText = "Remove terrain group", Disabled = true };
        _removeGroupButton.Pressed += OnRemoveGroupPressed;
        groupButtons.AddChild(_removeGroupButton);

        // Right panel: Group details and members
        var rightPanel = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(rightPanel);

        // Group properties
        var propsContainer = new VBoxContainer();
        rightPanel.AddChild(propsContainer);

        var nameRow = new HBoxContainer();
        propsContainer.AddChild(nameRow);
        nameRow.AddChild(new Label { Text = "Name:", CustomMinimumSize = new Vector2(60, 0) });
        _groupNameEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Editable = false
        };
        _groupNameEdit.TextChanged += OnGroupNameChanged;
        nameRow.AddChild(_groupNameEdit);

        var priorityRow = new HBoxContainer();
        propsContainer.AddChild(priorityRow);
        priorityRow.AddChild(new Label { Text = "Priority:", CustomMinimumSize = new Vector2(60, 0) });
        _prioritySpinBox = new SpinBox
        {
            MinValue = 0,
            MaxValue = 1000,
            Step = 1,
            Editable = false
        };
        _prioritySpinBox.ValueChanged += OnPriorityChanged;
        priorityRow.AddChild(_prioritySpinBox);

        rightPanel.AddChild(new HSeparator());

        // Member list
        var memberLabel = new Label { Text = "Tiles in Group" };
        rightPanel.AddChild(memberLabel);

        _memberList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        rightPanel.AddChild(_memberList);

        var memberButtons = new HBoxContainer();
        rightPanel.AddChild(memberButtons);

        _addTileButton = new Button { Text = "Add Tile...", Disabled = true };
        _addTileButton.Pressed += OnAddTilePressed;
        memberButtons.AddChild(_addTileButton);

        _removeTileButton = new Button { Text = "Remove Tile", Disabled = true };
        _removeTileButton.Pressed += OnRemoveTilePressed;
        memberButtons.AddChild(_removeTileButton);
    }

    private void RefreshDisplay()
    {
        _isUpdating = true;
        try
        {
            _groupList!.Clear();
            foreach (var group in _service.AllTerrainGroups.OrderByDescending(g => g.Priority))
            {
                _groupList.AddItem($"{group.Name} ({group.Members.Count})");
                _groupList.SetItemMetadata(_groupList.ItemCount - 1, group.Id);
            }

            _selectedGroup = null;
            UpdateMemberDisplay();
            UpdateButtonStates();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnGroupSelected(long index)
    {
        if (_isUpdating) return;

        var groupId = _groupList!.GetItemMetadata((int)index).AsString();
        _selectedGroup = _service.GetTerrainGroup(groupId);
        UpdateMemberDisplay();
        UpdateButtonStates();
    }

    private void UpdateMemberDisplay()
    {
        _isUpdating = true;
        try
        {
            _memberList!.Clear();

            if (_selectedGroup == null)
            {
                _groupNameEdit!.Text = "";
                _groupNameEdit.Editable = false;
                _prioritySpinBox!.Value = 0;
                _prioritySpinBox.Editable = false;
                return;
            }

            _groupNameEdit!.Text = _selectedGroup.Name;
            _groupNameEdit.Editable = true;
            _prioritySpinBox!.Value = _selectedGroup.Priority;
            _prioritySpinBox.Editable = true;

            foreach (var tileId in _selectedGroup.Members.OrderBy(t => t))
            {
                var tile = _service.GetTile(tileId);
                var displayName = tile != null ? $"{tile.Name} ({tileId})" : $"[Missing] {tileId}";
                _memberList.AddItem(displayName);
                _memberList.SetItemMetadata(_memberList.ItemCount - 1, tileId);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateButtonStates()
    {
        _removeGroupButton!.Disabled = _selectedGroup == null;
        _addTileButton!.Disabled = _selectedGroup == null;
        _removeTileButton!.Disabled = _selectedGroup == null || _memberList!.GetSelectedItems().Length == 0;

        _memberList!.ItemSelected += _ => UpdateButtonStates();
    }

    private void OnGroupNameChanged(string newName)
    {
        if (_isUpdating || _selectedGroup == null) return;

        _selectedGroup.Name = newName;
        _service.UpdateTerrainGroup(_selectedGroup);
    }

    private void OnPriorityChanged(double value)
    {
        if (_isUpdating || _selectedGroup == null) return;

        _selectedGroup.Priority = (int)value;
        _service.UpdateTerrainGroup(_selectedGroup);
    }

    private void OnAddGroupPressed()
    {
        // Create dialog to get group ID
        var dialog = new ConfirmationDialog
        {
            Title = "New Terrain Group",
            Size = new Vector2I(300, 120)
        };

        var vbox = new VBoxContainer();
        dialog.AddChild(vbox);

        vbox.AddChild(new Label { Text = "Group ID (snake_case):" });
        var idEdit = new LineEdit { PlaceholderText = "new_group" };
        vbox.AddChild(idEdit);

        dialog.Confirmed += () =>
        {
            var id = idEdit.Text.Trim();
            if (string.IsNullOrEmpty(id))
            {
                GD.PrintErr("[TerrainGroupsPanel] Group ID cannot be empty");
                dialog.QueueFree();
                return;
            }

            var newGroup = new EditableTerrainGroup
            {
                Id = id,
                Name = id.Replace("_", " ").ToUpper().Substring(0, 1) + id.Replace("_", " ").Substring(1),
                Priority = 50,
                Members = new System.Collections.Generic.List<string>()
            };

            if (_service.AddTerrainGroup(newGroup))
            {
                RefreshDisplay();
                // Select the new group
                for (int i = 0; i < _groupList!.ItemCount; i++)
                {
                    if (_groupList.GetItemMetadata(i).AsString() == id)
                    {
                        _groupList.Select(i);
                        OnGroupSelected(i);
                        break;
                    }
                }
            }

            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnRemoveGroupPressed()
    {
        if (_selectedGroup == null) return;

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Delete terrain group '{_selectedGroup.Name}'?\nThis will remove {_selectedGroup.Members.Count} tile assignments."
        };

        dialog.Confirmed += () =>
        {
            _service.RemoveTerrainGroup(_selectedGroup.Id);
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnAddTilePressed()
    {
        if (_selectedGroup == null) return;
        EmitSignal(SignalName.TilePickerRequested, _selectedGroup.Id);
    }

    public void AddTileToSelectedGroup(string tileId)
    {
        if (_selectedGroup == null) return;
        if (_service.AddTileToGroup(_selectedGroup.Id, tileId))
        {
            UpdateMemberDisplay();
        }
    }

    private void OnRemoveTilePressed()
    {
        if (_selectedGroup == null) return;

        var selected = _memberList!.GetSelectedItems();
        if (selected.Length == 0) return;

        var tileId = _memberList.GetItemMetadata(selected[0]).AsString();
        if (_service.RemoveTileFromGroup(_selectedGroup.Id, tileId))
        {
            UpdateMemberDisplay();
        }
    }

    private void OnGroupModified(string groupId)
    {
        if (_selectedGroup?.Id == groupId)
        {
            _selectedGroup = _service.GetTerrainGroup(groupId);
        }
        RefreshDisplay();
    }

    private void OnGroupAdded(string groupId)
    {
        RefreshDisplay();
    }

    private void OnGroupRemoved(string groupId)
    {
        if (_selectedGroup?.Id == groupId)
        {
            _selectedGroup = null;
        }
        RefreshDisplay();
    }
}
#endif
