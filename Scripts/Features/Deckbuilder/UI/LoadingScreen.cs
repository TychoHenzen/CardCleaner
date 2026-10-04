using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.UI;

/// <summary>Displays map generation progress and hides when generation completes.</summary>
internal sealed partial class LoadingScreen : Control
{
    private ProgressBar? _progressBar;
    private IGameSessionService? _sessionService;

    public override void _Ready()
    {
        // Initially hidden until generation starts
        Visible = false;

        // Get progress bar child node
        _progressBar = GetNodeOrNull<ProgressBar>("ProgressBar");
        if (_progressBar == null)
        {
            GD.PushWarning("LoadingScreen: No ProgressBar child found. Progress won't be displayed.");
        }
        else
        {
            _progressBar.MinValue = 0;
            _progressBar.MaxValue = 100;
            _progressBar.Value = 0;
        }

        // Subscribe to game session events
        ServiceLocator.Get<IGameSessionService>(svc =>
        {
            _sessionService = svc;
            _sessionService.StateChanged += OnStateChanged;
            _sessionService.ProgressUpdated += OnProgressUpdated;
        });
    }

    public override void _ExitTree()
    {
        // Unsubscribe from events to prevent memory leaks
        if (_sessionService != null)
        {
            _sessionService.StateChanged -= OnStateChanged;
            _sessionService.ProgressUpdated -= OnProgressUpdated;
        }
    }

    private void OnStateChanged(SessionState state)
    {
        // Show loading screen only during map generation
        Visible = state == SessionState.GeneratingMap;

        // Reset progress when generation starts
        if (state == SessionState.GeneratingMap && _progressBar != null)
        {
            _progressBar.Value = 0;
        }
    }

    private void OnProgressUpdated(float value)
    {
        // Update progress bar (value is 0.0-1.0, convert to 0-100)
        if (_progressBar != null)
        {
            _progressBar.Value = value * 100;
        }
    }
}
