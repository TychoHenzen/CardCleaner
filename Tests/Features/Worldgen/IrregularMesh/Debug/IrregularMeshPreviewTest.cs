using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.Debug;

/// <summary>
/// The inspector reset button of <see cref="IrregularMeshPreview"/>: a changed property reverts to its default.
/// The node stays out of the tree, so its editor-only _Ready never runs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshPreviewTest
{
    private const int DefaultRings = 6;

    private IrregularMeshPreview _preview = null!;

    [BeforeTest]
    public void Setup() => _preview = new IrregularMeshPreview();

    [AfterTest]
    public void Teardown() => _preview.Free();

    [TestCase]
    [TestCategory("Unit")]
    public void ChangedRingsRevertsToItsDefault()
    {
        AssertThat(_preview.Rings).IsEqual(DefaultRings);
        _preview.Rings = 9;

        AssertBool(_preview._PropertyCanRevert(nameof(IrregularMeshPreview.Rings))).IsTrue();
        _preview.Rings = _preview._PropertyGetRevert(nameof(IrregularMeshPreview.Rings)).AsInt32();

        AssertThat(_preview.Rings).IsEqual(DefaultRings);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PropertyWithoutDefaultCannotRevert()
    {
        AssertBool(_preview._PropertyCanRevert("ShowVertices")).IsFalse();
    }
}
