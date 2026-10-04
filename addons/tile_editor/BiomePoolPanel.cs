#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for managing biome tile pools - dynamically loads biomes from tiles.json
/// </summary>
[Tool]
public partial class BiomePoolPanel : VBoxContainer
{
    private readonly TileEditorService? _service;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _biomesContainer;
    private readonly Dictionary<string, BiomeSection> _biomeSections = new();

    // Add Biome dialog
    private AcceptDialog? _addBiomeDialog;
    private LineEdit? _biomeIdField;
    private LineEdit? _biomeDisplayNameField;
    private Label? _biomeValidationLabel;

    // Required by Godot for [Tool] classes
    public BiomePoolPanel() { }

    public BiomePoolPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += OnTilesLoaded;
        _service.BiomesLoaded += OnBiomesLoaded;
        _service.TileModified += OnTileModified;
        _service.BiomeAdded += OnBiomeAdded;
        _service.BiomeRemoved += OnBiomeRemoved;
    }

    public override void _Ready()
    {
        // Guard for Godot's parameterless constructor case
        if (_service == null) return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Toolbar
        var toolbar = new HBoxContainer();
        AddChild(toolbar);

        var addBiomeButton = new Button
        {
            Text = "Add Biome",
            TooltipText = "Create a new biome"
        };
        addBiomeButton.Pressed += OnAddBiomePressed;
        toolbar.AddChild(addBiomeButton);

        toolbar.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.Expand });

        // Scroll container for biome sections
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(_scrollContainer);

        _biomesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scrollContainer.AddChild(_biomesContainer);

        // Sections will be created when BiomesLoaded signal fires
    }

    private void OnTilesLoaded()
    {
        RefreshDisplay();
    }

    private void OnBiomesLoaded()
    {
        RebuildBiomeSections();
    }

    private void OnBiomeAdded(string biomeId)
    {
        RebuildBiomeSections();
    }

    private void OnBiomeRemoved(string biomeId)
    {
        RebuildBiomeSections();
    }

    private void RebuildBiomeSections()
    {
        if (_service == null || _biomesContainer == null) return;

        // Clear existing sections
        foreach (var child in _biomesContainer.GetChildren())
        {
            child.QueueFree();
        }
        _biomeSections.Clear();

        // Create sections for each loaded biome
        foreach (var biome in _service.AllBiomes.OrderBy(b => b.DisplayName))
        {
            var section = new BiomeSection(biome.Id, _service);
            _biomesContainer.AddChild(section);
            _biomeSections[biome.Id] = section;
        }

        // Universal tiles section (tiles with no biome restrictions)
        var universalSection = new BiomeSection("universal", _service, isUniversal: true);
        _biomesContainer.AddChild(universalSection);
        _biomeSections["universal"] = universalSection;

        // Refresh tile display after sections are built
        CallDeferred(nameof(RefreshDisplay));
    }

    private void RefreshDisplay()
    {
        foreach (var section in _biomeSections.Values)
        {
            section.RefreshTiles();
        }
    }

    private void OnTileModified(string tileId)
    {
        // Refresh all sections as tile biomes may have changed
        RefreshDisplay();
    }

    private void OnAddBiomePressed()
    {
        // Create dialog lazily
        if (_addBiomeDialog == null)
        {
            _addBiomeDialog = new AcceptDialog
            {
                Title = "Add New Biome",
                OkButtonText = "Create",
                Size = new Vector2I(400, 180)
            };

            var vbox = new VBoxContainer();
            _addBiomeDialog.AddChild(vbox);

            vbox.AddChild(new Label { Text = "Biome ID (snake_case):" });
            _biomeIdField = new LineEdit
            {
                PlaceholderText = "new_biome_id",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _biomeIdField.TextChanged += OnBiomeIdChanged;
            vbox.AddChild(_biomeIdField);

            vbox.AddChild(new Label { Text = "Display Name:" });
            _biomeDisplayNameField = new LineEdit
            {
                PlaceholderText = "New Biome",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            vbox.AddChild(_biomeDisplayNameField);

            _biomeValidationLabel = new Label
            {
                Text = "",
                Modulate = new Color(1, 0.3f, 0.3f)
            };
            vbox.AddChild(_biomeValidationLabel);

            _addBiomeDialog.Confirmed += OnAddBiomeConfirmed;
            AddChild(_addBiomeDialog);
        }

        // Reset dialog
        _biomeIdField!.Text = "";
        _biomeDisplayNameField!.Text = "";
        OnBiomeIdChanged("");
        _addBiomeDialog.PopupCentered();
    }

    private void OnBiomeIdChanged(string newId)
    {
        if (_biomeValidationLabel == null || _addBiomeDialog == null) return;

        var validation = ValidateBiomeId(newId);
        _biomeValidationLabel.Text = validation.Valid ? "" : validation.Message;
        _addBiomeDialog.GetOkButton().Disabled = !validation.Valid;
    }

    private BiomeIdValidation ValidateBiomeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new BiomeIdValidation(false, "ID is required");

        if (!Regex.IsMatch(id, @"^[a-z][a-z0-9_]*$"))
            return new BiomeIdValidation(false, "ID must be snake_case starting with a letter");

        if (_service.GetBiome(id) != null)
            return new BiomeIdValidation(false, $"Biome '{id}' already exists");

        return new BiomeIdValidation(true, "");
    }

    private void OnAddBiomeConfirmed()
    {
        if (_biomeIdField == null || _biomeDisplayNameField == null) return;

        var id = _biomeIdField.Text.Trim();
        var displayName = _biomeDisplayNameField.Text.Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = char.ToUpper(id[0]) + id[1..].Replace("_", " ");

        var validation = ValidateBiomeId(id);
        if (!validation.Valid) return;

        var biome = new EditableBiome
        {
            Id = id,
            DisplayName = displayName,
            Signature = new float[8],
            BlockedPercentage = 0.2f,
            PassableTiles = new Dictionary<string, float>(),
            BlockedTiles = new Dictionary<string, float>()
        };

        _service.AddBiome(biome);
    }
}

#endif
