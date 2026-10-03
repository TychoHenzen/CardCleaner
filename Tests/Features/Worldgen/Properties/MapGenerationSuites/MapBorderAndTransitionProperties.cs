using CardCleaner.Tests.Features.Worldgen.Support;

namespace CardCleaner.Tests.Features.Worldgen.Properties.MapGenerationSuites;

/// <summary>
///     Property-based tests for map borders, tile data and dual-grid decoration overlays (ST006 and ST007).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class MapBorderAndTransitionProperties : MapGenerationPropertyBase
{
    [TestCase]
    public void Map_AllTileIdsAreNonNull()
    {
        MapProperty(5, 15, 50, request =>
            MapInvariants.AllTileIdsNonNull(GenerateMap(request), request.Width, request.Height));
    }

    [TestCase]
    public void Map_DecorationOverlaysHaveValidBitmasks()
    {
        MapProperty(8, 15, 50, request => MapInvariants.DecorationBitmasksValid(GenerateMap(request)));
    }

    [TestCase]
    public void Map_DecorationOverlaysAreWithinMapBounds()
    {
        MapProperty(8, 15, 50, request =>
            MapInvariants.DecorationOverlaysWithinVisualGrid(GenerateMap(request), request.Width, request.Height));
    }

    [TestCase]
    public void Map_BiomeMapMatchesDimensions()
    {
        MapProperty(5, 15, 50, request =>
        {
            var map = GenerateMap(request);
            return map.BiomeMap.GetLength(0) == request.Height &&
                   map.BiomeMap.GetLength(1) == request.Width;
        });
    }

    [TestCase]
    public void Map_PassableTilesAreWithinBounds()
    {
        MapProperty(5, 15, 50, request =>
            MapInvariants.PassableTilesWithinBounds(GenerateMap(request), request.Width, request.Height));
    }
}
