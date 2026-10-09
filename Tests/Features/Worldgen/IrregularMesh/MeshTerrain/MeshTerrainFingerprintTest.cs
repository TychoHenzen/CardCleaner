using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Pins the terrain that mesh generation produces for two fixed-seed paths: the factory path with cards
/// (background WFC plus mesh foreground WFC) and the dictionary rules path. A refactor of the WFC front door
/// must reproduce these fingerprints exactly. The golden values were recorded on master 857068e.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MeshTerrainFingerprintTest
{
    private const string FactoryGolden = "f6d30692addfccf5cae860c8a3885223b81ad0bd4963d4c100b270556d6621ff";
    private const string RulesGolden = "68bf23cfce9a2bbd9d69c863bcd9e77b7830d5a402c10e3f9af328be929f8f2e";

    [TestCase]
    [TestCategory("Unit")]
    public static void FactoryPathWithCardsMatchesMasterFingerprint()
    {
        var generator = TerrainGeneratorFactory.Create(new TileRegistry());
        var mesh = generator.GenerateWithCards(3, TerrainCards(), 4242, relaxationIterations: 8);

        var backgroundTiles = mesh.Quads
            .Select(quad => quad.BackgroundTileId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .Count();
        // ASSUMPTION: a real two-pass run on seed 4242 has at least two background tiles; the fallback writes one.
        AssertThat(backgroundTiles).IsGreater(1);
        // ASSUMPTION: a real run puts at least one auto-tile on a vertex; the fallback leaves every foreground empty.
        AssertBool(mesh.Vertices.Any(vertex => !string.IsNullOrEmpty(vertex.ForegroundTileId))).IsTrue();

        var hash = Fingerprint(mesh);
        GD.Print($"[MeshTerrainFingerprint] factory seed 4242 hash {hash}");
        AssertString(hash).IsEqual(FactoryGolden);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DictionaryRulesPathMatchesMasterFingerprint()
    {
        var adjacency = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "water" } },
            { "water", new HashSet<string> { "grass", "water" } }
        };
        var tileToTerrain = new Dictionary<string, int> { { "grass", 5 }, { "water", 0 } };
        var mesh = new IrregularMeshNs.MeshTerrainGenerator(adjacency, tileToTerrain).GenerateFromRules(3, 777);

        var terrainTypes = mesh.Vertices
            .Select(vertex => vertex.TerrainType)
            .Distinct()
            .OrderBy(type => type)
            .ToArray();
        // ASSUMPTION: a real solve on seed 777 uses both mapped terrain types; the fallback writes only type 1 here.
        AssertThat(terrainTypes).IsEqual(new[] { 0, 5 });

        var hash = Fingerprint(mesh);
        GD.Print($"[MeshTerrainFingerprint] rules seed 777 hash {hash}");
        AssertString(hash).IsEqual(RulesGolden);
    }

    private static CardSignature[] TerrainCards() => new[]
    {
        new CardSignature(new[] { 1f, 1f, 0f, -1f, 0f, 0.5f, 0f, 0f }),
        new CardSignature(new[] { -1f, -1f, 1f, 1f, 0f, -0.5f, 0f, 0f })
    };

    private static string Fingerprint(IrregularMeshNs.IrregularMesh mesh)
    {
        var text = new StringBuilder();
        text.Append(FormattableString.Invariant($"v{mesh.Vertices.Count};q{mesh.Quads.Count}\n"));
        for (var index = 0; index < mesh.Vertices.Count; index++)
        {
            var vertex = mesh.Vertices[index];
            text.Append(FormattableString.Invariant(
                $"{index}:{vertex.TerrainType}:{vertex.TileId ?? "-"}:{vertex.ForegroundTileId ?? "-"}\n"));
        }

        for (var index = 0; index < mesh.Quads.Count; index++)
        {
            text.Append(FormattableString.Invariant($"q{index}:{mesh.Quads[index].BackgroundTileId ?? "-"}\n"));
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
