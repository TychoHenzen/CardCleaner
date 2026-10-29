using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>The shape facts of one auto-tile format: its allowed bitmasks and its largest multi-cell bounds.</summary>
internal readonly record struct TileShape(
    IReadOnlySet<int>? AllowedBitmasks,
    (Vector2I Size, Vector2I Offset)? MultiCellBounds);
