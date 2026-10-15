namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// The cabinet's activate-reset cycle: idle until an activate press starts a session, running while that session
/// plays, and awaiting the reset press once it completes. It also decides whether an activate press may start one.
/// </summary>
internal sealed class CabinetCycle
{
    private enum Phase
    {
        Idle,
        Running,
        AwaitingReset
    }

    private Phase _phase = Phase.Idle;

    // Set by the activate press and read by the reset press that ends that session, so a change of the picked kind in
    // between cannot send the reset to the other screen.
    private MapGenerationType _runningKind;

    internal bool IsRunning => _phase == Phase.Running;

    internal bool IsAwaitingReset => _phase == Phase.AwaitingReset;

    // The map kind toggle works only while the cabinet is idle, so the kind never changes under a live map.
    internal bool CanToggleMapKind => _phase == Phase.Idle;

    internal void Start(MapGenerationType kind)
    {
        _runningKind = kind;
        _phase = Phase.Running;
    }

    internal void Complete() => _phase = Phase.AwaitingReset;

    internal void ReturnToIdle() => _phase = Phase.Idle;

    /// <summary>The reset press: the cabinet is idle again; returns the kind of the session that finished.</summary>
    internal MapGenerationType Reset()
    {
        _phase = Phase.Idle;
        return _runningKind;
    }

    /// <summary>Why an activate press cannot start a <paramref name="kind"/> session, or null when it can.</summary>
    internal static string? StartRefusal(MapGenerationType kind, bool slotsFilled, bool hasIrregularScreen,
        bool hasGridGenerator, bool hasMeshGenerator)
    {
        if (!slotsFilled) return "Button pressed but slots not properly filled!";

        return kind switch
        {
            MapGenerationType.IrregularMesh when !hasIrregularScreen =>
                "IrregularMapScreen not assigned! Cannot generate irregular mesh map.",
            MapGenerationType.RegularGrid when !hasGridGenerator =>
                "No IGridMapGenerator registered! Cannot generate a grid map.",
            MapGenerationType.IrregularMesh when !hasMeshGenerator =>
                "No IMeshMapGenerator registered! Cannot generate an irregular mesh map.",
            _ => null
        };
    }
}
