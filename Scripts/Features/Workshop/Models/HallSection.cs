using Godot;

namespace CardCleaner.Scripts.Features.Workshop.Models;

/// <summary>A named room or hallway with a spot on its floor, in metres in workshop space, that a player must be able to reach.</summary>
public readonly record struct HallSection(string Name, Vector2 Anchor);
