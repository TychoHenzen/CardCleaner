using System.IO;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TileDataLoaderFormat;

/// <summary>
///     Shared fixture for the TileDataLoaderFormatTest scenario suites.
/// </summary>
public abstract class TileDataLoaderFormatTestBase
{
    protected string _testDir = null!;

    /// <summary>
    /// Setup test directory for JSON files.
    /// </summary>
    [BeforeTest]
    public void Setup()
    {
        // Use OS temp directory for test files
        _testDir = Path.Combine(Path.GetTempPath(), "cardcleaner_test_" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    /// <summary>
    /// Clean up test directory and registry state.
    /// </summary>
    [AfterTest]
    public void Cleanup()
    {
        AutoTileFormatRegistry.ClearCustomFormats();

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    // ==================== Helper Methods ====================

    protected TileRegistryResult LoadFromJson(string json)
    {
        var filePath = Path.Combine(_testDir, "tiles.json");
        File.WriteAllText(filePath, json);

        // TileDataLoader expects res:// paths, but we can use absolute path
        // by converting the path format
        return TileDataLoader.LoadTileRegistry(filePath);
    }
}
