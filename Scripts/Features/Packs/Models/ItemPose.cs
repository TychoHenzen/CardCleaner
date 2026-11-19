namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     How an item lies in a box. <c>Flat</c> is the item as it is built (a card or booster lying on its face, a
///     booster box standing on its base); <c>Turned</c> is that turned a quarter about the vertical. <c>OnEnd</c>
///     stands the item on its short end, and <c>OnEndTurned</c> is that turned a quarter about the vertical.
/// </summary>
public enum ItemPose
{
    Flat,
    Turned,
    OnEnd,
    OnEndTurned
}
