using System;

namespace CardCleaner.Scripts.Core.Devices;

/// <summary>
/// Input-specific jack interface. Devices use this to receive data.
/// </summary>
public interface IInputJack<T> : IJack
{
    /// <summary>
    /// Event fired when data is received from a connected output.
    /// Devices subscribe to this to handle incoming data.
    /// </summary>
    event Action<T>? DataReceived;

    /// <summary>
    /// Called by connected output jack to deliver data.
    /// </summary>
    void Receive(T data);
}
