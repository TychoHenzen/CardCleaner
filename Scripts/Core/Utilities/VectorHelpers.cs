using Godot;

public static class VectorHelpers
{
    public static Vector2I YZ(this Vector3I vector) => new(vector.Y, vector.Z);
}