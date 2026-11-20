using System;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     What one left click does: the prompt shown under the crosshair and the move that carries the click out. The same
///     plan judges the target, shows the prompt and runs on the click, so the prompt always names what the click does.
/// </summary>
/// <param name="Prompt">What the click does ("Pick up card").</param>
/// <param name="Apply">Carries the click out; false when it moved nothing after all.</param>
public sealed record ClickPlan(string Prompt, Func<bool> Apply);

/// <summary>
///     The one place that decides what a left click on an item does (the player's hand router). The interaction system
///     asks it about every target it claims instead of the target's own <see cref="IInteractable.CanInteract" />,
///     <see cref="IInteractable.Prompt" /> and <see cref="IInteractable.Interact" />.
/// </summary>
public interface IClickRouter
{
    /// <summary>Whether the router decides clicks on <paramref name="target" />.</summary>
    bool Claims(IInteractable target);

    /// <summary>
    ///     The click on <paramref name="target" />, aimed at <paramref name="point" /> (world space), or null when such
    ///     a click moves nothing now.
    /// </summary>
    ClickPlan? PlanFor(IInteractable target, Vector3 point, bool shift);

    /// <summary>The click made while nothing usable is aimed at, or null when it does nothing.</summary>
    ClickPlan? PlanForNothing();
}
