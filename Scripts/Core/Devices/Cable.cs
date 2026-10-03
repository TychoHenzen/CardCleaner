using System;
using Godot;

namespace CardCleaner.Scripts.Core.Devices;

internal partial class Cable : Node3D, ICable
{
    private const float CableRadius = 0.02f;
    private static readonly Color CableColor = new(0.2f, 0.2f, 0.2f);

    private MeshInstance3D? _meshInstance;
    private IDevice? _sourceDevice;
    private IDevice? _destinationDevice;

    public IJack SourceJack { get; private set; }
    public IJack DestinationJack { get; private set; }

    public Cable(IJack source, IJack destination, IDevice sourceDevice, IDevice destinationDevice)
    {
        SourceJack = source;
        DestinationJack = destination;
        _sourceDevice = sourceDevice;
        _destinationDevice = destinationDevice;
    }

    public void Connect()
    {
        // Auto-swap if connected backwards
        if (SourceJack.Direction == JackDirection.Input && DestinationJack.Direction == JackDirection.Output)
        {
            (SourceJack, DestinationJack) = (DestinationJack, SourceJack);
            (_sourceDevice, _destinationDevice) = (_destinationDevice, _sourceDevice);
        }

        // Validate directions after potential swap
        if (SourceJack.Direction != JackDirection.Output || DestinationJack.Direction != JackDirection.Input)
            throw new InvalidOperationException("Cable must connect an output jack to an input jack");

        if (!SourceJack.TryConnect(DestinationJack))
            throw new InvalidOperationException("Jack types are not compatible");

        SourceJack.SetCable(this);
        DestinationJack.SetCable(this);

        CreateVisual();
    }

    public void Disconnect()
    {
        SourceJack.DisconnectLogical();
        SourceJack.SetCable(null);
        DestinationJack.SetCable(null);

        _meshInstance?.QueueFree();
        _meshInstance = null;
    }

    /// <summary>
    /// Updates the cable visual. Call this if devices have moved.
    /// </summary>
    public void UpdateVisual()
    {
        if (_meshInstance == null) return;

        var (startPos, endPos) = GetEndpoints();
        if (!startPos.HasValue || !endPos.HasValue) return;

        UpdateCylinderTransform(_meshInstance, startPos.Value, endPos.Value);
    }

    private void CreateVisual()
    {
        var (startPos, endPos) = GetEndpoints();
        if (!startPos.HasValue || !endPos.HasValue)
        {
            GD.PrintErr("Cable: Could not find jack marker nodes for visual");
            return;
        }

        // Create cylinder mesh
        var cylinder = new CylinderMesh
        {
            TopRadius = CableRadius,
            BottomRadius = CableRadius,
            Height = 1.0f // Will be scaled
        };

        // Create material
        var material = new StandardMaterial3D
        {
            AlbedoColor = CableColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        // Create mesh instance
        _meshInstance = new MeshInstance3D
        {
            Mesh = cylinder,
            MaterialOverride = material
        };

        AddChild(_meshInstance);
        UpdateCylinderTransform(_meshInstance, startPos.Value, endPos.Value);
    }

    private CableEndpoints GetEndpoints()
    {
        var startMarker = FindJackMarker(_sourceDevice, SourceJack.Name);
        var endMarker = FindJackMarker(_destinationDevice, DestinationJack.Name);

        return new CableEndpoints(startMarker?.GlobalPosition, endMarker?.GlobalPosition);
    }

    private static Node3D? FindJackMarker(IDevice? device, string jackName)
    {
        if (device?.WorldNode == null) return null;

        // Look for a child node named after the jack
        return device.WorldNode.GetNodeOrNull<Node3D>(jackName);
    }

    private static void UpdateCylinderTransform(MeshInstance3D mesh, Vector3 start, Vector3 end)
    {
        var direction = end - start;
        var length = direction.Length();

        if (length < 0.001f) return;

        // Position at midpoint
        mesh.GlobalPosition = (start + end) / 2f;

        // Scale to match distance
        mesh.Scale = new Vector3(1, length, 1);

        // Rotate to point from start to end
        var up = Vector3.Up;
        if (Mathf.Abs(direction.Normalized().Dot(up)) > 0.99f)
            up = Vector3.Forward;

        mesh.LookAt(end, up);
        mesh.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
    }
}
