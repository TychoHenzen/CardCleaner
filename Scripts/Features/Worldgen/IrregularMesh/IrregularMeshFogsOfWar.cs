using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

public static class IrregularMeshFogsOfWar
{
    public static IPassiveFogOfWar Create(IMapData mapData) => new IrregularMeshFogOfWar(mapData);
}
