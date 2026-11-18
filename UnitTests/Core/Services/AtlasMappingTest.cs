using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using Godot;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// The engine-free atlas mapping reads JSON from an absolute path, returns null for a missing or invalid file,
/// holds no static state, and translates original tile coordinates to atlas coordinates.
/// </summary>
[TestSuite]
public class AtlasMappingTest
{
    private const string ValidJson =
        "{\"version\":\"v1\",\"atlas\":{\"path\":\"a.png\",\"width\":32,\"height\":16,\"tileSize\":16}," +
        "\"sources\":{\"4\":{\"1,2\":{\"x\":3,\"y\":5,\"w\":1,\"h\":1}}}}";

    private string _path = "";

    [BeforeTest]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
    }

    [AfterTest]
    public void TearDown()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [TestCase]
    public void AValidFileParses()
    {
        File.WriteAllText(_path, ValidJson);

        var mapping = AtlasMapping.Load(_path);

        AssertThat(mapping).IsNotNull();
        AssertString(mapping!.Version).IsEqual("v1");
        AssertInt(mapping.Atlas!.TileSize).IsEqual(16);
    }

    [TestCase]
    public void AMissingFileGivesNullAndALaterLoadFindsTheFile()
    {
        AssertThat(AtlasMapping.Load(_path)).IsNull();

        File.WriteAllText(_path, ValidJson);

        AssertThat(AtlasMapping.Load(_path)).IsNotNull();
    }

    [TestCase]
    public void MalformedJsonGivesNullAndALaterLoadReadsTheFixedFile()
    {
        File.WriteAllText(_path, "not-json");
        AssertThat(AtlasMapping.Load(_path)).IsNull();

        File.WriteAllText(_path, ValidJson);

        AssertThat(AtlasMapping.Load(_path)).IsNotNull();
    }

    [TestCase]
    public void JsonWithoutAnAtlasRootGivesNullAndALaterLoadReadsTheFixedFile()
    {
        File.WriteAllText(_path, "{\"version\":\"test\"}");
        AssertThat(AtlasMapping.Load(_path)).IsNull();

        File.WriteAllText(_path, ValidJson);

        AssertThat(AtlasMapping.Load(_path)).IsNotNull();
    }

    [TestCase]
    public void EveryLoadRereadsTheFile()
    {
        File.WriteAllText(_path, ValidJson);
        AssertString(AtlasMapping.Load(_path)!.Version).IsEqual("v1");

        File.WriteAllText(_path, ValidJson.Replace("v1", "v2"));

        AssertString(AtlasMapping.Load(_path)!.Version).IsEqual("v2");
    }

    [TestCase]
    public void AKnownCoordinateMapsToSourceZeroAndItsRect()
    {
        File.WriteAllText(_path, ValidJson);
        var mapping = AtlasMapping.Load(_path);

        var translation = AtlasMapping.Translate(4, new Vector2I(1, 2), mapping);

        AssertInt(translation.SourceId).IsEqual(0);
        AssertThat(translation.AtlasCoords).IsEqual(new Vector2I(3, 5));
        AssertBool(translation.Mapped).IsTrue();
    }

    [TestCase]
    public void AnUnknownCoordinateComesBackUnchanged()
    {
        File.WriteAllText(_path, ValidJson);
        var mapping = AtlasMapping.Load(_path);

        var translation = AtlasMapping.Translate(4, new Vector2I(9, 9), mapping);

        AssertInt(translation.SourceId).IsEqual(4);
        AssertThat(translation.AtlasCoords).IsEqual(new Vector2I(9, 9));
        AssertBool(translation.Mapped).IsFalse();
    }

    [TestCase]
    public void ANullMappingReturnsTheOriginalCoordinates()
    {
        File.WriteAllText(_path, ValidJson);
        AtlasMapping.Load(_path);

        var translation = AtlasMapping.Translate(4, new Vector2I(1, 2), null);

        AssertInt(translation.SourceId).IsEqual(4);
        AssertThat(translation.AtlasCoords).IsEqual(new Vector2I(1, 2));
        AssertBool(translation.Mapped).IsFalse();
    }

    [TestCase]
    public static void AtlasMappingHasNoWritableStaticState()
    {
        var writable = typeof(AtlasMapping)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsInitOnly && !field.IsLiteral)
            .ToArray();

        AssertInt(writable.Length).IsEqual(0);
    }
}
