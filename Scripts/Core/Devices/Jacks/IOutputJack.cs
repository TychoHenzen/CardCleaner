using System;

namespace CardCleaner.Scripts.Core.Devices;

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
