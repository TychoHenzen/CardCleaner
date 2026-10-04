#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileAtlasPanel
{
    public override void _Ready()
    {
        if (_service == null)
            return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(vbox);
        vbox.AddChild(CreateFilterBar());
        vbox.AddChild(CreateActionToolbar());
        CreateTileGrid(vbox);
    }

    private HBoxContainer CreateFilterBar()
    {
        var filterBar = new HBoxContainer();
        filterBar.AddChild(new Label { Text = "Search:" });
        _searchBox = new LineEdit
        {
            PlaceholderText = "Filter tiles...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _searchBox.TextChanged += _ => RefreshTileDisplay();
        filterBar.AddChild(_searchBox);

        filterBar.AddChild(new Label { Text = "Biome:" });
        _biomeFilter = new OptionButton();
        AddBiomeFilterItems();
        _biomeFilter.ItemSelected += _ => RefreshTileDisplay();
        filterBar.AddChild(_biomeFilter);

        filterBar.AddChild(new Label { Text = "Passability:" });
        _passabilityFilter = new OptionButton();
        _passabilityFilter.AddItem("All", 0);
        _passabilityFilter.AddItem("Passable", 1);
        _passabilityFilter.AddItem("Solid", 2);
        _passabilityFilter.ItemSelected += _ => RefreshTileDisplay();
        filterBar.AddChild(_passabilityFilter);

        filterBar.AddChild(new Label { Text = "Layer:" });
        _layerFilter = new OptionButton();
        AddLayerFilterItems();
        _layerFilter.ItemSelected += _ => RefreshTileDisplay();
        filterBar.AddChild(_layerFilter);
        return filterBar;
    }

    private void AddBiomeFilterItems()
    {
        _biomeFilter!.AddItem("All", 0);
        _biomeFilter.AddItem("Plains", 1);
        _biomeFilter.AddItem("Forest", 2);
        _biomeFilter.AddItem("Desert", 3);
        _biomeFilter.AddItem("Tundra", 4);
        _biomeFilter.AddItem("Swamp", 5);
        _biomeFilter.AddItem("Mountains", 6);
        _biomeFilter.AddItem("Universal", 7);
    }

    private void AddLayerFilterItems()
    {
        _layerFilter!.AddItem("All", 0);
        _layerFilter.AddItem("Terrain", 1);
        _layerFilter.AddItem("Decoration", 2);
        _layerFilter.AddItem("Structure", 3);
        _layerFilter.AddItem("Effects", 4);
    }

    private HBoxContainer CreateActionToolbar()
    {
        var toolbar = new HBoxContainer();
        _duplicateButton = new Button
        {
            Text = "Duplicate",
            TooltipText = "Duplicate selected tile with a new ID",
            Disabled = true
        };
        _duplicateButton.Pressed += OnDuplicatePressed;
        toolbar.AddChild(_duplicateButton);

        _deleteButton = new Button
        {
            Text = "Delete",
            TooltipText = "Delete selected tile",
            Disabled = true
        };
        _deleteButton.Pressed += OnDeletePressed;
        toolbar.AddChild(_deleteButton);
        toolbar.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.Expand });
        return toolbar;
    }

    private void CreateTileGrid(VBoxContainer vbox)
    {
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        vbox.AddChild(_scrollContainer);
        _tileGrid = new GridContainer
        {
            Columns = TilesPerRow,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scrollContainer.AddChild(_tileGrid);
    }
}
#endif
