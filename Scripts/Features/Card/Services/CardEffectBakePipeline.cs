using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.EffectBaking;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     The pipeline behind <see cref="CardEffectNormalMapCache" />. Once a frame, on the main thread, it hands out the
///     finished maps and reads back the next few art textures; the pure bake runs on the .NET thread pool.
/// </summary>
internal sealed class CardEffectBakePipeline
{
    // A readback costs about 1.5 ms (up to 10 ms) on Forward+, so a burst of new art is spread over frames.
    private const int MaxReadbacksPerFrame = 2;

    private readonly ConditionalWeakTable<Texture2D, Entry> _entries = new();
    private readonly Queue<Entry> _toRead = new();
    private readonly List<Entry> _baking = [];
    private readonly Action _pump;
    private SceneTree? _pumpingOn;

    public CardEffectBakePipeline()
    {
        _pump = Pump;
    }

    public void Request(Texture2D art, Action<ImageTexture> onReady)
    {
        if (_entries.TryGetValue(art, out var entry) && entry.Map != null)
        {
            onReady(entry.Map);
            return;
        }

        if (entry == null)
        {
            entry = new Entry(art);
            _entries.Add(art, entry);
            _toRead.Enqueue(entry);
            StartPumping();
        }

        entry.Waiting.Add(onReady);
    }

    /// <summary>Stops handing out maps; the bakes in flight finish on their own and are dropped.</summary>
    public void Stop()
    {
        _toRead.Clear();
        _baking.Clear();
        if (_pumpingOn == null) return;

        _pumpingOn.ProcessFrame -= _pump;
        _pumpingOn = null;
    }

    private void StartPumping()
    {
        if (_pumpingOn != null || Engine.GetMainLoop() is not SceneTree tree) return;

        _pumpingOn = tree;
        tree.ProcessFrame += _pump;
    }

    private void Pump()
    {
        foreach (var entry in _baking.FindAll(entry => entry.Bake!.IsCompleted))
        {
            _baking.Remove(entry);
            Finish(entry);
        }

        for (var read = 0; read < MaxReadbacksPerFrame && _toRead.Count > 0; read++)
            StartBake(_toRead.Dequeue());

        if (_toRead.Count == 0 && _baking.Count == 0)
            Stop();
    }

    private void StartBake(Entry entry)
    {
        var image = entry.Art.GetImage();
        if (image == null)
        {
            GD.PushError($"Art texture {entry.Art.ResourcePath} has no image; its card shows no rarity effect.");
            _entries.Remove(entry.Art);
            return;
        }

        if (image.IsCompressed())
            image.Decompress();

        // GetData holds every mip level of an imported texture; the baker wants the base level only.
        image.ClearMipmaps();
        image.Convert(Image.Format.Rgba8);
        // The worker gets plain bytes only; every Godot object stays on the main thread.
        var rgba = image.GetData();
        var width = image.GetWidth();
        var height = image.GetHeight();
        entry.Bake = Task.Run(() => CardEffectNormalBaker.Bake(rgba, width, height));
        _baking.Add(entry);
    }

    private void Finish(Entry entry)
    {
        if (!entry.Bake!.IsCompletedSuccessfully)
        {
            GD.PushError($"Baking the bevel map of {entry.Art.ResourcePath} failed: {entry.Bake.Exception}");
            _entries.Remove(entry.Art);
            return;
        }

        var map = entry.Bake.Result;
        entry.Map = ImageTexture.CreateFromImage(
            Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgba8, map.Rgba));
        entry.Bake = null;
        foreach (var onReady in entry.Waiting)
            onReady(entry.Map);
        entry.Waiting.Clear();
    }

    private sealed class Entry(Texture2D art)
    {
        public Texture2D Art { get; } = art;
        public ImageTexture? Map { get; set; }
        public List<Action<ImageTexture>> Waiting { get; } = [];
        public Task<CardEffectNormalMap>? Bake { get; set; }
    }
}
