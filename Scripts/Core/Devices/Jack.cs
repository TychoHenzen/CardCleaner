using System;

namespace CardCleaner.Scripts.Core.Devices;

/// <summary>
/// Concrete implementation of a typed jack.
/// Handles connection logic and event forwarding between output and input jacks.
/// </summary>
public class Jack<T> : IJack<T>
{
    private IJack<T>? _connectedJack;

    public string Name { get; }
    public JackDirection Direction { get; }
    public ICable? ConnectedCable { get; private set; }

    public event Action<T>? DataEmitted;
    public event Action<T>? DataReceived;

    public Jack(string name, JackDirection direction)
    {
        Name = name;
        Direction = direction;
    }

    public bool IsCompatibleWith(IJack other)
    {
        return other is IJack<T> && other.Direction != Direction;
    }

    public bool TryConnect(IJack other)
    {
        if (other is not IJack<T> typedOther)
            return false;

        _connectedJack = typedOther;

        if (Direction == JackDirection.Output)
        {
            // Output: subscribe so Emit() forwards to input's Receive()
            DataEmitted += typedOther.Receive;
        }

        return true;
    }

    public void DisconnectLogical()
    {
        if (_connectedJack != null && Direction == JackDirection.Output)
        {
            DataEmitted -= _connectedJack.Receive;
        }
        _connectedJack = null;
    }

    public void SetCable(ICable? cable) => ConnectedCable = cable;

    /// <summary>
    /// Called by the owning device to send data out.
    /// Only meaningful for output jacks.
    /// </summary>
    public void Emit(T data)
    {
        DataEmitted?.Invoke(data);
    }

    /// <summary>
    /// Called when data arrives from a connected output.
    /// Only meaningful for input jacks.
    /// </summary>
    public void Receive(T data)
    {
        DataReceived?.Invoke(data);
    }
}
