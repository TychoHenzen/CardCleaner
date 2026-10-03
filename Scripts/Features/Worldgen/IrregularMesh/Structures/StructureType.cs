namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Types of structures that can be placed on the mesh.
/// </summary>
public enum StructureType
{
    /// <summary>
    /// Basic wall that blocks movement and line of sight.
    /// </summary>
    Wall,

    /// <summary>
    /// Door that can be opened/closed.
    /// </summary>
    Door,

    /// <summary>
    /// Bridge that allows crossing water.
    /// </summary>
    Bridge,

    /// <summary>
    /// Fence that blocks movement but not line of sight.
    /// </summary>
    Fence,

    /// <summary>
    /// Column/pillar for structural support.
    /// </summary>
    Column
}
