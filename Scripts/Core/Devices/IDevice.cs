using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Core.Devices;

public interface IDevice
{
    string UniqueId { get; }
    Node3D WorldNode { get; }
    IReadOnlyList<IJack> Jacks { get; }
    IJack? GetJack(string name);
}
