#if TOOLS
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Reusable popup for selecting a tile from available tiles.
/// Shows tile grid with search filtering.
/// </summary>
[Tool]
public partial class TilePickerPopup : Window
{
    private readonly TileEditorService _service;
    private LineEdit? _searchEdit;
    private ItemList? _tileList;
    private Button? _clearButton;
    private Button? _cancelButton;
    private string? _selectedTileId;

    [Signal]
    public delegate void TileSelectedEventHandler(string? tileId);

    public TilePickerPopup(TileEditorService service)
    {
        _service = service;

        Title = "Select Tile";
        Size = new Vector2I(400, 500);
        Unresizable = false;
        Transient = true;
        Exclusive = true;

        SetupUI();
    }

    private void SetupUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.OffsetLeft = 8;
        vbox.OffsetTop = 8;
        vbox.OffsetRight = -8;
        vbox.OffsetBottom = -8;
        AddChild(vbox);

        // Search bar
        var searchRow = new HBoxContainer();
        vbox.AddChild(searchRow);

        searchRow.AddChild(new Label { Text = "Search:" });
        _searchEdit = new LineEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = "Filter tiles..."
        };
        _searchEdit.TextChanged += OnSearchChanged;
        searchRow.AddChild(_searchEdit);

        // Tile list
        _tileList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowSearch = true
        };
        _tileList.ItemSelected += OnTileItemSelected;
        _tileList.ItemActivated += OnTileItemActivated;
        vbox.AddChild(_tileList);

        // Buttons
        var buttonRow = new HBoxContainer();
        vbox.AddChild(buttonRow);

        _clearButton = new Button { Text = "Clear (No Tile)" };
        _clearButton.Pressed += OnClearPressed;
        buttonRow.AddChild(_clearButton);

        buttonRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _cancelButton = new Button { Text = "Cancel" };
        _cancelButton.Pressed += OnCancelPressed;
        buttonRow.AddChild(_cancelButton);

        var selectButton = new Button { Text = "Select" };
        selectButton.Pressed += OnSelectPressed;
        buttonRow.AddChild(selectButton);

        // Connect close requested
        CloseRequested += OnCancelPressed;
    }

    public override void _Ready()
    {
        RefreshTileList();
    }

    private void RefreshTileList(string filter = "")
    {
        _tileList!.Clear();

        var tiles = _service.AllTiles
            .Where(t => string.IsNullOrEmpty(filter) ||
                        t.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase) ||
                        t.Id.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name);

        foreach (var tile in tiles)
        {
            var displayText = $"{tile.Name} ({tile.Id})";
            _tileList.AddItem(displayText);
            _tileList.SetItemMetadata(_tileList.ItemCount - 1, tile.Id);

            // Try to show tile icon
            var texture = _service.GetTileTexture(tile);
            if (texture != null)
            {
                var region = _service.GetTileTextureRegion(tile);
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = region
                };
                _tileList.SetItemIcon(_tileList.ItemCount - 1, atlas);
            }
        }
    }

    private void OnSearchChanged(string newText)
    {
        RefreshTileList(newText);
    }

    private void OnTileItemSelected(long index)
    {
        _selectedTileId = _tileList!.GetItemMetadata((int)index).AsString();
    }

    private void OnTileItemActivated(long index)
    {
        _selectedTileId = _tileList!.GetItemMetadata((int)index).AsString();
        ConfirmSelection();
    }

    private void OnSelectPressed()
    {
        if (_selectedTileId == null)
        {
            var selected = _tileList!.GetSelectedItems();
            if (selected.Length > 0)
            {
                _selectedTileId = _tileList.GetItemMetadata(selected[0]).AsString();
            }
        }

        if (_selectedTileId != null)
        {
            ConfirmSelection();
        }
    }

    private void OnClearPressed()
    {
        _selectedTileId = null;
        EmitSignal(SignalName.TileSelected, Variant.CreateFrom<string?>(null));
        Hide();
    }

    private void OnCancelPressed()
    {
        Hide();
    }

    private void ConfirmSelection()
    {
        EmitSignal(SignalName.TileSelected, _selectedTileId ?? "");
        Hide();
    }

    public void ShowPicker()
    {
        _selectedTileId = null;
        _searchEdit!.Text = "";
        RefreshTileList();
        PopupCentered();
        _searchEdit.GrabFocus();
    }
}
#endif
