using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The shop level bar: the current level and a bar that fills toward the next one. It follows
///     <see cref="IShopProgression" /> and redraws whenever its progress changes. It never takes mouse input.
/// </summary>
public partial class LevelBar : HBoxContainer
{
    private IShopProgression? _progression;

    public LevelBar()
    {
    }

    public Label? LevelLabel { get; private set; }

    public ProgressBar? Bar { get; private set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopProgression? Progression
    {
        get => _progression;
        set
        {
            Unsubscribe();
            _progression = value;
            Subscribe();
            Refresh();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        LevelLabel = new Label { Name = "LevelLabel", MouseFilter = MouseFilterEnum.Ignore };
        Bar = new ProgressBar
        {
            Name = "Bar",
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(120, 12),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(LevelLabel);
        AddChild(Bar);
        Refresh();
    }

    public override void _EnterTree()
    {
        Unsubscribe();
        Subscribe();
    }

    public override void _ExitTree() => Unsubscribe();

    private void Subscribe()
    {
        if (_progression != null)
            _progression.ProgressChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (_progression != null)
            _progression.ProgressChanged -= Refresh;
    }

    private void Refresh()
    {
        if (LevelLabel == null || Bar == null)
            return;
        if (_progression == null)
        {
            LevelLabel.Text = "Level -";
            Bar.Value = 0;
            return;
        }

        LevelLabel.Text = _progression.AtMaxLevel ? $"Level {_progression.Level} (max)" : $"Level {_progression.Level}";
        Bar.Value = _progression.Progress;
    }
}
