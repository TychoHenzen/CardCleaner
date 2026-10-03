#if TOOLS
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileAtlasPanel
{
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_selectedTileId == null)
            return default;

        var tile = _service!.GetTile(_selectedTileId);
        if (tile == null)
            return default;

        var preview = new Label
        {
            Text = tile.Name,
            Modulate = new Color(1, 1, 1, 0.8f)
        };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary
        {
            { "type", "tile" },
            { "tile_id", _selectedTileId }
        };
    }

    private void OnDuplicatePressed()
    {
        if (_selectedTileId == null)
            return;
        var tile = _service!.GetTile(_selectedTileId);
        if (tile == null)
            return;

        CreateDuplicateDialog();
        _duplicateIdField!.Text = tile.Id + "_copy";
        OnDuplicateIdChanged(_duplicateIdField.Text);
        _duplicateDialog!.PopupCentered();
    }

    private void CreateDuplicateDialog()
    {
        if (_duplicateDialog != null)
            return;

        _duplicateDialog = new AcceptDialog
        {
            Title = "Duplicate Tile",
            OkButtonText = "Duplicate",
            Size = new Vector2I(400, 150)
        };
        var vbox = new VBoxContainer();
        _duplicateDialog.AddChild(vbox);
        vbox.AddChild(new Label { Text = "Enter a new ID for the duplicated tile:" });
        _duplicateIdField = new LineEdit
        {
            PlaceholderText = "new_tile_id (snake_case)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _duplicateIdField.TextChanged += OnDuplicateIdChanged;
        vbox.AddChild(_duplicateIdField);
        _duplicateValidationLabel = new Label
        {
            Text = "",
            Modulate = new Color(1, 0.3f, 0.3f)
        };
        vbox.AddChild(_duplicateValidationLabel);
        _duplicateDialog.Confirmed += OnDuplicateConfirmed;
        AddChild(_duplicateDialog);
    }

    private void OnDuplicateIdChanged(string newId)
    {
        if (_duplicateValidationLabel == null || _duplicateDialog == null)
            return;

        var validation = ValidateTileId(newId);
        _duplicateValidationLabel.Text = validation.Valid ? "" : validation.Message;
        _duplicateDialog.GetOkButton().Disabled = !validation.Valid;
    }

    private TileIdValidation ValidateTileId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new TileIdValidation(false, "ID is required");
        if (!Regex.IsMatch(id, @"^[a-z][a-z0-9_]*$"))
            return new TileIdValidation(false, "ID must be snake_case starting with a letter");
        if (_service!.GetTile(id) != null)
            return new TileIdValidation(false, $"Tile '{id}' already exists");
        return new TileIdValidation(true, "");
    }

    private void OnDuplicateConfirmed()
    {
        if (_selectedTileId == null || _duplicateIdField == null)
            return;

        var sourceTile = _service!.GetTile(_selectedTileId);
        if (sourceTile == null)
            return;

        var newId = _duplicateIdField.Text.Trim();
        var validation = ValidateTileId(newId);
        if (!validation.Valid)
            return;

        var clone = sourceTile.Clone();
        clone.Id = newId;
        if (_service.AddTile(clone))
            CallDeferred(nameof(SelectTileDeferred), newId);
    }

    private void SelectTileDeferred(string tileId)
    {
        SelectTile(tileId);
    }

    private void OnDeletePressed()
    {
        if (_selectedTileId == null)
            return;
        var tile = _service!.GetTile(_selectedTileId);
        if (tile == null)
            return;

        CreateDeleteDialog();
        _deleteDialog!.DialogText =
            $"Are you sure you want to delete tile '{tile.Name}' ({tile.Id})?\n\n"
            + "This action cannot be undone.";
        _deleteDialog.PopupCentered();
    }

    private void CreateDeleteDialog()
    {
        if (_deleteDialog != null)
            return;

        _deleteDialog = new ConfirmationDialog
        {
            Title = "Delete Tile",
            OkButtonText = "Delete",
            Size = new Vector2I(400, 120)
        };
        _deleteDialog.Confirmed += OnDeleteConfirmed;
        AddChild(_deleteDialog);
    }

    private void OnDeleteConfirmed()
    {
        if (_selectedTileId == null)
            return;

        var idToDelete = _selectedTileId;
        _selectedTileId = null;
        UpdateToolbarState();
        _service!.RemoveTile(idToDelete);
    }
}
#endif
