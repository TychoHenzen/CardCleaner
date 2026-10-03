#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    public List<List<int>> FindDuplicateSources()
    {
        var duplicateGroups = new List<List<int>>();
        if (!IsTileSetValid())
            return duplicateGroups;

        try
        {
            var sourceImageData = CollectSourceImageData();
            var processed = new HashSet<int>();
            foreach (var (sourceId, imageData) in sourceImageData)
            {
                if (processed.Contains(sourceId))
                    continue;

                var group = FindMatchingSources(
                    sourceId,
                    imageData,
                    sourceImageData,
                    processed);
                processed.Add(sourceId);
                if (group.Count <= 1)
                    continue;

                group.Sort();
                duplicateGroups.Add(group);
            }

            return duplicateGroups;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to find duplicate sources: {ex.Message}");
            return duplicateGroups;
        }
    }

    private Dictionary<int, byte[]> CollectSourceImageData()
    {
        var imageDataBySource = new Dictionary<int, byte[]>();
        foreach (var sourceInfo in GetAvailableAtlasSources())
        {
            if (sourceInfo.Source?.Texture == null)
                continue;

            var image = sourceInfo.Source.Texture.GetImage();
            if (image != null)
                imageDataBySource[sourceInfo.SourceId] = image.GetData();
        }

        return imageDataBySource;
    }

    private static List<int> FindMatchingSources(
        int sourceId,
        byte[] sourceData,
        Dictionary<int, byte[]> imageDataBySource,
        HashSet<int> processed)
    {
        var group = new List<int> { sourceId };
        foreach (var (candidateId, candidateData) in imageDataBySource)
        {
            if (sourceId == candidateId || processed.Contains(candidateId))
                continue;

            if (sourceData.Length != candidateData.Length
                || !sourceData.SequenceEqual(candidateData))
            {
                continue;
            }

            group.Add(candidateId);
            processed.Add(candidateId);
        }

        return group;
    }

    public int RemoveDuplicateSources()
    {
        if (!IsTileSetValid())
            return 0;

        var duplicateGroups = FindDuplicateSources();
        if (duplicateGroups.Count == 0)
            return 0;

        var removedCount = 0;
        try
        {
            foreach (var group in duplicateGroups)
                removedCount += RemoveDuplicateGroup(group);

            var saveResult = ResourceSaver.Save(_tileSet!, TilesetPath);
            if (saveResult != Error.Ok)
            {
                GD.PrintErr(
                    $"[TileEditorService] Failed to save TileSet after removing duplicates: "
                    + $"{saveResult}");
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to remove duplicate sources: {ex.Message}");
            return removedCount;
        }
    }

    private int RemoveDuplicateGroup(List<int> group)
    {
        var keepId = group[0];
        var removedCount = 0;
        foreach (var removeId in group.Skip(1))
        {
            foreach (var tile in _tiles.Values.Where(tile => tile.SourceId == removeId))
                tile.SourceId = keepId;

            _tileSet!.RemoveSource(removeId);
            removedCount++;
            GD.Print(
                $"[TileEditorService] Removed duplicate source {removeId} "
                + $"(kept {keepId})");
        }

        return removedCount;
    }

    public bool AreSourceIdsContiguous()
    {
        if (!IsTileSetValid())
            return true;

        try
        {
            return AreContiguous(GetSourceIds());
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to check source IDs: {ex.Message}");
            return true;
        }
    }

    public Dictionary<int, int> CompactAtlasSourceIds()
    {
        var mapping = new Dictionary<int, int>();
        if (!IsTileSetValid())
        {
            GD.PrintErr("[TileEditorService] Cannot compact: TileSet not loaded");
            return mapping;
        }

        try
        {
            var sourceIds = GetSourceIds();
            if (sourceIds.Count == 0 || AreContiguous(sourceIds))
            {
                if (sourceIds.Count > 0)
                    GD.Print("[TileEditorService] Source IDs already contiguous");
                return mapping;
            }

            var sources = CaptureSources(sourceIds);
            RemoveSources(sourceIds);
            ReaddSources(sources, mapping);
            UpdateTileSourceIds(mapping);
            SaveCompactedTileSet(sources.Count, mapping.Count);
            return mapping;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to compact atlas source IDs: {ex.Message}");
            return mapping;
        }
    }

    private List<int> GetSourceIds()
    {
        var sourceIds = new List<int>();
        for (var index = 0; index < _tileSet!.GetSourceCount(); index++)
            sourceIds.Add(_tileSet.GetSourceId(index));
        sourceIds.Sort();
        return sourceIds;
    }

    private static bool AreContiguous(List<int> sourceIds)
    {
        for (var index = 0; index < sourceIds.Count; index++)
        {
            if (sourceIds[index] != index)
                return false;
        }

        return true;
    }

    private List<CompactedSource> CaptureSources(List<int> sourceIds)
    {
        var sources = new List<CompactedSource>();
        foreach (var oldId in sourceIds)
        {
            var source = _tileSet!.GetSource(oldId) as TileSetAtlasSource;
            if (source != null)
                sources.Add(new CompactedSource(oldId, source));
        }

        return sources;
    }

    private void RemoveSources(List<int> sourceIds)
    {
        foreach (var sourceId in sourceIds)
            _tileSet!.RemoveSource(sourceId);
    }

    private void ReaddSources(
        List<CompactedSource> sources,
        Dictionary<int, int> mapping)
    {
        for (var newId = 0; newId < sources.Count; newId++)
        {
            var source = sources[newId];
            _tileSet!.AddSource(source.Source, newId);
            mapping[source.OldId] = newId;
        }
    }

    private void UpdateTileSourceIds(Dictionary<int, int> mapping)
    {
        foreach (var tile in _tiles.Values)
        {
            if (mapping.TryGetValue(tile.SourceId, out var newId))
                tile.SourceId = newId;
        }
    }

    private void SaveCompactedTileSet(int sourceCount, int mappingCount)
    {
        var saveResult = ResourceSaver.Save(_tileSet, TilesetPath);
        if (saveResult != Error.Ok)
        {
            GD.PrintErr(
                $"[TileEditorService] Failed to save TileSet after compaction: {saveResult}");
            return;
        }

        GD.Print(
            $"[TileEditorService] Compacted {sourceCount} sources, "
            + $"remapped {mappingCount} IDs");
    }

    private sealed record CompactedSource(int OldId, TileSetAtlasSource Source);
}
#endif
