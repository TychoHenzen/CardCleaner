using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Pipeline;

/// <summary>
///     Shared fixture for the PipelineIntegrationTest scenario suites.
/// </summary>
public abstract class PipelineIntegrationTestBase
{
    protected TileRegistry _registry = null!;

    protected CompiledTransitionResolver _resolver = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new TileRegistry();
        _resolver = new CompiledTransitionResolver();
    }

    /// <summary>
    ///     Enumerates every visual tile of the dual grid for a data grid of the given size (visual = data + 1).
    /// </summary>
    protected static IEnumerable<VisualCell> EnumerateVisualGrid(int dataWidth, int dataHeight)
    {
        for (var vy = 0; vy <= dataHeight; vy++)
        {
            for (var vx = 0; vx <= dataWidth; vx++)
            {
                yield return new VisualCell(vx, vy);
            }
        }
    }

    /// <summary>
    ///     True when the visual tile is fully inside the data grid (not on the dual-grid border).
    /// </summary>
    protected static bool IsInteriorCell(VisualCell cell, int dataWidth, int dataHeight)
    {
        return cell.X > 0 && cell.X < dataWidth && cell.Y > 0 && cell.Y < dataHeight;
    }

    protected static string FormatCoords(Vector2I? coords)
    {
        return coords.HasValue ? $"({coords.Value.X},{coords.Value.Y})" : "NULL";
    }

    protected static void PrintErrors(string heading, List<string> errors)
    {
        if (errors.Count > 0)
            GD.PrintErr($"{heading}:\n{string.Join("\n", errors)}");
    }

    protected readonly record struct VisualCell(int X, int Y);

    protected readonly record struct ResolvedCell(VisualCell Cell, int Bitmask, Vector2I? Coords);
}
