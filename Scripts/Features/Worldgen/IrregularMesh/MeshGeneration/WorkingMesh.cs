namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;

using Godot;
using System.Collections.Generic;
using System.Linq;

internal sealed class WorkingMesh
{
    private const int PositionPrecision = 6;

    private readonly Dictionary<MeshPositionKey, int> _positionCache = new();
    private readonly Dictionary<MeshEdge, int> _midpointCache = new();
    private int _nextVertexId;

    public Dictionary<int, Vector2> Vertices { get; } = new();

    public List<WorkingFace> Faces { get; set; } = new();

    public int GetOrCreateVertex(Vector2 position)
    {
        float scale = Mathf.Pow(10, PositionPrecision);
        var key = new MeshPositionKey(
            Mathf.Round(position.X * scale) / scale,
            Mathf.Round(position.Y * scale) / scale);

        if (_positionCache.TryGetValue(key, out int existingId))
        {
            return existingId;
        }

        int id = _nextVertexId++;
        Vertices[id] = position;
        _positionCache[key] = id;
        return id;
    }

    public int GetMidpointVertex(int firstVertexId, int secondVertexId)
    {
        var key = new MeshEdge(firstVertexId, secondVertexId).Normalize();

        if (_midpointCache.TryGetValue(key, out int existingId))
        {
            return existingId;
        }

        var midpoint = (Vertices[firstVertexId] + Vertices[secondVertexId]) / 2f;
        int id = GetOrCreateVertex(midpoint);
        _midpointCache[key] = id;
        return id;
    }

    public int GetCentroidVertex(int[] vertexIds)
    {
        var centroid = new Vector2(
            vertexIds.Average(id => Vertices[id].X),
            vertexIds.Average(id => Vertices[id].Y));
        return GetOrCreateVertex(centroid);
    }
}
