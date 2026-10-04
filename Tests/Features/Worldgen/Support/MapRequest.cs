using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Generated inputs for one map generation property run: RNG seed and requested map dimensions.
/// </summary>
public sealed record MapRequest(int Seed, int Width, int Height)
{
    public ulong RngSeed => (ulong)Seed;

    public Vector2I Size => new(Width, Height);
}
