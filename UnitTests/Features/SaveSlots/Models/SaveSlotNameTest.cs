using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Tests.Features.SaveSlots.Models;

[TestSuite]
public class SaveSlotNameTest
{
    [TestCase("..")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase("C:x")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("name.")]
    public static void RejectedNamesReturnFalseWithReason(string raw)
    {
        var accepted = SaveSlotName.TryCreate(raw, out _, out var reason);

        AssertThat(accepted).IsFalse();
        AssertThat(reason).IsNotEmpty();
    }

    [TestCase]
    public static void NameLongerThanThirtyTwoCharactersIsRejected()
    {
        var accepted = SaveSlotName.TryCreate(new string('a', 33), out _, out var reason);

        AssertThat(accepted).IsFalse();
        AssertThat(reason).IsNotEmpty();
    }

    [TestCase]
    public static void NamesThatDifferOnlyByCaseHaveEqualKey()
    {
        SaveSlotName.TryCreate("My Shop", out var upper, out _);
        SaveSlotName.TryCreate("my shop", out var lower, out _);

        AssertThat(upper.Key).IsEqual(lower.Key);
        AssertThat(upper).IsEqual(lower);
    }

    [TestCase]
    public static void MyShopKeepsItsValue()
    {
        SaveSlotName.TryCreate("My Shop", out var name, out _);

        AssertThat(name.Value).IsEqual("My Shop");
    }
}
