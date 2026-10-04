using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;

/// <summary>
/// Inclusive min/max tile coordinates covered by a TMX map.
/// </summary>
internal readonly record struct TmxBounds(Vector2I Min, Vector2I Max);
