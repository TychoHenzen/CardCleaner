using System.Linq;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties.GradientSuites;

[TestSuite]
[RequireGodotRuntime]
public class GradientPropertyRevertTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void NoiseGradientRevertsOnlyExportedScalarProperties()
    {
        var gradient = new NoiseGradient();

        AssertBool(HasExportedProperty(gradient, nameof(NoiseGradient.NoiseScale), Variant.Type.Float)).IsTrue();
        AssertBool(HasExportedProperty(gradient, nameof(NoiseGradient.NoiseStrength), Variant.Type.Float)).IsTrue();
        AssertBool(HasExportedProperty(gradient, nameof(NoiseGradient.NoiseSeed), Variant.Type.Int)).IsTrue();
        AssertThat(gradient.PropertyGetRevert(nameof(NoiseGradient.BaseSignature)).Obj).IsNull();
        AssertBool(gradient.PropertyCanRevert(nameof(NoiseGradient.BaseSignature))).IsFalse();
        AssertBool(gradient.PropertyCanRevert(nameof(NoiseGradient.NoiseScale))).IsTrue();
        AssertFloat(gradient.PropertyGetRevert(nameof(NoiseGradient.NoiseScale)).AsSingle()).IsEqual(0.1f);
        AssertBool(gradient.PropertyCanRevert(nameof(NoiseGradient.NoiseStrength))).IsTrue();
        AssertFloat(gradient.PropertyGetRevert(nameof(NoiseGradient.NoiseStrength)).AsSingle()).IsEqual(0.3f);
        AssertBool(gradient.PropertyCanRevert(nameof(NoiseGradient.NoiseSeed))).IsTrue();
        AssertThat(gradient.PropertyGetRevert(nameof(NoiseGradient.NoiseSeed)).AsInt32()).IsEqual(42);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RadialGradientRevertsOnlyExportedScalarProperties()
    {
        var gradient = new RadialGradient();

        AssertBool(HasExportedProperty(gradient, nameof(RadialGradient.Falloff), Variant.Type.Float)).IsTrue();
        AssertThat(gradient.PropertyGetRevert(nameof(RadialGradient.CenterSignature)).Obj).IsNull();
        AssertBool(gradient.PropertyCanRevert(nameof(RadialGradient.CenterSignature))).IsFalse();
        AssertBool(gradient.PropertyCanRevert(nameof(RadialGradient.EdgeSignature))).IsFalse();
        AssertThat(gradient.PropertyGetRevert(nameof(RadialGradient.EdgeSignature)).Obj).IsNull();
        AssertBool(gradient.PropertyCanRevert(nameof(RadialGradient.Falloff))).IsTrue();
        AssertFloat(gradient.PropertyGetRevert(nameof(RadialGradient.Falloff)).AsSingle()).IsEqual(1.0f);
    }

    private static bool HasExportedProperty(GodotObject target, string propertyName, Variant.Type type)
    {
        return target.GetPropertyList().Any(property =>
            property["name"].AsString() == propertyName
            && property["type"].AsInt32() == (int)type
            && (property["usage"].AsInt32() & (int)PropertyUsageFlags.ScriptVariable) != 0);
    }
}
