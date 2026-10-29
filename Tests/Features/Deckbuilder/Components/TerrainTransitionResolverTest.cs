using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Components;

[TestSuite]
[RequireGodotRuntime]
public class TerrainTransitionResolverTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ResolveUsesTheInjectedResolver()
    {
        var baseTile = new TileDefinition("a", "A", TilePassability.Passable, Vector2I.Zero);
        var topTile = new TileDefinition("b", "B", TilePassability.Passable, Vector2I.One);
        var resolver = TerrainTransitions.Create(new FakeTransitionResolver(), null);
        var transitions = 0;

        var coords = resolver.Resolve(baseTile, topTile, "a", "b", 5, ref transitions);

        AssertThat(coords.SourceId).IsEqual(7);
        AssertThat(coords.AtlasCoords).IsEqual(new Vector2I(3, 4));
        AssertThat(transitions).IsEqual(1);
        AssertThat(resolver.UsingCompiledAtlas).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ResolveEmptyBitmaskTranslatesTheBaseTileThroughTheInjectedMapping()
    {
        var baseTile = new TileDefinition(
            "a", "A", TilePassability.Passable, new Vector2I(1, 2), new TileDefinitionOptions { SourceId = 4 });
        var mapping = new AtlasMapping.AtlasMappingData
        {
            Sources = new Dictionary<string, Dictionary<string, AtlasMapping.TileAtlasRect>>
            {
                ["4"] = new() { ["1,2"] = new AtlasMapping.TileAtlasRect { X = 3, Y = 5, W = 1, H = 1 } }
            }
        };
        var mapped = TerrainTransitions.Create(new FakeTransitionResolver(), mapping);
        var unmapped = TerrainTransitions.Create(new FakeTransitionResolver(), null);
        var transitions = 0;

        var mappedCoords = mapped.Resolve(baseTile, null, "a", "a", 0, ref transitions);
        var unmappedCoords = unmapped.Resolve(baseTile, null, "a", "a", 0, ref transitions);

        AssertInt(mappedCoords.SourceId).IsEqual(0);
        AssertThat(mappedCoords.AtlasCoords).IsEqual(new Vector2I(3, 5));
        AssertInt(unmappedCoords.SourceId).IsEqual(4);
        AssertThat(unmappedCoords.AtlasCoords).IsEqual(new Vector2I(1, 2));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DataErrorWithoutResolverFallsBack()
    {
        var baseTile = new TileDefinition("a", "A", TilePassability.Passable, new Vector2I(2, 2));
        var resolver = TerrainTransitions.Create(null, null);
        var transitions = 0;

        var coords = resolver.Resolve(baseTile, null, "a", "a", 5, ref transitions);

        var expectedAtlas = baseTile.HasAutoTileVariants ? baseTile.GetAutoTileCoords(5) : baseTile.AtlasCoords;
        AssertThat(resolver.UsingCompiledAtlas).IsFalse();
        AssertThat(coords.SourceId).IsEqual(baseTile.SourceId);
        AssertThat(coords.AtlasCoords).IsEqual(expectedAtlas);
    }

    private sealed class FakeTransitionResolver : ITransitionResolver
    {
        public int CompiledAtlasSourceId => 7;

        public TransitionResolveResult ResolveWithFallback(
            string innerTerrainId,
            string outerTerrainId,
            int bitmask,
            int fallbackSourceId,
            Vector2I fallbackCoords)
        {
            return new TransitionResolveResult(7, new Vector2I(3, 4));
        }

        public Vector2I? ResolveTransition(string innerTerrainId, string outerTerrainId, int bitmask)
        {
            return null;
        }

        public Vector2I? ResolveSolidFill(string terrainId)
        {
            return null;
        }

        public bool HasTransition(string innerTerrainId, string outerTerrainId)
        {
            return false;
        }

        public Vector2I? ResolveTransitionWithVariant(
            string innerTerrainId,
            string outerTerrainId,
            int bitmask,
            int x,
            int y)
        {
            return null;
        }

        public Vector2I? ResolveAsOuterTerrain(string terrainId, int bitmask)
        {
            return null;
        }

        public Vector2I? ResolveAnyVariant(string terrainId, int bitmask)
        {
            return null;
        }

        public IEnumerable<(string innerTerrain, string outerTerrain)> GetAllTransitionPairs()
        {
            return Array.Empty<(string innerTerrain, string outerTerrain)>();
        }
    }
}
