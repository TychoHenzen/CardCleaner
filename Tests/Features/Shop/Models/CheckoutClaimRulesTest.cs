using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class CheckoutClaimRulesTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void New_claim_is_not_held_and_excludes_nobody()
    {
        var claim = new CheckoutClaim();

        AssertThat(claim.IsHeld).IsFalse();
        AssertThat(claim.Excludes(null)).IsFalse();
        AssertThat(claim.Excludes(new object())).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Held_claim_excludes_everyone_but_the_holder()
    {
        var claim = new CheckoutClaim();
        var holder = new object();

        AssertThat(claim.TryTake(holder)).IsTrue();

        AssertThat(claim.IsHeld).IsTrue();
        AssertThat(claim.Excludes(holder)).IsFalse();
        AssertThat(claim.Excludes(new object())).IsTrue();
        AssertThat(claim.Excludes(null)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Second_take_is_refused_and_keeps_the_first_holder()
    {
        var claim = new CheckoutClaim();
        var first = new object();
        claim.TryTake(first);

        AssertThat(claim.TryTake(new object())).IsFalse();

        AssertThat(claim.Excludes(first)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Only_the_holder_can_release()
    {
        var claim = new CheckoutClaim();
        var holder = new object();
        claim.TryTake(holder);

        claim.Release(new object());
        AssertThat(claim.IsHeld).IsTrue();

        claim.Release(holder);
        AssertThat(claim.IsHeld).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Clear_drops_the_claim_whoever_holds_it()
    {
        var claim = new CheckoutClaim();
        claim.TryTake(new object());

        claim.Clear();

        AssertThat(claim.IsHeld).IsFalse();
    }
}
