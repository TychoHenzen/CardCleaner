using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Pause.Components;

// Asserts AppliedMode only. Input.MouseMode is not read back, because headless MouseMode behaviour is not relied on.
[TestSuite]
[RequireGodotRuntime]
public class CursorOwnerTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Ready_NoClaims_Captured()
    {
        var owner = AddNode(new CursorOwner());

        AssertThat(owner.AppliedMode).IsEqual(Godot.Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Claim_Visible()
    {
        var owner = AddNode(new CursorOwner());

        owner.Claim(new object());

        AssertThat(owner.AppliedMode).IsEqual(Godot.Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ReleaseLastClaim_Captured()
    {
        var owner = AddNode(new CursorOwner());
        var claimant = new object();
        owner.Claim(claimant);

        owner.Release(claimant);

        AssertThat(owner.AppliedMode).IsEqual(Godot.Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoClaims_ReleaseOne_StillVisible()
    {
        var owner = AddNode(new CursorOwner());
        var first = new object();
        var second = new object();
        owner.Claim(first);
        owner.Claim(second);

        owner.Release(first);

        AssertThat(owner.AppliedMode).IsEqual(Godot.Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ReleaseUnknownOwner_WhileClaimed_StaysVisible()
    {
        var owner = AddNode(new CursorOwner());
        owner.Claim(new object());

        owner.Release(new object());

        AssertThat(owner.AppliedMode).IsEqual(Godot.Input.MouseModeEnum.Visible);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void HasClaimOtherThan_ForeignOwner_True()
    {
        var owner = AddNode(new CursorOwner());
        var claimant = new object();
        owner.Claim(claimant);

        AssertBool(owner.HasClaimOtherThan(new object())).IsTrue();
        AssertBool(owner.HasClaimOtherThan(claimant)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AClaimSuspendsTheWorldActionsUntilTheLastRelease()
    {
        // The test's own input service replaces the app's for this case; the reset at the end drops it again, as the
        // scene suites that register their own input service do in their teardown.
        var input = AddNode(new InputService());
        TestServices.Container.RegisterSingleton<IInputService>(input);
        try
        {
            var owner = AddNode(new CursorOwner());
            var first = new object();
            var second = new object();

            owner.Claim(first);
            owner.Claim(second);
            owner.Release(first);
            AssertBool(input.ActionsSuspended).IsTrue();

            owner.Release(second);
            AssertBool(input.ActionsSuspended).IsFalse();

            owner.Claim(first);
            owner.GetParent().RemoveChild(owner);
            AssertBool(input.ActionsSuspended).IsFalse();
        }
        finally
        {
            TestServices.Reset();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsRegisteredUnderICursorOwner()
    {
        var attributes = typeof(CursorOwner).GetCustomAttributes(typeof(ServiceAttribute), false)
            .Cast<ServiceAttribute>()
            .ToArray();

        AssertThat(attributes.Length).IsEqual(1);
        AssertBool(attributes[0].ServiceTypes.SequenceEqual(new[] { typeof(ICursorOwner) })).IsTrue();
    }
}
