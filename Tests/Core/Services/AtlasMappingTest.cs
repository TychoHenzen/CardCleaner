using System;
using System.IO;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using Godot;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// The engine-free atlas mapping reads JSON from an absolute path, caches by that path, never caches a failure,
/// and translates original tile coordinates to atlas coordinates.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AtlasMappingTest
{
    private const string ValidJson =
        "{\"version\":\"v1\",\"atlas\":{\"path\":\"a.png\",\"width\":32,\"height\":16,\"tileSize\":16}," +
        "\"sources\":{\"4\":{\"1,2\":{\"x\":3,\"y\":5,\"w\":1,\"h\":1}}}}";

    private string _path = "";

    [BeforeTest]
    public void Setup()
    {
        AtlasMapping.ClearCache();
        _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
    }

    [AfterTest]
    public void TearDown()
    {
        AtlasMapping.ClearCache();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AValidFileParses()
    {
        File.WriteAllText(_path, ValidJson);

        var mapping = AtlasMapping.Load(_path);

        AssertThat(mapping).IsNotNull();
        AssertString(mapping!.Version).IsEqual("v1");
        AssertInt(mapping.Atlas!.TileSize).IsEqual(16);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AMissingFileGivesNullAndIsRetriedOnceTheFileAppears()
    {
        AssertThat(AtlasMapping.Load(_path)).IsNull();

        File.WriteAllText(_path, ValidJson);

        AssertThat(AtlasMapping.Load(_path)).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MalformedJsonGivesNullWithoutCachingTheFailure()
    {
        File.WriteAllText(_path, "not-json");
        AssertThat(AtlasMapping.Load(_path)).IsNull();

        File.WriteAllText(_path, ValidJson);

        AssertThat(AtlasMapping.Load(_path)).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void JsonWithoutAnAtlasRootGivesNullWithoutCachingTheFailure()
    {
        File.WriteAllText(_path, "{\"version\":\"test\"}");
        AssertThat(AtlasMapping.Load(_path)).IsNull();

        File.WriteAllText(_path, ValidJson);

        AssertThat(AtlasMapping.Load(_path)).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClearCacheForcesARereadOfTheFile()
    {
        File.WriteAllText(_path, ValidJson);
        AssertString(AtlasMapping.Load(_path)!.Version).IsEqual("v1");
        File.WriteAllText(_path, ValidJson.Replace("v1", "v2"));
        AssertString(AtlasMapping.Load(_path)!.Version).IsEqual("v1");

        AtlasMapping.ClearCache();

        AssertString(AtlasMapping.Load(_path)!.Version).IsEqual("v2");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AKnownCoordinateMapsToSourceZeroAndItsRect()
    {
        File.WriteAllText(_path, ValidJson);
        var mapping = AtlasMapping.Load(_path);

        var (sourceId, coords) = AtlasMapping.Translate(4, new Vector2I(1, 2), mapping);

        AssertInt(sourceId).IsEqual(0);
        AssertThat(coords).IsEqual(new Vector2I(3, 5));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnUnknownCoordinateComesBackUnchanged()
    {
        File.WriteAllText(_path, ValidJson);
        var mapping = AtlasMapping.Load(_path);

        var (sourceId, coords) = AtlasMapping.Translate(4, new Vector2I(9, 9), mapping);

        AssertInt(sourceId).IsEqual(4);
        AssertThat(coords).IsEqual(new Vector2I(9, 9));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WithoutAMappingArgumentTheCachedMappingIsUsed()
    {
        File.WriteAllText(_path, ValidJson);
        AtlasMapping.Load(_path);

        var (sourceId, coords) = AtlasMapping.Translate(4, new Vector2I(1, 2));

        AssertInt(sourceId).IsEqual(0);
        AssertThat(coords).IsEqual(new Vector2I(3, 5));
    }
}
