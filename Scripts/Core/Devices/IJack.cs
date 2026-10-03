namespace CardCleaner.Scripts.Core.Devices;

public interface IJack
{
    string Name { get; }
    JackDirection Direction { get; }
    ICable? ConnectedCable { get; }
    bool IsCompatibleWith(IJack other);

    /// <summary>Establishes a logical connection to another compatible jack.</summary>
    bool TryConnect(IJack other);

    /// <summary>
    /// Tears down the logical connection to the connected jack.
    /// </summary>
    void DisconnectLogical();

    /// <summary>
    /// Called by Cable to register itself with this jack.
    /// </summary>
    void SetCable(ICable? cable);
}
