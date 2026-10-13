using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for IrregularMeshFogOfWar, the passive fog the session's exploration feeds: fog states from the seen and
/// visible sets, cells that never fall back to hidden, reveal and reset, and the change events.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshFogOfWarTest
{
    private IrregularMeshNs.IrregularMeshMapData _mapData = null!;
    private IrregularMeshNs.IrregularMeshFogOfWar _fogOfWar = null!;

    [BeforeTest]
    public void Setup()
    {
        _mapData = new IrregularMeshNs.IrregularMeshMapData(CreateSimpleTestMesh());
        _fogOfWar = new IrregularMeshNs.IrregularMeshFogOfWar(_mapData);
    }

    private static HashSet<int> Cells(params int[] cellIds) => new(cellIds);

    [TestCase]
    [TestCategory("Unit")]
    public void AllCellsStartHidden()
    {
        for (var i = 0; i < _mapData.CellCount; i++)
            AssertThat(_fogOfWar.GetFogState(i)).IsEqual(FogState.Hidden);

        AssertThat(_fogOfWar.SeenCells.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void VisibleCellsShowAndSeenCellsAreRevealed()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 1, 2), Cells(0, 1));

        AssertThat(_fogOfWar.GetFogState(0)).IsEqual(FogState.Visible);
        AssertThat(_fogOfWar.GetFogState(1)).IsEqual(FogState.Visible);
        AssertThat(_fogOfWar.GetFogState(2)).IsEqual(FogState.Revealed);
        AssertThat(_fogOfWar.GetFogState(3)).IsEqual(FogState.Hidden);
        AssertBool(_fogOfWar.HasBeenSeen(2)).IsTrue();
        AssertBool(_fogOfWar.IsVisible(2)).IsFalse();
        AssertThat(_fogOfWar.CurrentlyVisibleCells).ContainsExactlyInAnyOrder(0, 1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACellThatLeavesViewIsRevealed()
    {
        _fogOfWar.UpdateVisibility(Cells(0), Cells(0));

        _fogOfWar.UpdateVisibility(Cells(0, 1), Cells(1));

        AssertThat(_fogOfWar.GetFogState(0)).IsEqual(FogState.Revealed);
        AssertThat(_fogOfWar.GetFogState(1)).IsEqual(FogState.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ASmallerSeenSetNeverHidesACellAgain()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 1, 2), Cells(0));

        // A resume after a combat starts a new explorer, whose seen set starts over.
        _fogOfWar.UpdateVisibility(Cells(3), Cells(3));

        AssertThat(_fogOfWar.GetFogState(0)).IsEqual(FogState.Revealed);
        AssertThat(_fogOfWar.GetFogState(1)).IsEqual(FogState.Revealed);
        AssertThat(_fogOfWar.GetFogState(2)).IsEqual(FogState.Revealed);
        AssertThat(_fogOfWar.GetFogState(3)).IsEqual(FogState.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CellsOutsideTheMapAreIgnored()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 99), Cells(0, 99));

        AssertThat(_fogOfWar.CurrentlyVisibleCells).ContainsExactlyInAnyOrder(0);
        AssertThat(_fogOfWar.GetFogState(99)).IsEqual(FogState.Hidden);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void VisibilityChangedNamesOnlyTheChangedCells()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 1), Cells(0));
        IReadOnlySet<int>? changed = null;
        _fogOfWar.VisibilityChanged += cells => changed = cells;

        _fogOfWar.UpdateVisibility(Cells(0, 1, 2), Cells(0, 2));

        AssertThat(changed).IsNotNull();
        AssertThat(changed!).ContainsExactlyInAnyOrder(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnUnchangedUpdateRaisesNoEvent()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 1), Cells(0));
        var raised = 0;
        _fogOfWar.VisibilityChanged += _ => raised++;

        _fogOfWar.UpdateVisibility(Cells(0, 1), Cells(0));

        AssertInt(raised).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RevealAllShowsEveryCell()
    {
        _fogOfWar.RevealAll();

        for (var i = 0; i < _mapData.CellCount; i++)
            AssertBool(_fogOfWar.IsVisible(i)).IsTrue();

        AssertThat(_fogOfWar.CurrentlyVisibleCells.Count).IsEqual(_mapData.CellCount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ResetHidesEveryCell()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 1, 2), Cells(0));

        _fogOfWar.Reset();

        for (var i = 0; i < _mapData.CellCount; i++)
            AssertThat(_fogOfWar.GetFogState(i)).IsEqual(FogState.Hidden);

        AssertThat(_fogOfWar.CurrentlyVisibleCells.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GetCellsWithStateListsTheCellsInThatState()
    {
        _fogOfWar.UpdateVisibility(Cells(0, 1), Cells(0));

        AssertThat(_fogOfWar.GetCellsWithState(FogState.Visible)).ContainsExactlyInAnyOrder(0);
        AssertThat(_fogOfWar.GetCellsWithState(FogState.Revealed)).ContainsExactlyInAnyOrder(1);
        AssertThat(_fogOfWar.GetCellsWithState(FogState.Hidden)).ContainsExactlyInAnyOrder(2, 3);
    }

    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create 3x3 grid of vertices
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32)
        };

        foreach (var pos in positions)
        {
            var id = mesh.AddVertex(pos);
            mesh.Vertices[id].TerrainType = 1;
        }

        // Add 4 quads
        mesh.AddQuad(new[] { 0, 1, 4, 3 });
        mesh.AddQuad(new[] { 1, 2, 5, 4 });
        mesh.AddQuad(new[] { 3, 4, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 8, 7 });

        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
