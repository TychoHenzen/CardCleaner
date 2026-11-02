using Godot;

namespace CardCleaner.Scripts.Features.Menu.Services;

/// <summary>
///     The engine-facing display state derived from <see cref="Models.GameOptions" />: the window mode, the window
///     size and the vsync mode. Size is null in fullscreen, where the engine picks the mode. Built by
///     <see cref="OptionsApplier.DisplayFor" /> so the mapping is testable without a window.
/// </summary>
public sealed record DisplayPlan(DisplayServer.WindowMode Mode, Vector2I? Size, DisplayServer.VSyncMode VSync);
