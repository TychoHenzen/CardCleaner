using CardCleaner.Scripts.Features.Shop.Ui;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>The desktop's Expansions icon: enabled once an app is assigned, it opens that app and Home closes it.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PcDesktopExpansionsTest
{
    private const string DesktopScene = "res://Scenes/Shop/PcDesktop.tscn";
    private const string IconPath = "Icons/ExpansionsIcon";

    [TestCase]
    [TestCategory("Unit")]
    public static void AssignedExpansionsIconIsEnabled()
    {
        var (desktop, _, _) = BuildDesktop();

        var icon = desktop.GetNode<Button>(IconPath);

        AssertThat(icon.Disabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ExpansionsIconShowsExpansionsAppAndHidesOthers()
    {
        var (desktop, stockApp, expansionsApp) = BuildDesktop();
        desktop.GetNode<Button>("Icons/StockIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(stockApp.Visible).IsTrue();

        desktop.GetNode<Button>(IconPath).EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(expansionsApp.Visible).IsTrue();
        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ResetToDesktopHidesTheExpansionsApp()
    {
        var (desktop, _, expansionsApp) = BuildDesktop();
        desktop.GetNode<Button>(IconPath).EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(expansionsApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(expansionsApp.Visible).IsFalse();
    }

    private static (PcDesktop Desktop, Control StockApp, Control ExpansionsApp) BuildDesktop()
    {
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        var stockApp = NewApp("StockApp", desktop);
        var expansionsApp = NewApp("ExpansionsApp", desktop);
        desktop.StockApp = stockApp;
        desktop.ExpansionsApp = expansionsApp;
        AddNode(desktop);
        return (desktop, stockApp, expansionsApp);
    }

    private static Control NewApp(string name, Node parent)
    {
        var app = new Control { Name = name, Visible = false };
        parent.AddChild(app);
        return app;
    }
}
