using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.StructurePlacementScenarios;

/// <summary>
///     StructurePlacementRulesTest scenarios split out of StructurePlacementTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StructurePlacementRulesTest : StructurePlacementTestBase
{
    [TestCase]
    public void TestCanPlaceStructureOnValidVertex()
    {
        // Interior vertex (id=5) should be valid for placement
        var canPlace = _placement.CanPlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsTrue();
    }

    [TestCase]
    public void TestCannotPlaceStructureOnInvalidVertex()
    {
        // Invalid vertex ID
        var canPlace = _placement.CanPlaceStructure(-1, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsFalse();

        canPlace = _placement.CanPlaceStructure(999, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestCannotPlaceStructureOnBoundaryVertex()
    {
        // Mark a vertex as boundary
        _testMesh.Vertices[0].IsBoundary = true;

        var canPlace = _placement.CanPlaceStructure(0, IrregularMeshNs.StructureType.Wall);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestCannotPlaceStructureOnExistingStructure()
    {
        // Place first structure
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);

        // Cannot place another at same vertex
        var canPlace = _placement.CanPlaceStructure(5, IrregularMeshNs.StructureType.Door);
        AssertBool(canPlace).IsFalse();
    }

    [TestCase]
    public void TestPlaceStructureUpdatesVertexHasStructure()
    {
        AssertBool(_testMesh.Vertices[5].HasStructure).IsFalse();

        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);

        AssertBool(_testMesh.Vertices[5].HasStructure).IsTrue();
    }

    [TestCase]
    public void TestPlaceStructureReturnsTrue()
    {
        var result = _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestPlaceStructureOnInvalidVertexReturnsFalse()
    {
        var result = _placement.PlaceStructure(-1, IrregularMeshNs.StructureType.Wall);
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestRemoveStructure()
    {
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Wall);
        AssertBool(_testMesh.Vertices[5].HasStructure).IsTrue();

        var result = _placement.RemoveStructure(5);

        AssertBool(result).IsTrue();
        AssertBool(_testMesh.Vertices[5].HasStructure).IsFalse();
    }

    [TestCase]
    public void TestRemoveStructureFromEmptyVertexReturnsFalse()
    {
        var result = _placement.RemoveStructure(5);
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void TestGetStructureAt()
    {
        _placement.PlaceStructure(5, IrregularMeshNs.StructureType.Door);

        var structureType = _placement.GetStructureAt(5);

        AssertThat(structureType).IsNotNull();
        AssertThat(structureType).IsEqual(IrregularMeshNs.StructureType.Door);
    }

    [TestCase]
    public void TestGetStructureAtEmptyVertexReturnsNull()
    {
        var structureType = _placement.GetStructureAt(5);
        AssertThat(structureType).IsNull();
    }
}
