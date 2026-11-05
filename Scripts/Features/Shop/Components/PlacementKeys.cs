using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>The keys that drive a placement session, bound to <see cref="PlacementActions" />.</summary>
internal sealed class PlacementKeys
{
    private readonly Node _owner;
    private readonly PlacementActions _actions;
    private IInputService? _input;

    public PlacementKeys(Node owner, PlacementActions actions) => (_owner, _actions) = (owner, actions);

    public void Bind(IInputService input)
    {
        _input = input;
        input.RegisterAction(_owner, "furniture_rotate", Key.T, _actions.Rotate);
        input.RegisterAction(_owner, "furniture_confirm", Key.E, _actions.Confirm);
        input.RegisterAction(_owner, "furniture_cancel", Key.C, _actions.Cancel);
        input.RegisterAction(_owner, "furniture_pickup", Key.G, _actions.PickUp);
    }

    public void Unbind() => _input?.UnregisterAllActions(_owner);
}
