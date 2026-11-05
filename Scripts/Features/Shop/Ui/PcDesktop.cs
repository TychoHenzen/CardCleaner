using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The desktop on the backoffice PC screen. The Stock, Furniture, Bills and Expansions icons open
///     the apps assigned to <see cref="StockApp" />, <see cref="FurnitureApp" />, <see cref="BillsApp" />
///     and <see cref="ExpansionsApp" />.
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

    // The one place each app is named next to its icon. A new app is one
    // export and one row here; ResetToDesktop reads the same rows.
    private (string Path, Control? App)[] IconRows() =>
    [
        ("Icons/StockIcon", StockApp),
        ("Icons/BillsIcon", BillsApp),
        ("Icons/FurnitureIcon", FurnitureApp),
        ("Icons/ExpansionsIcon", ExpansionsApp),
        ("Icons/StaffIcon", null)
    ];

    private void ShowApp(Control app)
    {
        ResetToDesktop();
        app.Visible = true;
    }
}
