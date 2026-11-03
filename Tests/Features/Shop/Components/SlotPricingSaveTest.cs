using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using GdUnit4;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// A shelf slot's price tag saves its markup in the save and reads it back. A markup that is missing, off the ladder
/// or not a whole number in range loads as the default, and a text, decimal or boolean value never throws.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SlotPricingSaveTest
{
    private const string MarkupKey = "markup";

    private SlotPricing _tag = null!;
    private SlotPricing _loaded = null!;

    [BeforeTest]
    public void Setup()
    {
        _tag = new SlotPricing();
        _loaded = new SlotPricing();
    }

    [AfterTest]
    public void Teardown()
    {
        _tag.Free();
        _loaded.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MarkupRoundTripsThroughNodeSave()
    {
        _tag.SetMarkup(30);
        var save = SaveOf(_tag);

        _loaded.Load(save);

        AssertThat(_loaded.MarkupPercent).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AMissingKeyKeepsTheDefault()
    {
        _loaded.Load(new NodeSave());

        AssertThat(_loaded.MarkupPercent).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnOffLadderMarkupLoadsAsZero()
    {
        var save = new NodeSave();
        save.SetOrAddProperty(MarkupKey, 7);

        _loaded.Load(save);

        AssertThat(_loaded.MarkupPercent).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ATextMarkupLoadsAsZeroWithoutThrowing()
    {
        var save = new NodeSave();
        save.SetOrAddProperty(MarkupKey, "abc");

        _loaded.Load(save);

        AssertThat(_loaded.MarkupPercent).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ADoubleOrBoolMarkupLoadsAsZeroWithoutThrowing()
    {
        var fraction = new NodeSave();
        fraction.SetOrAddProperty(MarkupKey, 3.5);
        _loaded.Load(fraction);
        AssertThat(_loaded.MarkupPercent).IsEqual(0);

        var flag = new NodeSave();
        flag.SetOrAddProperty(MarkupKey, true);
        _loaded.Load(flag);
        AssertThat(_loaded.MarkupPercent).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnOutOfRangeNumberLoadsAsZero()
    {
        var save = new NodeSave();
        save.SetOrAddProperty(MarkupKey, 99999999999L);

        _loaded.Load(save);

        AssertThat(_loaded.MarkupPercent).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryLadderStepRoundTrips()
    {
        foreach (var step in AskingPrice.MarkupLadder)
        {
            _tag.SetMarkup(step);
            var save = SaveOf(_tag);

            _loaded.Load(save);

            AssertThat(_loaded.MarkupPercent).IsEqual(step);
        }
    }

    private static NodeSave SaveOf(SlotPricing tag)
    {
        var save = new NodeSave();
        tag.Save(save);
        return save;
    }
}
