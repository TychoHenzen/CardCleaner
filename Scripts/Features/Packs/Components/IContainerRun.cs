namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     A container's opening as it plays out over real time: loose on the floor (<see cref="ContainerOpening" />) or a
///     booster in the player's hand (<see cref="BoosterReveal" />). The container advances it every frame and is freed
///     once it is over.
/// </summary>
internal interface IContainerRun
{
    /// <summary>Items that have left the container so far.</summary>
    int Released { get; }

    /// <summary>Items still inside the container.</summary>
    int Pending { get; }

    bool IsOver { get; }

    /// <summary>Advances the opening by <paramref name="delta" /> seconds. True once it is over.</summary>
    bool Advance(float delta);

    /// <summary>Ends the opening at once, as if its time had run out.</summary>
    void Finish();

    /// <summary>Lets go of what has come out so far, for a container that leaves the tree before it is done.</summary>
    void Abandon();
}
