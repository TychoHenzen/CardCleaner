using Godot;

namespace CardCleaner.Scripts.Features.Shop.Screens;

/// <summary>
///     The panel a shop screen draws its rows in: a fixed width, centred on the screen, as tall as its rows, with even
///     padding inside. The space around it lets clicks through; the panel itself takes them.
/// </summary>
internal static class ScreenPanel
{
    /// <summary>Adds the panel under <paramref name="host" /> and returns the column its rows go in.</summary>
    public static VBoxContainer AddTo(Node host, float width, int padding)
    {
        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(centre);

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(width, 0)
        };
        centre.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, padding);
        panel.AddChild(margin);

        var column = new VBoxContainer();
        margin.AddChild(column);
        return column;
    }
}
