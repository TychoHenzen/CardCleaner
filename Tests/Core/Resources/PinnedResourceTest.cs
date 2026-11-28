using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Scripts.Core.Resources;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Tests.Core.Resources;

/// <summary>
///     Every C# resource class derives from <see cref="PinnedResource" />, so no loaded resource can lose its managed
///     wrapper while the resource cache still hands it out (godotengine/godot issue 83762).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PinnedResourceTest
{
    // A call such as ResourceLoader.LoadThreadedRequest(path), not a mention of the name in a comment.
    private static readonly Regex ThreadedLoadCall = new(@"\.LoadThreaded\w*\s*\(", RegexOptions.CultureInvariant);

    [TestCase]
    public void EveryCSharpResourceClassIsPinned()
    {
        var resources = typeof(PinnedResource).Assembly.GetTypes()
            .Where(type => type != typeof(PinnedResource) && type.IsSubclassOf(typeof(Resource)))
            .Where(type => type.Namespace?.StartsWith("CardCleaner.", StringComparison.Ordinal) == true)
            .ToList();
        var unpinned = resources
            .Where(type => !type.IsSubclassOf(typeof(PinnedResource)))
            .Select(type => type.FullName)
            .ToList();

        AssertThat(resources).Contains(typeof(OrderCatalog));
        AssertThat(unpinned)
            .OverrideFailureMessage("Derive these from PinnedResource: " + string.Join(", ", unpinned))
            .IsEmpty();
    }

    // PinnedResource checks a new instance's path at the end of the frame. A load on a worker thread can set the
    // path after that check, so such a resource would not be pinned.
    [TestCase]
    public void NothingInScriptsLoadsOnAWorkerThread()
    {
        var scripts = ProjectSettings.GlobalizePath("res://Scripts");
        var threadedLoads = Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories)
            .Where(file => ThreadedLoadCall.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(scripts, file))
            .ToList();

        AssertThat(threadedLoads)
            .OverrideFailureMessage("These load on a worker thread: " + string.Join(", ", threadedLoads))
            .IsEmpty();
    }
}
