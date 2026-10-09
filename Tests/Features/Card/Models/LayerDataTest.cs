using GdUnit4;
using Godot;
using LayerData = CardCleaner.Scripts.Features.Card.Models.LayerData;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class LayerDataTest
{
    [TestCase]
    public void TestDefaultValues()
    {
        var layer = new LayerData();

        Assertions.AssertThat(layer.Region).IsEqual(new Rect2(0, 0, 1, 1));
        Assertions.AssertBool(layer.RenderOnFront).IsTrue();
        Assertions.AssertBool(layer.RenderOnBack).IsFalse();
    }

    [TestCase]
    public void TestPropertyAssignment()
    {
        var layer = new LayerData();
        var region = new Rect2(0.1f, 0.2f, 0.5f, 0.6f);

        layer.Region = region;
        layer.RenderOnFront = false;
        layer.RenderOnBack = true;

        Assertions.AssertThat(layer.Region).IsEqual(region);
        Assertions.AssertBool(layer.RenderOnFront).IsFalse();
        Assertions.AssertBool(layer.RenderOnBack).IsTrue();
    }
}