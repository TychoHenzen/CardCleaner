using GdUnit4;
using Godot;
using LayerData = CardCleaner.Scripts.Core.Data.LayerData;

namespace CardCleaner.Tests.Core;

[TestSuite]
public class LayerDataTest
{
    [TestCase]
    public void TestDefaultValues()
    {
        var layer = new LayerData();

        Assertions.AssertThat(layer.Region).IsEqual(new Vector4(0, 0, 1, 1));
        Assertions.AssertBool(layer.RenderOnFront).IsTrue();
        Assertions.AssertBool(layer.RenderOnBack).IsFalse();
    }

    [TestCase]
    public void TestPropertyAssignment()
    {
        var layer = new LayerData();
        var region = new Vector4(0.1f, 0.2f, 0.5f, 0.6f);

        layer.Region = region;
        layer.RenderOnFront = false;
        layer.RenderOnBack = true;

        Assertions.AssertThat(layer.Region).IsEqual(region);
        Assertions.AssertBool(layer.RenderOnFront).IsFalse();
        Assertions.AssertBool(layer.RenderOnBack).IsTrue();
    }
}