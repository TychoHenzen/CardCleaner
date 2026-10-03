using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Deterministic position hash used to pick tile variants.
/// </summary>
internal static class TerrainPositionHash
{
    internal static int Compute(Vector2 pos)
    {
        int x = (int)(pos.X * 1000);
        int y = (int)(pos.Y * 1000);
        return x * 31 + y;
    }
}
