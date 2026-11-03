using System.Text;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The end-of-day report. Shown when the shop closes; lists the takings, the bills settled and the complaints,
///     and holds the cursor and the player's controls until the player presses Continue.
/// </summary>
public partial class DayReportPanel : Control, IDependent
{
    private const string PlayerGroup = "player";

    private IShopDayService? _dayService;
    private ICursorOwner? _cursor;
    private IServiceResolver? _services;
    private PlayerController? _player;

    public Label? ReportLabel { get; private set; }

    public Button? ContinueButton { get; private set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopDayService? DayService
    {
        get => _dayService;
        set
        {
            Unsubscribe();
            _dayService = value;
            Subscribe();
        }
    }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public ICursorOwner? Cursor
    {
        get => _cursor;
        set => _cursor = value;
    }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildPanel();
        Visible = false;

        if (_services != null)
        {
            _services.Get<IShopDayService>(service => DayService ??= service);
            _services.Get<ICursorOwner>(cursor => Cursor ??= cursor);
        }
    }

    public override void _EnterTree()
    {
        Unsubscribe();
        Subscribe();
    }

    public override void _ExitTree()
    {
        Unsubscribe();
        if (Visible)
            ReleaseReport();
    }

    /// <summary>
    ///     Fills the report with the day's figures, shows it, and takes the cursor and the player's controls.
    /// </summary>
    public void ShowReport(DayReport report)
    {
        if (ReportLabel == null)
            return;

        ReportLabel.Text = BuildText(report);
        Visible = true;
        Cursor?.Claim(this);

        if (IsInsideTree())
        {
            // ASSUMPTION (Low): if the PC order screen is open at closing time, closing that screen re-enables
            // player control while this report is still up. The report's cursor claim keeps the button usable;
            // not handled.
            _player = GetTree().GetFirstNodeInGroup(PlayerGroup) as PlayerController;
            if (_player != null)
                _player.ControlEnabled = false;
        }
    }

    private void BuildPanel()
    {
        var panel = new Panel { MouseFilter = MouseFilterEnum.Stop };
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -240;
        panel.OffsetRight = 240;
        panel.OffsetTop = -180;
        panel.OffsetBottom = 180;
        AddChild(panel);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 16;
        box.OffsetTop = 16;
        box.OffsetRight = -16;
        box.OffsetBottom = -16;
        panel.AddChild(box);

        ReportLabel = new Label { Name = "ReportLabel" };
        box.AddChild(ReportLabel);

        ContinueButton = new Button { Name = "ContinueButton", Text = "Continue" };
        ContinueButton.Pressed += OnContinuePressed;
        box.AddChild(ContinueButton);
    }

    private void OnContinuePressed()
    {
        Visible = false;
        ReleaseReport();
    }

    private void ReleaseReport()
    {
        Cursor?.Release(this);
        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.ControlEnabled = true;
        _player = null;
    }

    private void Subscribe()
    {
        if (_dayService != null)
            _dayService.Closed += OnClosed;
    }

    private void Unsubscribe()
    {
        if (_dayService != null)
            _dayService.Closed -= OnClosed;
    }

    private void OnClosed(DayReport report) => ShowReport(report);

    private static string BuildText(DayReport report)
    {
        var settlement = report.Settlement;
        var text = new StringBuilder();
        text.AppendLine($"Day {report.Day} report");
        text.AppendLine($"Takings: {report.Takings}");
        text.AppendLine($"Debt carried in: {settlement.CarriedIn}");
        foreach (var line in settlement.Lines)
            text.AppendLine($"{line.Name}: {line.Amount}");
        text.AppendLine($"Unpaid (new debt): {settlement.Unpaid}");
        foreach (var (reason, count) in report.Complaints)
            text.AppendLine($"Complaint: {reason} x{count}");
        return text.ToString().TrimEnd();
    }
}
