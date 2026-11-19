using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop roof closes the whole building: its ceiling covers every floor tile, the expansion lot included, and
/// every tile lies within reach of a ceiling light. The ceiling stays out of the navigation bake's source group, or
/// the bake would lay a walkable mesh on the roof.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopRoofSceneTest
{
    private const string FloorArt = "SI_Env_Floor_01.fbx";
    private const string CeilingMeshPath = "World/Roof/Ceiling/Mesh";
    private const string CeilingPath = "World/Roof/Ceiling";
    private const string LightsPath = "World/Roof/CeilingLights";
    private const string NavigationGroup = "shop_navigation";
    private const float Tolerance = 0.01f;

    private Node3D _shop = null!;

    [BeforeTest]
    public void Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        ShopScenePlayer.Sideline(_shop);
        AddNode(_shop);
    }

    [TestCase]
    public void TheCeilingCoversEveryFloorTile()
    {
        var ceiling = SceneNodes.Footprint((MeshInstance3D)SceneNodes.Require(_shop, CeilingMeshPath)).Grow(Tolerance);

        var tiles = FloorTiles().ToList();
        AssertThat(tiles.Count).IsGreater(0);
        var uncovered = tiles
            .Where(tile => !ceiling.Encloses(SceneNodes.Footprint(tile)))
            .Select(tile => tile.GetPath().ToString());
        AssertThat(string.Join(", ", uncovered)).IsEmpty();
    }

    [TestCase]
    public void EveryFloorTileIsWithinReachOfACeilingLight()
    {
        var lights = SceneNodes.Descendants(SceneNodes.Require(_shop, LightsPath)).OfType<OmniLight3D>().ToList();

        AssertThat(lights.Count).IsGreater(0);
        var dark = FloorTiles()
            .Where(tile => !lights.Any(light => Reaches(light, SceneNodes.Footprint(tile).GetCenter())))
            .Select(tile => tile.GetPath().ToString());
        AssertThat(string.Join(", ", dark)).IsEmpty();
    }

    [TestCase]
    public void TheCeilingIsNotNavigationSource()
    {
        var ceiling = SceneNodes.Require(_shop, CeilingPath);

        var sources = new List<string>();
        for (Node? node = ceiling; node != null && node != _shop; node = node.GetParent())
        {
            if (node.IsInGroup(NavigationGroup))
                sources.Add(node.GetPath().ToString());
        }
        AssertThat(string.Join(", ", sources)).IsEmpty();
    }

    // The placeholder of each floor slot spans the slot's whole tile, whether or not the pack art loaded.
    private IEnumerable<MeshInstance3D> FloorTiles() => SceneNodes.Descendants(_shop)
        .OfType<ShopArtSlot>()
        .Where(slot => slot.ArtPath.EndsWith(FloorArt, System.StringComparison.Ordinal))
        .Select(slot => slot.Placeholder)
        .OfType<MeshInstance3D>();

    private static bool Reaches(OmniLight3D light, Vector2 point) =>
        new Vector2(light.GlobalPosition.X, light.GlobalPosition.Z).DistanceTo(point) <= light.OmniRange;
}
