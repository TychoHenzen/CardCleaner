namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Something a carried box or pack can be unpacked onto, such as a product shelf or a storage rack. The
///     <see cref="ContainerHolder" /> asks the target it finds in front of the hand before it opens a container loose.
/// </summary>
public interface IUnpackTarget
{
    /// <summary>
    ///     Unpacks <paramref name="container" /> onto this target. Returns false when the target does not take this
    ///     kind of container, in which case the holder opens it loose.
    /// </summary>
    bool TryUnpack(CardContainer container);
}
