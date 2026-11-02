using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Controllers;

/// <summary>
///     The options screen. Every control change saves the whole options value and applies it. The mouse part also goes
///     to the scene's <see cref="IGameSettings" /> when one is registered. Loading the stored values into the controls
///     neither saves nor applies anything.
/// </summary>
public partial class OptionsMenuController : Control, IDependent
{
    [Signal]
    public delegate void ClosedEventHandler();

    // The node paths name the controls inside OptionsMenu.tscn; the scene contract test pins them.
    [Export] public NodePath SensitivityPath = new("Layout/SensitivitySlider");
    [Export] public NodePath InvertYPath = new("Layout/InvertYCheck");
    [Export] public NodePath MasterPath = new("Layout/MasterSlider");
    [Export] public NodePath MusicPath = new("Layout/MusicSlider");
    [Export] public NodePath SfxPath = new("Layout/SfxSlider");
    [Export] public NodePath FullscreenPath = new("Layout/FullscreenCheck");
    [Export] public NodePath ResolutionPath = new("Layout/ResolutionOption");
    [Export] public NodePath VsyncPath = new("Layout/VsyncCheck");
    [Export] public NodePath BackPath = new("Layout/BackButton");

    private IServiceResolver? _services;
    private OptionsStore? _store;
    private OptionsApplier? _applier;
    private bool _loading;

    public void Inject(IServiceResolver services)
    {
        _services = services;
    }

    public override void _Ready()
    {
        _store = _services!.Get<OptionsStore>();
        _applier = _services.Get<OptionsApplier>();

        var resolution = GetNode<OptionButton>(ResolutionPath);
        resolution.Clear();
        foreach (var (width, height) in GameOptions.Resolutions)
            resolution.AddItem($"{width}x{height}");

        GetNode<HSlider>(SensitivityPath).ValueChanged += _ => OnControlChanged();
        GetNode<CheckButton>(InvertYPath).Toggled += _ => OnControlChanged();
        GetNode<HSlider>(MasterPath).ValueChanged += _ => OnControlChanged();
        GetNode<HSlider>(MusicPath).ValueChanged += _ => OnControlChanged();
        GetNode<HSlider>(SfxPath).ValueChanged += _ => OnControlChanged();
        GetNode<CheckButton>(FullscreenPath).Toggled += _ => OnControlChanged();
        resolution.ItemSelected += _ => OnControlChanged();
        GetNode<CheckButton>(VsyncPath).Toggled += _ => OnControlChanged();
        GetNode<Button>(BackPath).Pressed += () => EmitSignal(SignalName.Closed);

        ShowOptions(_store.Load());
    }

    private void ShowOptions(GameOptions options)
    {
        _loading = true;
        GetNode<HSlider>(SensitivityPath).Value = options.MouseSensitivity;
        GetNode<CheckButton>(InvertYPath).ButtonPressed = options.InvertY;
        GetNode<HSlider>(MasterPath).Value = options.MasterVolume;
        GetNode<HSlider>(MusicPath).Value = options.MusicVolume;
        GetNode<HSlider>(SfxPath).Value = options.SfxVolume;
        GetNode<CheckButton>(FullscreenPath).ButtonPressed = options.Fullscreen;
        GetNode<OptionButton>(ResolutionPath).Select(options.ResolutionIndex);
        GetNode<CheckButton>(VsyncPath).ButtonPressed = options.Vsync;
        _loading = false;
    }

    private void OnControlChanged()
    {
        if (_loading || _store == null || _applier == null)
            return;

        var options = ReadControls();
        _store.Save(options);
        _applier.Apply(options, GetWindow());
        if (_services!.Has<IGameSettings>())
            _applier.ApplyMouse(options, _services.Get<IGameSettings>());
    }

    private GameOptions ReadControls()
    {
        return new GameOptions(
            (float)GetNode<HSlider>(SensitivityPath).Value,
            GetNode<CheckButton>(InvertYPath).ButtonPressed,
            (float)GetNode<HSlider>(MasterPath).Value,
            (float)GetNode<HSlider>(MusicPath).Value,
            (float)GetNode<HSlider>(SfxPath).Value,
            GetNode<CheckButton>(FullscreenPath).ButtonPressed,
            GetNode<OptionButton>(ResolutionPath).Selected,
            GetNode<CheckButton>(VsyncPath).ButtonPressed).Clamped();
    }
}
