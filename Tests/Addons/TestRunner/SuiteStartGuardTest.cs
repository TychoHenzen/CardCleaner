using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CardCleaner.Tests.Addons.TestRunner;

/// <summary>
/// Keeps the issue 448 patch of the vendored gdUnit4 addon in place. The C# bridge must run every suite through
/// SuiteStartWatchdog, which a gdUnit4 addon update would silently remove. The watchdog counts a silent suite as a
/// lost start, and gdUnit sends a suite's first event only after its [Before] hook, so no [Before] hook may await.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SuiteStartGuardTest
{
    private const string BridgePath = "res://addons/gdUnit4/src/dotnet/GdUnit4CSharpApi.cs";

    [TestCase]
    [TestCategory("Unit")]
    public static void TheCSharpBridgeRunsEverySuiteThroughTheWatchdog()
    {
        var source = Godot.FileAccess.GetFileAsString(BridgePath);
        AssertThat(source).IsNotEmpty();

        AssertThat(Regex.Matches(source, @"GdUnit4NetApiGodotBridge\.ExecuteAsync\(").Count)
            .OverrideFailureMessage($"{BridgePath} must call the package bridge exactly once, inside the watchdog")
            .IsEqual(1);
        AssertThat(Regex.IsMatch(
                source,
                @"SuiteStartWatchdog\.Run<Variant>\(\s*\(receive, token\) => GdUnit4NetApiGodotBridge\.ExecuteAsync\("))
            .OverrideFailureMessage(
                $"{BridgePath} no longer starts suites through SuiteStartWatchdog (issue 448); restore the patch "
                + "after a gdUnit4 addon update")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoSuiteBeforeHookAwaits()
    {
        var hooks = typeof(SuiteStartGuardTest).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<BeforeAttribute>() != null)
            .ToList();
        AssertThat(hooks.Count > 0)
            .OverrideFailureMessage("The [Before] hook scan found no hook, so the guard could pass on nothing")
            .IsTrue();

        var awaiting = hooks
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType)
                || method.ReturnType.Name.StartsWith("ValueTask", System.StringComparison.Ordinal))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .ToList();
        AssertThat(awaiting)
            .OverrideFailureMessage(
                "These [Before] hooks await, so SuiteStartWatchdog could abandon a suite that is still starting "
                + $"(issue 448): {string.Join(", ", awaiting)}")
            .IsEmpty();
    }
}
