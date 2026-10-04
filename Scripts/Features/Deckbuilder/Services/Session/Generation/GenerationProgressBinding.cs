using System;
using System.Threading;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// Hosts a GodotProgress reporter under the owner node for one generation
/// and forwards its main-thread updates.
/// </summary>
internal sealed class GenerationProgressBinding : IDisposable
{
    private readonly GodotProgress _progress;
    private readonly GodotProgress.ProgressUpdatedEventHandler _handler;

    internal GenerationProgressBinding(Node owner, Action<float> onProgress, CancellationToken cancellationToken)
    {
        _progress = new GodotProgress(cancellationToken);
        owner.AddChild(_progress);
        _handler = value => onProgress(value);
        _progress.ProgressUpdated += _handler;
    }

    internal IProgress<float> Progress => _progress;

    void IDisposable.Dispose()
    {
        Detach();
    }

    internal void Detach()
    {
        _progress.ProgressUpdated -= _handler;
        if (!GodotObject.IsInstanceValid(_progress))
            return;

        try
        {
            _progress.QueueFree();
        }
        catch (ObjectDisposedException)
        {
            // The node was disposed between the validity check and the free call.
        }
    }
}
