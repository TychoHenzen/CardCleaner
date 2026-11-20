using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Tests.Features.CardStacks.Probe;

/// <summary>
///     A rendering probe, not a suite: the e2e driver (<c>tools/e2e</c>) opens <c>PileFrameCostProbe.tscn</c> on a
///     hidden desktop and calls <see cref="Build" /> and <see cref="Measure" />, then reads <see cref="FrameMs" /> and
///     <see cref="GpuMs" />, to see what piles cost to draw. Vsync and the frame cap are off, so the frame time is the
///     real cost and not the display's refresh.
/// </summary>
public partial class PileFrameCostProbe : Node3D
{
    private const float Spacing = 1.4f;

    private double _elapsed;
    private double _gpuSum;
    private int _frames;
    private int _target;

    [Export] public Node3D PileParent { get; set; } = null!;

    /// <summary>The mean frame time of the last finished measurement, in milliseconds.</summary>
    public double FrameMs { get; private set; }

    /// <summary>The mean GPU time of the last finished measurement, in milliseconds.</summary>
    public double GpuMs { get; private set; }

    /// <summary>True once the measurement <see cref="Measure" /> started has finished.</summary>
    public bool Done { get; private set; }

    public override void _Ready()
    {
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
    }

    /// <summary>
    ///     Replaces the piles with <paramref name="piles" /> piles of <paramref name="cards" /> mundane cards each, every
    ///     card with its own look seed, in a row before the camera.
    /// </summary>
    public void Build(int piles, int cards)
    {
        foreach (var child in PileParent.GetChildren())
            child.Free();

        for (var p = 0; p < piles; p++)
        {
            var records = Enumerable.Range(0, cards).Select(i => new StoredCard(new CardSignature(), p * 100000 + i, null));
            var pile = CardBundle.Create(CardBundleKind.Pile, records);
            pile.Freeze = true;
            PileParent.AddChild(pile);
            pile.Position = new Vector3((p - (piles - 1) / 2f) * Spacing, 0f, 0f);
        }
    }

    /// <summary>Starts measuring the next <paramref name="frames" /> frames; <see cref="Done" /> turns true after.</summary>
    public void Measure(int frames)
    {
        _elapsed = 0;
        _gpuSum = 0;
        _frames = 0;
        _target = frames;
        Done = false;
    }

    public override void _Process(double delta)
    {
        if (Done || _target <= 0)
            return;

        _elapsed += delta;
        _gpuSum += RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
        if (++_frames < _target)
            return;

        FrameMs = _elapsed * 1000.0 / _frames;
        GpuMs = _gpuSum / _frames;
        Done = true;
    }
}
