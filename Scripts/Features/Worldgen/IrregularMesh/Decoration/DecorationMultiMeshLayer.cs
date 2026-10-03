using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Decoration;

/// <summary>
/// Owns the per-type MultiMesh nodes that batch-render decorations of one type.
/// </summary>
internal sealed class DecorationMultiMeshLayer
{
    private readonly Dictionary<string, MultiMesh> _multiMeshes = new();
    private readonly Dictionary<string, MultiMeshInstance2D> _multiMeshInstances = new();

    /// <summary>
    /// Renders every decoration of the given type through one MultiMesh attached to the parent.
    /// </summary>
    internal void Render(
        Node2D parent,
        IEnumerable<DecorationData> decorations,
        DecorationType type,
        Mesh mesh,
        Texture2D texture,
        int zIndex)
    {
        var decorationsOfType = decorations.Where(d => d.Type == type).ToList();
        if (decorationsOfType.Count == 0)
            return;

        var key = type.ToString();
        var multiMesh = GetOrCreateMultiMesh(key, mesh);
        WriteInstanceTransforms(multiMesh, decorationsOfType);

        var instance = GetOrCreateInstance(parent, key, zIndex);
        instance.Multimesh = multiMesh;
        instance.Texture = texture;
    }

    /// <summary>
    /// Frees the instance nodes and forgets all MultiMeshes.
    /// </summary>
    internal void Clear()
    {
        foreach (var instance in _multiMeshInstances.Values)
            instance.QueueFree();

        _multiMeshes.Clear();
        _multiMeshInstances.Clear();
    }

    private MultiMesh GetOrCreateMultiMesh(string key, Mesh mesh)
    {
        if (_multiMeshes.TryGetValue(key, out var multiMesh))
            return multiMesh;

        multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            Mesh = mesh
        };
        _multiMeshes[key] = multiMesh;
        return multiMesh;
    }

    private static void WriteInstanceTransforms(MultiMesh multiMesh, List<DecorationData> decorations)
    {
        multiMesh.InstanceCount = decorations.Count;

        for (int i = 0; i < decorations.Count; i++)
        {
            var data = decorations[i];
            var transform = Transform2D.Identity
                .Scaled(data.Scale)
                .Rotated(data.Rotation)
                .Translated(data.Position);

            multiMesh.SetInstanceTransform2D(i, transform);
        }
    }

    private MultiMeshInstance2D GetOrCreateInstance(Node2D parent, string key, int zIndex)
    {
        if (_multiMeshInstances.TryGetValue(key, out var instance))
            return instance;

        instance = new MultiMeshInstance2D
        {
            Name = $"MultiMesh_{key}",
            ZIndex = zIndex
        };
        parent.AddChild(instance);
        _multiMeshInstances[key] = instance;
        return instance;
    }
}
