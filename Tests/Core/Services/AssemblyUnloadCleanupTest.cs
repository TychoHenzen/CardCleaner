using System;
using System.IO;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Each cache owner registers its clear with AssemblyUnloadCleanup; unloading runs every registered clear,
/// a clear that throws does not stop the others, and the loader's cached mapping is dropped by it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AssemblyUnloadCleanupTest
{
    private string _mappingPath = "";

    [BeforeTest]
    public void Setup()
    {
        _mappingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
    }

    [AfterTest]
    public void TearDown()
    {
        AssemblyUnloadCleanup.ClearProjectCaches();
        if (File.Exists(_mappingPath))
            File.Delete(_mappingPath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ARegisteredClearRunsWhenProjectCachesAreCleared()
    {
        var runs = 0;
        AssemblyUnloadCleanup.Register(() => runs++);

        AssemblyUnloadCleanup.ClearProjectCaches();

        AssertInt(runs).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AClearThatThrowsDoesNotStopTheNextOne()
    {
        var ranAfterThrow = false;
        AssemblyUnloadCleanup.Register(() => throw new InvalidOperationException("clear failed"));
        AssemblyUnloadCleanup.Register(() => ranAfterThrow = true);

        AssemblyUnloadCleanup.ClearProjectCaches();

        AssertBool(ranAfterThrow).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClearingProjectCachesDropsTheCachedMapping()
    {
        File.WriteAllText(_mappingPath, MappingJson("one"));
        AssertString(CompiledAtlasLoader.LoadMapping(_mappingPath)!.Version).IsEqual("one");
        File.WriteAllText(_mappingPath, MappingJson("two"));
        AssertString(CompiledAtlasLoader.LoadMapping(_mappingPath)!.Version).IsEqual("one");

        AssemblyUnloadCleanup.ClearProjectCaches();

        AssertString(CompiledAtlasLoader.LoadMapping(_mappingPath)!.Version).IsEqual("two");
    }

    private static string MappingJson(string version) =>
        "{\"version\":\"" + version + "\",\"atlas\":{\"path\":\"a.png\",\"width\":1,\"height\":1,\"tileSize\":1}}";
}
