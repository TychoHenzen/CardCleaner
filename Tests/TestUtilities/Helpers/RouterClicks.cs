using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.TestUtilities.Helpers;

/// <summary>
///     Clicks an item the way the player's interaction system does: through the hand router's plan, aimed at a point.
///     Items the router claims (cards, containers, shelf boards, piles and decks, the trash can) offer no click of
///     their own, so a test that clicks one goes through here.
/// </summary>
public static class RouterClicks
{
    /// <summary>
    ///     The prompt a click on <paramref name="target" /> aimed at <paramref name="point" /> shows, or null.
    /// </summary>
    public static string? PromptAt(IClickRouter router, IInteractable target, Vector3 point, bool shift = false) =>
        router.PlanFor(target, point, shift)?.Prompt;

    /// <summary>
    ///     Clicks <paramref name="target" /> aimed at <paramref name="point" />; false when the click moved nothing.
    /// </summary>
    public static bool ClickAt(IClickRouter router, IInteractable target, Vector3 point, bool shift = false) =>
        router.PlanFor(target, point, shift)?.Apply() ?? false;

    /// <summary>Clicks <paramref name="target" /> aimed at its own origin.</summary>
    public static bool Click(IClickRouter router, IInteractable target, bool shift = false) =>
        ClickAt(router, target, target.InteractionBody.GlobalPosition, shift);

    /// <summary>The prompt of a click on <paramref name="target" /> aimed at its own origin, or null.</summary>
    public static string? Prompt(IClickRouter router, IInteractable target, bool shift = false) =>
        PromptAt(router, target, target.InteractionBody.GlobalPosition, shift);
}
