#if TOOLS
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class DualGridAutoTilePreview
{
    public override void _GuiInput(InputEvent @event)
    {
        if (_overlayTile == null)
            return;

        if (@event is InputEventMouseButton mouseButton
            && mouseButton.Pressed
            && mouseButton.ButtonIndex == MouseButton.Left)
        {
            ToggleDataCell(mouseButton.Position);
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            UpdateTooltip(mouseMotion.Position);
        }
    }

    private void ToggleDataCell(Vector2 position)
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var adjustedPosition = position - scaledTileSize / 2;
        var col = (int)(adjustedPosition.X / scaledTileSize.X);
        var row = (int)(adjustedPosition.Y / scaledTileSize.Y);
        if (row < 0 || row >= DataGridRows || col < 0 || col >= DataGridCols)
            return;

        _dataGrid[row, col] = !_dataGrid[row, col];
        RecomputeVisualBitmasks();
        QueueRedraw();
        EmitInfo();
    }

    private void RecomputeVisualBitmasks()
    {
        var format = GetEffectiveFormat();
        if (format == "blob47")
        {
            RecomputeBlobBitmasks();
            return;
        }

        _visualBitmasks = DualGridAutoTile.ComputeAllBitmasks(_dataGrid);
    }

    private void RecomputeBlobBitmasks()
    {
        _visualBitmasks = new int[DataGridRows, DataGridCols];
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            if (!_dataGrid[row, col])
            {
                _visualBitmasks[row, col] = -1;
                continue;
            }

            var position = new Vector2I(col, row);
            _visualBitmasks[row, col] = NeighborBitmask8.Compute(position, neighborPosition =>
            {
                if (neighborPosition.X < 0 || neighborPosition.X >= DataGridCols
                    || neighborPosition.Y < 0 || neighborPosition.Y >= DataGridRows)
                {
                    return false;
                }

                return _dataGrid[neighborPosition.Y, neighborPosition.X];
            });
        }
    }

    private void EmitInfo()
    {
        if (_overlayTile == null)
            return;

        var filledCount = CountFilledCells();
        var format = GetEffectiveFormat();
        var formatText = string.IsNullOrEmpty(_formatOverride)
            ? format
            : $"{format} (overridden)";
        var info = $"Data grid: {filledCount}/{DataGridRows * DataGridCols} cells filled. "
            + $"Format: {formatText}. Click to toggle cells.";
        EmitSignal(SignalName.InfoChanged, info);
    }

    private int CountFilledCells()
    {
        var filledCount = 0;
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            if (_dataGrid[row, col])
                filledCount++;
        }

        return filledCount;
    }
}
#endif
