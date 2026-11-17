#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TileEditorService
{
    /// <summary>
    /// Groups atlas sources whose textures hold identical pixels, each group sorted by source id. Only sources
    /// that share a texture size with another source are decoded, one at a time: holding every decoded image of
    /// a 576-texture TileSet at once took gigabytes.
    /// </summary>
    public List<List<int>> FindDuplicateSources()
    {
        var duplicateGroups = new List<List<int>>();
        if (!IsTileSetValid())
            return duplicateGroups;

        try
        {
            foreach (var sameSize in SourcesSharingATextureSize())
                duplicateGroups.AddRange(GroupByPixels(sameSize));

            duplicateGroups.Sort((left, right) => left[0].CompareTo(right[0]));
            return duplicateGroups;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileEditorService] Failed to find duplicate sources: {ex.Message}");
            return duplicateGroups;
        }
    }

    private IEnumerable<List<AtlasSourceInfo>> SourcesSharingATextureSize() =>
        GetAvailableAtlasSources()
            .Where(source => source.Source?.Texture != null)
            .GroupBy(source => (source.Source!.Texture.GetWidth(), source.Source.Texture.GetHeight()))
            .Where(group => group.Count() > 1)
            .Select(group => group.OrderBy(source => source.SourceId).ToList());

    private static IEnumerable<List<int>> GroupByPixels(List<AtlasSourceInfo> sources)
    {
        var idsByHash = new Dictionary<string, List<int>>();
        foreach (var source in sources)
        {
            var hash = PixelHash(source.Source!.Texture);
            if (hash == null)
                continue;

            if (!idsByHash.TryGetValue(hash, out var ids))
                idsByHash[hash] = ids = new List<int>();
            ids.Add(source.SourceId);
        }

        return idsByHash.Values.Where(ids => ids.Count > 1);
    }

    private static string? PixelHash(Texture2D texture)
    {
        // Disposing frees the decoded copy now; without it every image waits for the finalizer.
        using var image = texture.GetImage();
        if (image == null)
            return null;

        var data = image.GetData();
        return $"{image.GetFormat()}:{data.Length}:{Convert.ToHexString(SHA256.HashData(data))}";
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
