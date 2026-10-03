using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of scanning a grid for open cells: those next to collapsed cells, and the first open cell.
/// </summary>
internal readonly record struct FrontierScan(List<Vector2I> FrontierCells, Vector2I? AnyUncollapsed);
