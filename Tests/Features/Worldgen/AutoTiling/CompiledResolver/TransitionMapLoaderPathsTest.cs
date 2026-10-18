using System.IO;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.CompiledResolver;

/// <summary>
/// The transition map loader reads res:// paths through IAssetPaths and gives an empty map when the file is absent
/// or malformed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TransitionMapLoaderPathsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TestEmptyRootGivesEmptyMap()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var resolver = new CompiledTransitionResolver(new TempAssetPaths(root));

            AssertThat(resolver.GetAllTransitionPairs().ToList().Count).IsEqual(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMalformedJsonGivesEmptyMap()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var compiledDir = Path.Combine(root, "Data", "CompiledAtlas");
            Directory.CreateDirectory(compiledDir);
            File.WriteAllText(Path.Combine(compiledDir, "transition_map.json"), "{ not json");

            var resolver = new CompiledTransitionResolver(new TempAssetPaths(root));

            AssertThat(resolver.GetAllTransitionPairs().ToList().Count).IsEqual(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
