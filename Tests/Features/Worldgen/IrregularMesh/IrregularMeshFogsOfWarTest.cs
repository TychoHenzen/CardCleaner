using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.TestUtilities.Fixtures;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshFogsOfWarTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void CreateReturnsAPassiveFogWithEveryCellHidden()
    {
        var (_, gridData) = OpenFloorMap.Create(3, 3);

        IPassiveFogOfWar fog = IrregularMeshFogsOfWar.Create(gridData);

        AssertBool(fog is IrregularMeshFogOfWar).IsTrue();
        AssertThat(fog.GetFogState(0)).IsEqual(FogState.Hidden);
        fog.UpdateVisibility(new HashSet<int> { 0 }, new HashSet<int> { 0 });
        AssertThat(fog.GetFogState(0)).IsEqual(FogState.Visible);
    }
}
