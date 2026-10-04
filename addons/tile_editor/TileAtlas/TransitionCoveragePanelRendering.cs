#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TransitionCoveragePanel
{
    private void RefreshDisplay()
    {
        if (_service == null || _container == null || _summaryLabel == null || _matrixGrid == null)
            return;

        _coverageMatrix = CalculateCoverageMatrix();
        UpdateSummary();
        ClearMatrixGrid();
        if (_coverageMatrix.TerrainIds.Count == 0)
            return;

        PopulateMatrixGrid();
    }

    private void UpdateSummary()
    {
        var total = _coverageMatrix!.TotalTransitions;
        var covered = _coverageMatrix.CoveredCount;
        var missing = _coverageMatrix.MissingCount;
        if (total == 0)
        {
            _summaryLabel!.Text = "No terrain tiles found";
            _summaryLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
            return;
        }

        if (missing == 0)
        {
            _summaryLabel!.Text = $"All {covered} transitions covered";
            _summaryLabel.Modulate = new Color(0.5f, 0.9f, 0.5f);
            return;
        }

        var percentage = (float)covered / total * 100;
        _summaryLabel!.Text =
            $"{covered}/{total} transitions covered ({percentage:F0}%), {missing} missing";
        _summaryLabel.Modulate = new Color(0.9f, 0.7f, 0.3f);
    }

    private void ClearMatrixGrid()
    {
        foreach (var child in _matrixGrid!.GetChildren())
            child.QueueFree();
    }

    private void PopulateMatrixGrid()
    {
        var terrainIds = _coverageMatrix!.TerrainIds;
        _matrixGrid!.Columns = terrainIds.Count + 1;
        _matrixGrid.AddChild(CreateHeaderCell("Inner→Outer"));
        foreach (var terrainId in terrainIds)
            _matrixGrid.AddChild(CreateHeaderCell(terrainId));

        foreach (var innerTerrain in terrainIds)
        {
            _matrixGrid.AddChild(CreateHeaderCell(innerTerrain));
            foreach (var outerTerrain in terrainIds)
            {
                var status = _coverageMatrix.GetStatus(innerTerrain, outerTerrain);
                _matrixGrid.AddChild(CreateStatusCell(status, innerTerrain, outerTerrain));
            }
        }
    }
}
#endif
