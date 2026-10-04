using Godot;

namespace CardCleaner.Scripts.Core.Devices;

/// <summary>
/// World positions of the jack markers a cable connects; either end is null when its marker is missing.
/// </summary>
internal readonly record struct CableEndpoints(Vector3? Start, Vector3? End);
