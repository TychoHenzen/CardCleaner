using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Core.Resources;

/// <summary>
///     The resource cache hands a C#-scripted resource out again after the last holder let go of it. Godot 4.7.1
///     crashed a debug build there when the collector had taken the resource's managed wrapper and its finalizer had
///     not run yet (godotengine/godot issue 83762); a pinned resource keeps its wrapper, so the cache hands out the
///     same live instance. Each case parks the finalizer thread while the cache hands the resource out, so an
///     unpinned wrapper would reach that crash every run instead of now and then.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScriptedResourceRevivalTest
{
    private const string HeldProbePath = "res://Tests/Core/Resources/Fixtures/wrapper_probe.tres";
    private const string LoadedProbePath = "res://Tests/Core/Resources/Fixtures/loaded_probe.tres";
    private const string HolderPath = "res://Tests/Core/Resources/Fixtures/WrapperProbeHolder.tscn";
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(3);

    [TestCase]
    public async Task ASceneThatTakesTheResourceFromTheCacheGetsItsData()
    {
        var wrapper = await LoadAndLetGo(HeldProbePath);
        Node holder;
        FinalizerGate.Park(Hold);
        try
        {
            GC.Collect();
            AssertKeptAlive(wrapper);
            holder = GD.Load<PackedScene>(HolderPath).Instantiate();
        }
        finally
        {
            FinalizerGate.Open();
        }

        RunFinalizers();
        AssertInt(holder.GetMeta("probe").As<WrapperProbe>().Value).IsEqual(42);
        holder.Free();
    }

    [TestCase]
    public async Task LoadingTheResourceAgainGetsItsData()
    {
        var wrapper = await LoadAndLetGo(LoadedProbePath);
        WrapperProbe again;
        FinalizerGate.Park(Hold);
        try
        {
            GC.Collect();
            AssertKeptAlive(wrapper);
            again = GD.Load<WrapperProbe>(LoadedProbePath);
        }
        finally
        {
            FinalizerGate.Open();
        }

        RunFinalizers();
        AssertInt(again.Value).IsEqual(7);
    }

    [TestCase]
    public async Task AResourceMadeInCodeIsNotKept()
    {
        var made = MakeAndLetGo();
        await DeferredCallsRan();
        RunFinalizers();
        AssertBool(made.IsAlive).OverrideFailureMessage("A probe made with new was kept alive.").IsFalse();
    }

    // Loads the probe, lets go of it, and waits until the pin, a deferred call, has run.
    private static async Task<WeakReference> LoadAndLetGo(string path)
    {
        var wrapper = Load(path);
        await DeferredCallsRan();
        return wrapper;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Load(string path)
    {
        var probe = GD.Load<WrapperProbe>(path);
        AssertObject(probe).IsNotNull();
        return new WeakReference(probe);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MakeAndLetGo() => new(new WrapperProbe { Value = 1 });

    private static void AssertKeptAlive(WeakReference wrapper)
    {
        AssertBool(wrapper.IsAlive)
            .OverrideFailureMessage("The loaded probe's wrapper was collected while the resource cache held it.")
            .IsTrue();
    }

    private static void RunFinalizers()
    {
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    // A test resumes inside the process_frame signal, before that frame flushes its deferred calls, so a call queued
    // after the previous frame's last flush is still waiting one frame later. After two frames it has run.
    private static async Task DeferredCallsRan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
