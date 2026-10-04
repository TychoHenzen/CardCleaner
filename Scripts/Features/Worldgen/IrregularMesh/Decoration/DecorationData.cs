using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Data for a placed decoration.
/// </summary>
public class DecorationData
{
    public int QuadId { get; init; }
    public DecorationType Type { get; init; }
    public int Variation { get; init; }
    public Vector2 Position { get; init; }
    public float Rotation { get; init; }
    public Vector2 Scale { get; init; }
}
