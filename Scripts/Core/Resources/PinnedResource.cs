using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Core.Resources;

/// <summary>
///     The base of every C#-scripted resource. An instance that a load gave a path stays referenced for the rest of
///     the run, so the resource cache never hands out an instance whose managed side the garbage collector took.
/// </summary>
/// <remarks>
///     Godot 4.7.1 keeps only a weak handle to a C#-scripted resource that nothing but its managed wrapper holds.
///     When the collector takes that wrapper and the cache hands the resource out again before the wrapper's
///     finalizer has run (a scene or a <c>GD.Load</c> asking for the same path), the finalizer finds a released
///     handle and a debug build stops with <c>FATAL: Condition "gchandle.is_released()" is true</c>, exit
///     0xC000001D (godotengine/godot issue 83762). An instance made with <c>new</c> has no path, never enters the
///     cache, and is collected as usual. <c>PinnedResourceTest</c> fails when a C# resource class does not derive
///     from this one.
/// </remarks>
public abstract partial class PinnedResource : Resource
{
    // Touched only from the main thread: deferred calls run there.
    private static readonly List<PinnedResource> Loaded = [];

    protected PinnedResource()
    {
        // The editor reloads the game assembly, and a static list would hold instances of the unloaded one.
        if (Engine.IsEditorHint()) return;
        // A load sets the path after the constructor, so the check waits for the end of the frame. Until then the
        // deferred call holds the instance.
        // ASSUMPTION (Low): nothing loads a resource on a worker thread (ResourceLoader.LoadThreadedRequest), which
        // could set the path after this check ran; PinnedResourceTest fails when Scripts/ starts doing so.
        Callable.From(PinIfLoaded).CallDeferred();
    }

    private void PinIfLoaded()
    {
        if (!string.IsNullOrEmpty(ResourcePath)) Loaded.Add(this);
    }
}
