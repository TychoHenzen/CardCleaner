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

/// <summary>
/// Output-specific jack interface. Devices use this to emit data.
/// </summary>
public interface IOutputJack<T> : IJack
{
    /// <summary>
    /// Event fired when data is emitted. Connected input jacks subscribe to this.
    /// </summary>
    event Action<T>? DataEmitted;

    /// <summary>
    /// Called by the owning device to send data to connected inputs.
    /// </summary>
    void Emit(T data);
}

/// <summary>
/// Full jack interface combining input and output capabilities.
/// Concrete implementations use this; devices expose the specific directional interface.
/// </summary>
public interface IJack<T> : IInputJack<T>, IOutputJack<T>
{
}
