using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The street neighbourhood around the shop is placed across the road from the storefront: the road, kerb and
/// far sidewalk run along the street, the facades stand beyond them, and one neighbour flanks each side.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NeighbourhoodSceneTest
{
    private const string NeighbourhoodPath = "Neighbourhood";
    private const string ScenePath = "res://Scenes/Shop/Neighbourhood.tscn";

    private Node3D _shop = null!;
    private Node3D _neighbourhood = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        AddNode(_shop);
        await ShopPauseRig.SettleAsync();
        _neighbourhood = SceneNodes.Require(_shop, NeighbourhoodPath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NeighbourhoodIsInstancedOnce()
    {
        AssertThat(_neighbourhood.SceneFilePath).IsEqual(ScenePath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ContainsRoadKerbSidewalkFacadesNeighboursAndProps()
    {
        foreach (var name in new[] { "Road", "Kerb", "FarSidewalk", "NeighbourLeft", "NeighbourRight" })
            SceneNodes.Require(_neighbourhood, name);

        var facades = SceneNodes.Require(_neighbourhood, "Facades");
        AssertThat(facades.GetChildCount())
            .OverrideFailureMessage($"{facades.GetPath()} holds fewer than 3 facades")
            .IsGreaterEqual(3);

        var props = SceneNodes.Require(_neighbourhood, "Props");
        AssertThat(props.GetChildCount())
            .OverrideFailureMessage($"{props.GetPath()} holds fewer than 4 props")
            .IsGreaterEqual(4);
    }

    // Acceptance row AC-1: the street lies beyond the storefront (larger z), with the kerb between the shop
    // and the road, the far sidewalk past the road and the facades past the sidewalk, across from the shop.
    [TestCase]
    [TestCategory("Unit")]
    public void StreetRunsAcrossTheRoadInFrontOfTheStorefront()
    {
        var streetEdge = NeighbourhoodBounds.ShopAndStreet.End.Y;
        var roadZ = AssertAllBeyond("Road", streetEdge);
        var kerbZ = SceneNodes.Require(_neighbourhood, "Kerb").GlobalPosition.Z;
        AssertThat(kerbZ > streetEdge && kerbZ < roadZ.Min)
            .OverrideFailureMessage($"Kerb z {kerbZ} is not between the street edge {streetEdge} and {roadZ.Min}")
            .IsTrue();

        var sidewalkZ = AssertAllBeyond("FarSidewalk", roadZ.Max);
        AssertAllBeyond("Facades", sidewalkZ.Max);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NeighboursFlankTheShopOnEachSide()
    {
        var shop = NeighbourhoodBounds.ShopAndStreet;
        var left = SceneNodes.Require(_neighbourhood, "NeighbourLeft").GlobalPosition.X;
        var right = SceneNodes.Require(_neighbourhood, "NeighbourRight").GlobalPosition.X;
        AssertThat(left < shop.Position.X)
            .OverrideFailureMessage($"NeighbourLeft x {left} is not left of the shop and street ({shop.Position.X})")
            .IsTrue();
        AssertThat(right > shop.End.X)
            .OverrideFailureMessage($"NeighbourRight x {right} is not right of the shop and street ({shop.End.X})")
            .IsTrue();
    }

    // Every child of the group sits strictly beyond minZ; returns the nearest and farthest child z.
    private (float Min, float Max) AssertAllBeyond(string group, float minZ)
    {
        var root = SceneNodes.Require(_neighbourhood, group);
        var zs = new List<float>();
        foreach (var child in root.GetChildren())
        {
            var z = ((Node3D)child).GlobalPosition.Z;
            AssertThat(z > minZ)
                .OverrideFailureMessage($"{child.GetPath()} z {z} is not beyond {minZ}")
                .IsTrue();
            zs.Add(z);
        }

        AssertThat(zs.Count).OverrideFailureMessage($"{root.GetPath()} has no children").IsGreaterEqual(1);
        return (zs.Min(), zs.Max());
    }
}
