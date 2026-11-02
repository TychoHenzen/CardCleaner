using Godot;

namespace CardCleaner.Scripts.Features.Menu.Services;

/// <summary>Window mode, size and vsync derived from <see cref="Models.GameOptions" />; Size is null in fullscreen.
/// Built by <see cref="OptionsApplier.DisplayFor" /> so it is testable without a window.</summary>
public sealed record DisplayPlan(DisplayServer.WindowMode Mode, Vector2I? Size, DisplayServer.VSyncMode VSync);
