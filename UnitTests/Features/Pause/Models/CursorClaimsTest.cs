using CardCleaner.Scripts.Features.Pause.Models;

namespace CardCleaner.Tests.Features.Pause.Models;

[TestSuite]
public class CursorClaimsTest
{
    [TestCase]
    public static void NoClaims_HasClaimsFalse()
    {
        var claims = new CursorClaims();

        AssertBool(claims.HasClaims).IsFalse();
    }

    [TestCase]
    public static void Claim_HasClaimsTrue()
    {
        var claims = new CursorClaims();

        claims.Claim(new object());

        AssertBool(claims.HasClaims).IsTrue();
    }

    [TestCase]
    public static void ReleaseLast_HasClaimsFalse()
    {
        var claims = new CursorClaims();
        var owner = new object();
        claims.Claim(owner);

        claims.Release(owner);

        AssertBool(claims.HasClaims).IsFalse();
    }

    [TestCase]
    public static void ReleaseUnknown_NoChange()
    {
        var claims = new CursorClaims();
        var claimed = new object();
        var unknown = new object();
        claims.Claim(claimed);

        claims.Release(unknown);

        AssertBool(claims.HasClaims).IsTrue();
        AssertBool(claims.HasClaimOtherThan(unknown)).IsTrue();
    }

    [TestCase]
    public static void TwoOwners_ReleaseOne_StillClaimed()
    {
        var claims = new CursorClaims();
        var first = new object();
        var second = new object();
        claims.Claim(first);
        claims.Claim(second);

        claims.Release(first);

        AssertBool(claims.HasClaims).IsTrue();
        AssertBool(claims.HasClaimOtherThan(first)).IsTrue();
        AssertBool(claims.HasClaimOtherThan(second)).IsFalse();
    }

    [TestCase]
    public static void HasClaimOtherThan_IgnoresSelf()
    {
        var claims = new CursorClaims();
        var a = new object();
        var b = new object();
        claims.Claim(a);

        AssertBool(claims.HasClaimOtherThan(a)).IsFalse();
        AssertBool(claims.HasClaimOtherThan(b)).IsTrue();
    }

    [TestCase]
    public static void ClaimTwiceBySameOwner_ReleaseOnce_Released()
    {
        var claims = new CursorClaims();
        var owner = new object();
        claims.Claim(owner);
        claims.Claim(owner);

        claims.Release(owner);

        AssertBool(claims.HasClaims).IsFalse();
    }
}
