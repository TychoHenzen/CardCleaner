using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

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
