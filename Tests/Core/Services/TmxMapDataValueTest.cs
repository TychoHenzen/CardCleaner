using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;
using CardCleaner.Features.Deckbuilder.Tiles;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// TmxMapData is an immutable value record: it is a record type, every property is get-only or init-only, and every
/// instance field is readonly. This pins what the dependency gate measures for it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TmxMapDataValueTest
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase]
    [TestCategory("Unit")]
    public static void TmxMapDataIsAnImmutableRecord()
    {
        AssertThat(typeof(TmxMapData).GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public))
            .IsNotNull();

        foreach (var property in typeof(TmxMapData).GetProperties(InstanceMembers))
        {
            var setter = property.SetMethod;
            if (setter == null)
                continue;

            AssertBool(IsInitOnly(setter)).IsTrue();
        }

        foreach (var field in typeof(TmxMapData).GetFields(InstanceMembers))
            AssertBool(field.IsInitOnly).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MutatingTheSourceCollectionsDoesNotChangeTheRecord()
    {
        var tilesets = new List<TmxTilesetReference> { EmptyTileset() };
        var chunks = new Dictionary<Vector2I, TmxChunk>
        {
            [Vector2I.Zero] = new TmxChunk(Vector2I.Zero, 1, 1, [1]),
        };
        var map = new TmxMapData(new Vector2I(1, 1), tilesets, chunks);

        tilesets.Clear();
        chunks.Clear();

        AssertThat(map.Tilesets.Count).IsEqual(1);
        AssertBool(map.GetBounds() == new TmxBounds(Vector2I.Zero, new Vector2I(15, 15))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ExposedCollectionsAreReadOnlyInterfaces()
    {
        var tilesetsType = typeof(TmxMapData).GetProperty(nameof(TmxMapData.Tilesets), InstanceMembers)?.PropertyType;
        AssertBool(tilesetsType != null && tilesetsType.IsGenericType
                   && tilesetsType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .IsTrue();

        var chunksType = typeof(TmxMapData).GetField("_chunks", InstanceMembers)?.FieldType;
        AssertBool(chunksType != null && chunksType.IsGenericType
                   && chunksType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MutatingTheSourceTilesDoesNotChangeTheChunk()
    {
        var tiles = new[] { 7, 8, 9, 10 };
        var chunk = new TmxChunk(Vector2I.Zero, 2, 2, tiles);

        tiles[3] = 99;

        AssertThat(chunk.GetTileAt(1, 1)).IsEqual(10);
    }

    private static TmxTilesetReference EmptyTileset()
        => new(1, 1, "tileset.tsx", new TileRegistryResult("", [], TilesetConfig.Default), 1);

    private static bool IsInitOnly(MethodInfo setter)
        => setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
