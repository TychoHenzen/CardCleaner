using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Services;

[TestSuite]
[RequireGodotRuntime]
public class ActiveSaveSlotTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void DefaultIsDefault()
    {
        var slot = new ActiveSaveSlot();

        AssertThat(slot.Current.Key).IsEqual("default");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SelectRaisesChangedOnceWithNewName()
    {
        var slot = new ActiveSaveSlot();
        var raised = new System.Collections.Generic.List<string>();
        slot.Changed += name => raised.Add(name.Key);
        SaveSlotName.TryCreate("My Shop", out var name, out _);

        slot.Select(name);

        AssertThat(raised.Count).IsEqual(1);
        AssertThat(raised[0]).IsEqual("my shop");
        AssertThat(slot.Current.Key).IsEqual("my shop");
    }
}
