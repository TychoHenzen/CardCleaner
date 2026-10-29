using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

[TestSuite]
[RequireGodotRuntime]
public class IslandTerrainAssignerTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void AssignFillsTheSameVerticesAsBefore()
    {
        IIrregularMesh mesh = MeshGenerator.Generate(3, 7);
        var filled = IslandTerrainAssigner.Assign(mesh, 11, 2);

        AssertInt(filled).IsEqual(23);

        int terrainOne = 0;
        bool onlyZeroOrOne = true;
        foreach (var vertex in mesh.Vertices)
        {
            if (vertex.TerrainType == 1)
            {
                terrainOne++;
            }
            else if (vertex.TerrainType != 0)
            {
                onlyZeroOrOne = false;
            }
        }

        AssertBool(onlyZeroOrOne).IsTrue();
        AssertInt(terrainOne).IsEqual(filled);
    }
}
