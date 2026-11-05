using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The desktop on the backoffice PC screen. The Stock, Furniture, Bills, Expansions and Staff icons open
///     the apps assigned to <see cref="StockApp" />, <see cref="FurnitureApp" />, <see cref="BillsApp" />,
///     <see cref="ExpansionsApp" /> and <see cref="StaffApp" />. An icon whose app is unassigned stays disabled.
///     The Home button returns to the desktop, and the taskbar Close button raises
///     <see cref="CloseRequested" />. The nodes live in the scene.
/// </summary>
public partial class PcDesktop : Control
{
    private const string NotInstalledTooltip = "Not installed";

    private Button _closeButton = null!;

    /// <summary>
    ///     The app the Stock icon opens. Unassigned keeps that icon disabled.
    /// </summary>
    [Export]
    public Control? StockApp { get; set; }

    /// <summary>
    ///     The app the Furniture icon opens. Unassigned keeps that icon disabled.
    /// </summary>
    [Export]
    public Control? FurnitureApp { get; set; }

    /// <summary>
    ///     The app the Bills icon opens. Unassigned keeps that icon disabled.
    /// </summary>
    [Export]
    public Control? BillsApp { get; set; }

    /// <summary>
    ///     The app the Expansions icon opens. Unassigned keeps that icon disabled.
    /// </summary>
    [Export]
    public Control? ExpansionsApp { get; set; }

    /// <summary>
    ///     The app the Staff icon opens. Unassigned keeps that icon disabled.
    /// </summary>
    [Export]
    public Control? StaffApp { get; set; }

    /// <summary>Raised when the player presses Close on the taskbar.</summary>
    public event Action? CloseRequested;

    public override void _Ready()
    {
        _closeButton = GetNode<Button>("Taskbar/CloseButton");

        foreach (var (path, app) in IconRows())
        {
            var icon = GetNode<Button>(path);
            if (app == null)
            {
                icon.Disabled = true;
                icon.TooltipText = NotInstalledTooltip;
                continue;
            }

            icon.Pressed += () => ShowApp(app);
        }

        _closeButton.Pressed += () => CloseRequested?.Invoke();
        GetNode<Button>("Taskbar/HomeButton").Pressed += ResetToDesktop;
    }

    /// <summary>
    ///     Hides any open app and returns the screen to the desktop.
    /// </summary>
    public void ResetToDesktop()
    {
        foreach (var (_, app) in IconRows())
        {
            if (app != null)
                app.Visible = false;
        }
    }

    // ResetToDesktop reads these rows too, so a new app needs one export and one row here.
    private (string Path, Control? App)[] IconRows() =>
    [
        ("Icons/StockIcon", StockApp),
        ("Icons/BillsIcon", BillsApp),
        ("Icons/FurnitureIcon", FurnitureApp),
        ("Icons/ExpansionsIcon", ExpansionsApp),
        ("Icons/StaffIcon", StaffApp)
    ];

    private void ShowApp(Control app)
    {
        ResetToDesktop();
        app.Visible = true;
    }
}
