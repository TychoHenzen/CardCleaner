using System.Linq;
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
    private const float PanelWidth = 420f;
    private const int Padding = 20;
    private const int RowGap = 10;
    private const int ColumnGap = 32;
    private const int TitleFontSize = 28;

    private IShopDayService? _dayService;
    private ICursorOwner? _cursor;
    private IServiceResolver? _services;
    private PlayerController? _player;

    public Label? TitleLabel { get; private set; }

    /// <summary>Two columns: a label and its signed amount per row (<see cref="DayReportSheet.Figures" />).</summary>
    public GridContainer? FiguresGrid { get; private set; }

    /// <summary>One label per complaint line (<see cref="DayReportSheet.Complaints" />).</summary>
    public VBoxContainer? ComplaintsBox { get; private set; }

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
    ///     Fills the report with the day's figures, closes any open modal screen, shows the report, and takes the
    ///     cursor and the player's controls.
    /// </summary>
    public void ShowReport(DayReport report)
    {
        if (TitleLabel == null)
            return;

        Fill(report);
        if (IsInsideTree())
            CloseModalScreens();

        Visible = true;
        Cursor?.Claim(this);

        if (IsInsideTree())
        {
            _player = GetTree().GetFirstNodeInGroup(PlayerGroup) as PlayerController;
            if (_player != null)
                _player.ControlEnabled = false;
        }
    }

    // Closing time can come while the PC or the payment screen is open. The PC overlay draws above this report and
    // takes every click, and each screen gives the player's controls back as it closes, so they close first.
    private void CloseModalScreens()
    {
        foreach (var screen in GetTree().GetNodesInGroup(IModalScreen.GroupName).OfType<IModalScreen>())
            screen.Close();
    }

    // The panel sizes itself to its rows, centred on the screen, so a long bill list or many complaints never
    // spill out of it.
    private void BuildPanel()
    {
        var box = ScreenPanel.AddTo(this, PanelWidth, Padding);
        box.AddThemeConstantOverride("separation", RowGap);

        TitleLabel = new Label { Name = "Title", HorizontalAlignment = HorizontalAlignment.Center };
        TitleLabel.AddThemeFontSizeOverride("font_size", TitleFontSize);
        box.AddChild(TitleLabel);
        box.AddChild(new HSeparator());

        FiguresGrid = new GridContainer { Name = "Figures", Columns = 2 };
        FiguresGrid.AddThemeConstantOverride("h_separation", ColumnGap);
        box.AddChild(FiguresGrid);
        box.AddChild(new HSeparator());

        box.AddChild(new Label { Text = "Customer complaints" });
        ComplaintsBox = new VBoxContainer { Name = "Complaints" };
        box.AddChild(ComplaintsBox);

        ContinueButton = new Button { Name = "ContinueButton", Text = "Continue" };
        ContinueButton.Pressed += OnContinuePressed;
        box.AddChild(ContinueButton);
    }

    private void Fill(DayReport report)
    {
        TitleLabel!.Text = DayReportSheet.Title(report);
        Clear(FiguresGrid!);
        foreach (var (label, value) in DayReportSheet.Figures(report))
        {
            FiguresGrid!.AddChild(new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            FiguresGrid.AddChild(new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Right });
        }

        Clear(ComplaintsBox!);
        foreach (var line in DayReportSheet.Complaints(report))
            ComplaintsBox!.AddChild(new Label { Text = line });
    }

    // The rows are plain labels that nothing else holds or listens to, so they go at once, not at the end of the
    // frame: no old row waits outside the tree after a second report.
    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
            child.Free();
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
}
