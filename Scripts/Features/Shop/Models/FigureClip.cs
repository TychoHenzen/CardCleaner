namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     What a person in the shop is doing, as one animation. <see cref="Idle" /> and <see cref="Walk" /> loop; the
///     others play once and hand back to the loop.
/// </summary>
public enum FigureClip
{
    Idle,
    Walk,
    PickUp,
    HandOver,
    Refuse,
}
