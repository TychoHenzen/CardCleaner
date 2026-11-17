using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Core.Resources;

/// <summary>
///     Base of every C# resource: once a load gives an instance a path, it stays referenced for the rest of the run,
///     so the resource cache never hands out one whose wrapper was collected (godotengine/godot issue 83762).
/// </summary>
public abstract partial class PinnedResource : Resource
{
    // Touched only from the main thread: deferred calls run there.
    private static readonly List<PinnedResource> Loaded = [];

    protected PinnedResource()
    {
        // The editor reloads the game assembly, and a static list would hold instances of the unloaded one.
        if (Engine.IsEditorHint()) return;
        // A load sets the path after the constructor, so the check waits for the end of the frame. Until then the
        // deferred call holds the instance. ASSUMPTION (Low): no load runs on a worker thread, where the path could
        // come after the check; PinnedResourceTest fails when Scripts/ starts one.
        Callable.From(PinIfLoaded).CallDeferred();
    }

    private void PinIfLoaded()
    {
        if (!string.IsNullOrEmpty(ResourcePath)) Loaded.Add(this);
    }
}
