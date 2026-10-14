using System.Collections.Generic;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Composition;

/// <summary>
///     The "Services" autoload injects every IDependent node that enters the tree, through its live NodeAdded
///     subscription: after the node's _EnterTree, before its _Ready (#273, AC-1). Nodes are added to the real tree
///     here, never injected by hand, so the tests prove the subscription itself.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TreeInjectionTest
{
    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InjectRunsAfterEnterTreeAndBeforeReady()
    {
        var probe = new InjectionProbe();
        try
        {
            Root().AddChild(probe);

            Assertions.AssertThat(probe.Log).ContainsExactly("probe.enter", "probe.inject", "probe.ready");
            Assertions.AssertThat(probe.Services).IsNotNull();
            Assertions.AssertThat(probe.Services).IsSame(TestServices.Resolver);
        }
        finally
        {
            probe.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ReparentInjectsAgainButWiresOnce()
    {
        var first = new Node();
        var second = new Node();
        var probe = new InjectionProbe();
        try
        {
            Root().AddChild(first);
            Root().AddChild(second);
            first.AddChild(probe);

            probe.Reparent(second);

            Assertions.AssertThat(probe.Log).ContainsExactly(
                "probe.enter", "probe.inject", "probe.ready", "probe.enter", "probe.inject");
            Assertions.AssertInt(probe.WiringRuns).IsEqual(1);
            Assertions.AssertThat(probe.GetParent()).IsSame(second);
        }
        finally
        {
            first.Free();
            second.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ChildOfAnAddedSubtreeIsInjectedBeforeItsReady()
    {
        var log = new List<string>();
        var parent = new InjectionProbe("parent", log);
        var child = new InjectionProbe("child", log);
        parent.AddChild(child);
        try
        {
            Root().AddChild(parent);

            // Children enter after their parent and become ready before it.
            Assertions.AssertThat(log).ContainsExactly(
                "parent.enter", "parent.inject", "child.enter", "child.inject", "child.ready", "parent.ready");
            Assertions.AssertThat(child.Services).IsSame(TestServices.Resolver);
        }
        finally
        {
            parent.Free();
        }
    }

    private static Window Root() => ((SceneTree)Engine.GetMainLoop()).Root;
}
