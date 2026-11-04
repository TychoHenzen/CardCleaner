using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The desktop on the backoffice PC screen. The Stock and Bills icons open
///     the apps assigned to <see cref="StockApp" /> and <see cref="BillsApp" />.
///     The other icons stay disabled until their app slot exists. The Home
///     button returns to the desktop, and the taskbar Close button raises
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
    ///     The app the Bills icon opens. Unassigned keeps that icon disabled.
    /// </summary>
    [Export]
    public Control? BillsApp { get; set; }

    /// <summary>Raised when the player presses Close on the taskbar.</summary>
    public event Action? CloseRequested;

    public override void _Ready()
    {
        _closeButton = GetNode<Button>("Taskbar/CloseButton");

        var icons = new (string Path, Control? App)[]
        {
            ("Icons/StockIcon", StockApp),
            ("Icons/BillsIcon", BillsApp),
            ("Icons/FurnitureIcon", null),
            ("Icons/ExpansionsIcon", null),
            ("Icons/StaffIcon", null)
        };
        foreach (var (path, app) in icons)
        {
            var icon = GetNode<Button>(path);
            if (app == null)
            {
                icon.Disabled = true;
                icon.TooltipText = NotInstalledTooltip;
                continue;
            }

            var target = app;
            icon.Pressed += () => ShowApp(target);
        }

        _closeButton.Pressed += () => CloseRequested?.Invoke();
        GetNode<Button>("Taskbar/HomeButton").Pressed += ResetToDesktop;
    }

    /// <summary>
    ///     Hides any open app and returns the screen to the desktop.
    /// </summary>
    public void ResetToDesktop()
    {
        if (StockApp != null)
            StockApp.Visible = false;
        if (BillsApp != null)
            BillsApp.Visible = false;
    }

    private void ShowApp(Control app)
    {
        ResetToDesktop();
        app.Visible = true;
    }
}
