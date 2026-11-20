namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>
///     What one click does between the hand and a store. <see cref="Put" /> and the takes move records between two
///     stores (<see cref="ItemTransfer.Apply" />). <see cref="PickUp" />, <see cref="SetDown" /> and
///     <see cref="Discard" /> act on a body in the world, so the caller carries them out.
/// </summary>
public enum TransferMove
{
    None,
    Put,
    TakeOne,
    TakeWhole,
    TakeMany,

    /// <summary>The aimed open cardboard box itself goes into the empty hand.</summary>
    PickUp,

    /// <summary>The held open box's last item is set down where the aim meets a level surface.</summary>
    SetDown,

    /// <summary>The held empty cardboard box is thrown away.</summary>
    Discard
}
