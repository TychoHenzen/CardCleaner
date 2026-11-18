// Project patch (discovery cache): not part of the upstream gdUnit4 addon. Reapply after updating the addon.
#pragma warning disable IDE1006
namespace gdUnit4.addons.gdUnit4.src.dotnet;
#pragma warning restore IDE1006

#if GDUNIT4NET_API_V5
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using GdUnit4.Api;
using GdUnit4.Core.Discovery;

using Godot;

/// <summary>
///     Answers gdUnit's per-script C# discovery from one read of the test assembly. The package's per-script
///     discovery (<c>GdUnit4NetApiGodotBridge.DiscoverTestsFromScript</c>) opens the whole assembly and its symbols
///     with Mono.Cecil on every call, about 33 ms each, which made discovering the 518 scripts under res://Tests take
///     17 s at the start of every run. This reads the assembly once through the package's own whole-assembly
///     discovery (the internal <c>TestCaseDiscoverer.Discover</c>, reached by reflection) and files each suite's tests
///     under its script. Outside the editor only: the editor rebuilds the assembly while it runs, and its inspector
///     keeps the upstream path.
/// </summary>
internal static class SuiteDiscoveryCache
{
    private const string DiscovererTypeName = "GdUnit4.Core.Discovery.TestCaseDiscoverer";

    // Cecil names a type Namespace.Outer/Inner.
    private static readonly SearchValues<char> NameSeparators = SearchValues.Create("./");

    private static readonly string[] BuildConfigurations = { "Debug", "Release", "ExportDebug", "ExportRelease" };

    private static readonly object Gate = new();
    private static Index? index;
    private static bool tried;

    /// <summary>Why the cache could not be built, or null when it was built or has not been tried.</summary>
    internal static string? FailureReason { get; private set; }

    /// <summary>
    ///     The tests the per-script discovery would find in <paramref name="script" />, or null when the cache cannot
    ///     answer and the caller must ask the package bridge: in the editor, when the cache could not be built, or
    ///     when the script names a cached suite class without holding its tests.
    /// </summary>
    internal static IReadOnlyCollection<TestCaseDescriptor>? TestsOf(CSharpScript script)
    {
        if (Engine.IsEditorHint())
            return null;
        var built = Build();
        return built?.TestsOf(Path.GetFullPath(ProjectSettings.GlobalizePath(script.ResourcePath)));
    }

    private static Index? Build()
    {
        lock (Gate)
        {
            if (tried)
                return index;
            tried = true;
            try
            {
                index = Index.Read(TestAssemblyPaths(), out var reason);
                FailureReason = reason;
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                FailureReason = $"{e.GetType().Name}: {(e as TargetInvocationException)?.InnerException?.Message ?? e.Message}";
            }

            if (index == null)
                GD.PrintErr($"gdUnit4 discovery cache unavailable, discovering per script: {FailureReason}");
            return index;
        }
    }

    // The assemblies the package's per-script discovery searches, in its order. Godot loads the project assembly from
    // memory, so the loaded assembly has no Location to read instead.
    private static List<string> TestAssemblyPaths()
    {
        var name = ProjectSettings.GetSetting("dotnet/project/assembly_name").AsString();
        if (string.IsNullOrEmpty(name))
            name = ProjectSettings.GetSetting("application/config/name").AsString();
        var bin = Path.Combine(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")), ".godot", "mono", "temp", "bin");
        return BuildConfigurations
            .Select(configuration => Path.Combine(bin, configuration, $"{name}.dll"))
            .Where(File.Exists)
            .ToList();
    }

    private sealed class Index
    {
        private readonly Dictionary<string, IReadOnlyCollection<TestCaseDescriptor>> byScript;
        private readonly HashSet<string> suiteNames;

        private Index(
            Dictionary<string, IReadOnlyCollection<TestCaseDescriptor>> byScript,
            HashSet<string> suiteNames)
        {
            this.byScript = byScript;
            this.suiteNames = suiteNames;
        }

        public static Index? Read(IReadOnlyList<string> assemblyPaths, out string? reason)
        {
            if (assemblyPaths.Count == 0)
            {
                reason = "no test assembly under .godot/mono/temp/bin";
                return null;
            }

            var discover = typeof(TestCaseDescriptor).Assembly.GetType(DiscovererTypeName)?.GetMethod(
                "Discover",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(TestEngineSettings), typeof(ITestEngineLogger), typeof(string) },
                null);
            if (discover == null)
            {
                reason = $"{DiscovererTypeName}.Discover(TestEngineSettings, ITestEngineLogger, string) is gone";
                return null;
            }

            // The per-script discovery takes the class named after the script whose code lies in the script, from the
            // first assembly that has one, and returns every test of that class. Filed the same way: under each source
            // file named after the class, the first assembly winning.
            var byScript = new Dictionary<string, IReadOnlyCollection<TestCaseDescriptor>>(
                StringComparer.OrdinalIgnoreCase);
            var suiteNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assemblyPath in assemblyPaths)
            {
                var found = (IEnumerable<TestCaseDescriptor>?)discover.Invoke(
                    null,
                    new object[] { new TestEngineSettings(), new ErrorsOnlyLogger(), assemblyPath });
                if (found == null)
                {
                    reason = $"the whole-assembly discovery returned nothing for {assemblyPath}";
                    return null;
                }

                foreach (var suite in found.GroupBy(test => test.ManagedType, StringComparer.Ordinal))
                {
                    var tests = suite.ToList();
                    var name = ShortName(suite.Key);
                    suiteNames.Add(name);
                    foreach (var file in tests.Select(test => test.CodeFilePath).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (Path.IsPathFullyQualified(file ?? string.Empty)
                            && string.Equals(Path.GetFileNameWithoutExtension(file), name, StringComparison.Ordinal))
                            byScript.TryAdd(Path.GetFullPath(file!), tests);
                    }
                }
            }

            reason = null;
            return new Index(byScript, suiteNames);
        }

        // ASSUMPTION: a script whose name is no [TestSuite] class holds no tests. The per-script discovery would also
        // take tests from a plain or abstract class named after its file; gdUnit cannot run those, and the
        // whole-assembly discovery (the one the test adapter uses) skips them too.
        public IReadOnlyCollection<TestCaseDescriptor>? TestsOf(string scriptPath)
        {
            if (byScript.TryGetValue(scriptPath, out var tests))
                return tests;
            return suiteNames.Contains(Path.GetFileNameWithoutExtension(scriptPath))
                ? null
                : Array.Empty<TestCaseDescriptor>();
        }

        private static string ShortName(string managedType)
            => managedType[(managedType.AsSpan().LastIndexOfAny(NameSeparators) + 1)..];
    }

    // Discover logs one info line per suite; only its warnings and errors are worth a line in a run log.
    private sealed class ErrorsOnlyLogger : ITestEngineLogger
    {
        void ITestEngineLogger.SendMessage(LogLevel logLevel, string message)
        {
            if (logLevel == LogLevel.Error)
                GD.PrintErr(message);
            else if (logLevel == LogLevel.Warning)
                GD.Print(message);
        }
    }
}
#endif
