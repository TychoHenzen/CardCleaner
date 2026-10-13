using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GridMapGeneratorScenarios;

/// <summary>
/// The grid map a session generates for two fixed seed cards: tile ids, player start and enemy cells must equal the
/// map recorded in GridMapGolden.txt. The file was recorded from master ab82cbc, through the session's default
/// generator, before the grid generator moved behind IMapGenerator (#284).
/// To record it again, run this suite with CARDCLEANER_RECORD_GRID_GOLDEN=1; never edit the file by hand.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GridMapGoldenTest
{
    private const string FixturePath = "res://Tests/Features/Deckbuilder/Services/GridMapGeneratorScenarios/GridMapGolden.txt";
    private const string RecordSwitch = "CARDCLEANER_RECORD_GRID_GOLDEN";

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase(Timeout = 120000)]
    [TestCategory("Unit")]
    public async Task GridMapForFixedCardsMatchesTheRecordedMap()
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

    // Generates through the session's default generator, the only grid path master has.
    private static async Task<string> GenerateAsync(List<CardSignature> cards)
    {
        ServiceLocator.ResetForTesting();
        var tileRegistry = new TileRegistry();
        ServiceLocator.Container.RegisterSingleton<ITileRegistry>(tileRegistry);
        ServiceLocator.Container.RegisterSingleton<ITileMetadataProvider>(tileRegistry);

        var session = new GameSessionService();
        // Described when raised, before exploration moves anything on the map.
        string? described = null;
        session.MapGenerated += generated => described = Describe(generated);
        AddNode(session);
        await ISceneRunner.SyncProcessFrame;

        session.StartSession(cards, new List<CardSignature> { new() });
        await ISceneRunner.SyncProcessFrame;
        await session.CurrentGenerationTask!;
        session.ResetSession();

        AssertThat(described).IsNotNull();
        return described!;
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
