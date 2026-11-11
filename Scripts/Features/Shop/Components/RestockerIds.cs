using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Hands out the planner's integer ids so that a node keeps the same id across scans. The job's blocked list
///     holds ids, so an id that moved when another node left the tree would block the wrong pair. Ids count up in the
///     order nodes are first seen (scene-tree order on the first scan).
/// </summary>
internal sealed class RestockerIds
{
    private readonly Dictionary<ulong, int> _ids = new();

    public int For(GodotObject node)
    {
        var key = node.GetInstanceId();
        if (!_ids.TryGetValue(key, out var id))
        {
            id = _ids.Count;
            _ids[key] = id;
        }

        return id;
    }
}
