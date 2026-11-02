using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.SaveSlots.Models;

[TestSuite]
[RequireGodotRuntime]
public class SaveSlotNameTest
{
    [TestCase("..")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase("C:x")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("name.")]
    [TestCategory("Unit")]
    public static void RejectedNamesReturnFalseWithReason(string raw)
    {
        var accepted = SaveSlotName.TryCreate(raw, out _, out var reason);

        AssertThat(accepted).IsFalse();
        AssertThat(reason).IsNotEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NameLongerThanThirtyTwoCharactersIsRejected()
    {
        var accepted = SaveSlotName.TryCreate(new string('a', 33), out _, out var reason);

        AssertThat(accepted).IsFalse();
        AssertThat(reason).IsNotEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NamesThatDifferOnlyByCaseHaveEqualKey()
    {
        SaveSlotName.TryCreate("My Shop", out var upper, out _);
        SaveSlotName.TryCreate("my shop", out var lower, out _);

        AssertThat(upper.Key).IsEqual(lower.Key);
        AssertThat(upper).IsEqual(lower);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MyShopKeepsItsValue()
    {
        SaveSlotName.TryCreate("My Shop", out var name, out _);

        AssertThat(name.Value).IsEqual("My Shop");
    }
}
