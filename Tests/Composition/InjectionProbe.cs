using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.Composition;

/// <summary>
///     A test node that records, into a log it may share with other probes, when it enters the tree, is injected and
///     becomes ready. Its _Ready wiring uses the stored resolver, as the IDependent contract asks.
/// </summary>
public partial class InjectionProbe : Node, IDependent
{
    public InjectionProbe()
        : this("probe", new List<string>())
    {
    }

    public InjectionProbe(string label, List<string> log)
    {
        Label = label;
        Log = log;
    }

    public string Label { get; }
    public List<string> Log { get; }
    public IServiceResolver? Services { get; private set; }
    public int WiringRuns { get; private set; }

    public void Inject(IServiceResolver services)
    {
        Services = services;
        Log.Add($"{Label}.inject");
    }

    public override void _EnterTree() => Log.Add($"{Label}.enter");

    public override void _Ready()
    {
        Log.Add($"{Label}.ready");
        // The wiring reads through the stored resolver; a null one would throw here and fail the test.
        Services!.Has<ILog>();
        WiringRuns++;
    }
}
