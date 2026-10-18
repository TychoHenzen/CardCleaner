using CardCleaner.Tests.TestUtilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GridMapGeneratorScenarios;

/// <summary>
/// The grid map a session generates for two fixed seed cards: tile ids, player start and enemy cells must equal the
/// map recorded in GridMapGolden.txt. The file was recorded from master ab82cbc, through the session's default
/// generator, before the grid generator moved behind IMapGenerator (#284); the test now generates through
/// GridMapGenerator with the seed and size the session derives from the cards. Its start and enemies lines were
/// recorded again in #256, when MapPopulation began placing them; the tile rows are unchanged since ab82cbc.
/// To record it again, run this suite with CARDCLEANER_RECORD_GRID_GOLDEN=1; never edit the file by hand.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GridMapGoldenTest
{
    private const string FixturePath =
        "res://Tests/Features/Deckbuilder/Services/GridMapGeneratorScenarios/GridMapGolden.txt";
    private const string RecordSwitch = "CARDCLEANER_RECORD_GRID_GOLDEN";

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public static async Task GridMapForFixedCardsMatchesTheRecordedMap()
    {
        var actual = await GenerateAsync(MapCards());
        var path = ProjectSettings.GlobalizePath(FixturePath);

        if (System.Environment.GetEnvironmentVariable(RecordSwitch) == "1")
        {
            // Record only a map that a second generation reproduces.
            AssertThat(await GenerateAsync(MapCards())).IsEqual(actual);
            File.WriteAllText(path, actual);
        }

        AssertThat(actual).IsEqual(File.ReadAllText(path).Replace("\r\n", "\n"));
    }

    private static List<CardSignature> MapCards() => new()
    {
        new CardSignature(new[] { 0.2f, -0.1f, 0f, 0f, 0f, 0f, 0f, 0f }),
        new CardSignature(new[] { -0.5f, 0.4f, 0.3f, 0f, -0.2f, 0f, 0.6f, 0f })
    };

    // Generates through the grid generator the cabinet hands the session, with the seed and size the session derives.
    private static async Task<string> GenerateAsync(List<CardSignature> cards)
    {
        var tileRegistry = new TileRegistry(new GodotAssetPaths());
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
        var generator = new GridMapGenerator(
            tileRegistry, tileRegistry, biomeRegistry, CompiledTransitionPairs.Load());
        var seeds = cards.ToArray();

        var generated = await generator.GenerateAsync(new MapGenerationConfig
        {
            Size = MapSeeding.CalculateMapSize(seeds[0]),
            Seed = MapSeeding.ComputeSeedFromCards(seeds),
            MapSeeds = seeds
        });

        return Describe((SimpleMapData)((SimpleGeneratedMap)generated).RawMapData);
    }

    private static string Describe(SimpleMapData map)
    {
        var text = new StringBuilder();
        text.Append("size ").Append(map.Size.X).Append('x').Append(map.Size.Y).Append('\n');
        text.Append("start ").Append(Cell(map.PlayerStart)).Append('\n');
        text.Append("enemies ").Append(string.Join(";", map.EnemyPositions.ConvertAll(Cell))).Append('\n');
        for (var y = 0; y < map.Size.Y; y++)
        {
            var row = new string[map.Size.X];
            for (var x = 0; x < map.Size.X; x++)
                row[x] = map.TileIds[y, x] ?? "-";
            text.Append(string.Join(",", row)).Append('\n');
        }

        return text.ToString();
    }

    private static string Cell(Vector2I cell) => $"{cell.X},{cell.Y}";
}
