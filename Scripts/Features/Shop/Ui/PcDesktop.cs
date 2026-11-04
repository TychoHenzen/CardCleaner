using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The desktop on the backoffice PC screen. Only the Stock icon opens an app (the ordering screen in
///     <see cref="StockApp" />); the other icons are shown disabled because their apps are not installed yet.
///     The taskbar Close button raises <see cref="CloseRequested" />. The nodes live in the scene.
/// </summary>
public partial class PcDesktop : Control
{
    private const string NotInstalledTooltip = "Not installed";

    private static readonly string[] NotInstalledIconPaths =
    {
        "Icons/FurnitureIcon",
        "Icons/ExpansionsIcon",
        "Icons/StaffIcon",
        "Icons/BillsIcon"
    };

    private Button _stockIcon = null!;
    private Button _closeButton = null!;

    /// <summary>The ordering app the Stock icon shows (an <see cref="OrderTerminalUi" />).</summary>
    [Export]
    public CanvasLayer? StockApp { get; set; }

    /// <summary>Raised when the player presses Close on the taskbar.</summary>
    public event Action? CloseRequested;

    public override void _Ready()
    {
        _stockIcon = GetNode<Button>("Icons/StockIcon");
        _closeButton = GetNode<Button>("Taskbar/CloseButton");

        foreach (var path in NotInstalledIconPaths)
        {
            var icon = GetNode<Button>(path);
            icon.Disabled = true;
            icon.TooltipText = NotInstalledTooltip;
        }

        _stockIcon.Pressed += OnStockIconPressed;
        _closeButton.Pressed += () => CloseRequested?.Invoke();
    }

    /// <summary>Hides any open app and returns the screen to the desktop.</summary>
    public void ResetToDesktop()
    {
        if (StockApp != null)
            StockApp.Visible = false;
    }

    private void OnStockIconPressed()
    {
        if (StockApp != null)
            StockApp.Visible = true;
    }
}
