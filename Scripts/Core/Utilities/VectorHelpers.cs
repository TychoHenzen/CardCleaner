using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

public static class VectorHelpers
{
    public static Vector2I YZ(this Vector3I vector)
    {
        return new Vector2I(vector.Y, vector.Z);
    }
}