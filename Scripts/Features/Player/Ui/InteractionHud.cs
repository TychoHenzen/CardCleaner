using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Player.Ui;

/// <summary>
///     The player's aim HUD: a crosshair in the middle of the screen, the click prompt of the object under it
///     ("LMB Pick up card"), and a row of key hints at the bottom from <see cref="IControlHints" />. It hides while
///     anything holds the cursor (a menu, the PC, the payment screen) and never takes mouse input.
/// </summary>
public partial class InteractionHud : Control, IDependent
{
    public const string ClickKey = "LMB";

    private const float DotRadius = 3f;
    private const float RingRadius = 9f;
    private const float RingWidth = 2f;
    private const int PromptOffset = 30;
    private const int HintBottomMargin = 28;
    private const int FontSize = 18;
    private static readonly Color DotColour = new(1f, 1f, 1f, 0.9f);
    private static readonly Color ShadowColour = new(0f, 0f, 0f, 0.55f);
    private static readonly Color RingColour = new(0.98f, 0.82f, 0.35f, 0.95f);

    private IServiceResolver? _services;
    private IControlHints? _hints;
    private ICursorOwner? _cursor;
    private HBoxContainer _prompt = null!;
    private Label _promptText = null!;
    private HBoxContainer _hintRow = null!;
    private bool _hasTarget;
    private bool _built;

    /// <summary>The player's interaction ray, whose target the prompt describes.</summary>
    [Export]
    public InteractionSystem? Interaction { get; set; }

    /// <summary>The prompt text shown now, or empty when nothing is aimed at.</summary>
    internal string PromptText => _prompt.Visible ? _promptText.Text : string.Empty;

    /// <summary>The hints shown now, as "Key: action" lines.</summary>
    internal IReadOnlyList<string> HintLines
    {
        get
        {
            var lines = new List<string>();
            foreach (var chip in _hintRow.GetChildren())
                if (chip.GetMeta(HintMeta, "").AsString() is { Length: > 0 } line)
                    lines.Add(line);
            return lines;
        }
    }

    private const string HintMeta = "hint";

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Build();
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<ICursorOwner>(cursor => _cursor = cursor);
        _services.Get<IControlHints>(hints =>
        {
            _hints = hints;
            _hints.Changed += ShowHints;
            ShowHints();
        });
    }

    public override void _ExitTree()
    {
        if (_hints != null)
            _hints.Changed -= ShowHints;
    }

    public override void _EnterTree()
    {
        // A HUD that leaves and re-enters the tree must listen again.
        if (_hints == null)
            return;
        _hints.Changed -= ShowHints;
        _hints.Changed += ShowHints;
        ShowHints();
    }

    public override void _Process(double delta)
    {
        // The cursor is claimed by whatever has the player's attention: a menu, the PC or the payment screen.
        var shown = _cursor?.HasClaimOtherThan(this) != true;
        if (Visible != shown)
            Visible = shown;
        if (!shown)
            return;

        var target = Interaction?.Target;
        var prompt = target?.Prompt ?? string.Empty;
        _prompt.Visible = prompt.Length > 0;
        if (_promptText.Text != prompt)
            _promptText.Text = prompt;
        if (_hasTarget != (target != null))
        {
            _hasTarget = target != null;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var centre = Size / 2f;
        DrawCircle(centre, DotRadius + 1.5f, ShadowColour);
        DrawCircle(centre, DotRadius, DotColour);
        if (!_hasTarget)
            return;
        DrawArc(centre, RingRadius, 0f, Mathf.Tau, 32, ShadowColour, RingWidth + 2f, true);
        DrawArc(centre, RingRadius, 0f, Mathf.Tau, 32, RingColour, RingWidth, true);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    private void Build()
    {
        if (_built)
            return;
        _built = true;

        _prompt = new HBoxContainer { Name = "Prompt", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _prompt.AddThemeConstantOverride("separation", 8);
        _prompt.SetAnchorsPreset(LayoutPreset.Center);
        _prompt.GrowHorizontal = GrowDirection.Both;
        _prompt.OffsetTop = PromptOffset;
        _prompt.OffsetBottom = PromptOffset;
        _prompt.AddChild(KeyChip(ClickKey));
        _promptText = ShadowedLabel(string.Empty);
        _prompt.AddChild(_promptText);
        AddChild(_prompt);

        _hintRow = new HBoxContainer { Name = "Hints", MouseFilter = MouseFilterEnum.Ignore };
        _hintRow.AddThemeConstantOverride("separation", 24);
        _hintRow.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _hintRow.GrowHorizontal = GrowDirection.Both;
        _hintRow.GrowVertical = GrowDirection.Begin;
        _hintRow.OffsetTop = -HintBottomMargin;
        _hintRow.OffsetBottom = -HintBottomMargin;
        AddChild(_hintRow);
    }

    private void ShowHints()
    {
        Build();
        foreach (var chip in _hintRow.GetChildren())
        {
            _hintRow.RemoveChild(chip);
            chip.QueueFree();
        }

        if (_hints == null)
            return;
        foreach (var hint in _hints.Hints)
        {
            var item = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            item.AddThemeConstantOverride("separation", 8);
            item.SetMeta(HintMeta, $"{hint.Key}: {hint.Action}");
            item.AddChild(KeyChip(hint.Key));
            item.AddChild(ShadowedLabel(hint.Action));
            _hintRow.AddChild(item);
        }
    }

    private static PanelContainer KeyChip(string key)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.95f, 0.95f, 0.97f, 0.95f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 1,
            ContentMarginBottom = 1,
        };
        style.SetCornerRadiusAll(4);
        var chip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        chip.AddThemeStyleboxOverride("panel", style);
        var label = new Label { Text = key, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeColorOverride("font_color", new Color(0.08f, 0.09f, 0.12f));
        label.AddThemeFontSizeOverride("font_size", FontSize - 2);
        chip.AddChild(label);
        return chip;
    }

    private static Label ShadowedLabel(string text)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }
}
