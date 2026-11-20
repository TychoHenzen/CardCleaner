using System;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     What one left click does: the prompt shown under the crosshair and the move that carries the click out. The same
///     plan judges the target, shows the prompt and runs on the click, so the prompt always names what the click does.
/// </summary>
/// <param name="Prompt">What the click does ("Pick up card").</param>
/// <param name="Apply">Carries the click out; false when it moved nothing after all.</param>
public sealed record ClickPlan(string Prompt, Func<bool> Apply);
