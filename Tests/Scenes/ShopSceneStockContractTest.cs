using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Portal.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop scene's wiring contract: the sidelined player is the seams' target, the shelf kinds join their groups,
/// the starting shelves are placed furniture, the furniture catalog sells each piece as its box and the customer
/// spawner is wired under the services. The
/// geometry contract is in <see cref="ShopSceneContractTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneStockContractTest
{
    private const string FurnitureCatalogPath = "res://Data/Shop/FurnitureOrderCatalog.tres";
    // The capsule radius in Scenes/Shop/Customer.tscn.
    private const float CustomerRadius = 0.25f;

    private Node3D _shop = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        ShopScenePlayer.Sideline(_shop);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    public void SidelinedPlayerStaysTheLiveTargetOfBothSeams()
    {
        var player = _shop.GetNode<PlayerController>("Player");

        AssertBool(player.IsInsideTree()).IsTrue();
        AssertThat(_shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam").Player).IsSame(player);
        AssertThat(_shop.GetNode<WallSeam>("World/Workshop/ReturnSeam").Player).IsSame(player);
    }

    [TestCase]
    public void ExactlyOneStorageRackJoinsTheStorageRackGroup()
    {
        AssertThat(_shop.GetTree().GetNodesInGroup(CardShelf.StorageGroupName).Count).IsEqual(1);
    }

    [TestCase]
    public void EverySalesShelfGroupMemberIsAShelfAndNoStorageRackJoinsIt()
    {
        // Sales stock is listed through sales_shelves, so a storage rack must never join it.
        foreach (var node in _shop.GetTree().GetNodesInGroup(CardShelf.SalesGroupName))
        {
            AssertThat(node).IsInstanceOf<CardShelf>();
            AssertThat(((CardShelf)node).Kind).IsNotEqual(ShelfKind.StorageRack);
        }
    }

    [TestCase]
    public void AProductShelfStandsUnderWorld()
    {
        var shelves = Descendants(_shop.GetNode("World")).OfType<CardShelf>().ToArray();

        AssertThat(shelves.Count(shelf => shelf.Kind == ShelfKind.ProductShelf)).IsEqual(1);
    }

    [TestCase]
    public static void FurnitureCatalogSellsEveryFurnitureItemAsItsBox()
    {
        var items = GD.Load<OrderCatalog>(FurnitureCatalogPath).Items;

        foreach (var id in new[] { "shelf_unit", "product_shelf", "storage_rack", "bulk_packing_station" })
        {
            var item = items.FirstOrDefault(entry => entry.Id == id);
            AssertThat(item?.Scene).IsNotNull();
            var box = item!.Scene!.Instantiate();
            AssertThat(box).IsInstanceOf<FurnitureBox>();
            AssertThat(((FurnitureBox)box).Item?.Id).IsEqual(id);
            box.Free();
        }
    }

    [TestCase]
    public void TheStartingShelvesArePlacedFurnitureThatFitsTheFloor()
    {
        // Placed furniture can be moved, packed and saved like a bought piece, so the starting shelves are too.
        var pieces = _shop.GetNode("World/Furniture").GetChildren().OfType<PlacedFurniture>().ToList();
        AssertThat(pieces.Select(piece => piece.Name.ToString()))
            .ContainsExactly("CardShelf", "ProductShelf", "StorageRack");

        var standing = new List<FurnitureRect>();
        foreach (var piece in pieces)
        {
            AssertThat(piece.GetNodeOrNull<CardShelf>(PlacedFurniture.ContentName)).IsNotNull();
            AssertBool(FurniturePlacementModel.IsValid(piece.Item!.Footprint, piece.Pose, standing, FurnitureFloor.Shop))
                .IsTrue();
            standing.Add(piece.Rect);
        }
    }

    [TestCase]
    public void NoStartingFurnitureStandsInTheCheckoutQueue()
    {
        // Waiting customers step from queue spot to queue spot, so the lane from the first spot to the last, as wide as
        // a customer, has to be clear of furniture.
        var counter = _shop.GetNode<Node3D>("World/Storefront/CheckoutCounter");
        var spots = counter.GetChildren().OfType<Marker3D>()
            .Where(marker => marker.Name.ToString().StartsWith("QueueSlot", System.StringComparison.Ordinal))
            .OrderBy(marker => marker.Name.ToString(), System.StringComparer.Ordinal)
            .Select(marker => marker.GlobalPosition)
            .ToArray();
        var pieces = _shop.GetNode("World/Furniture").GetChildren().OfType<PlacedFurniture>().ToList();

        AssertThat(spots.Length).IsGreater(1);
        for (var i = 0; i + 1 < spots.Length; i++)
        {
            var lane = Lane(spots[i], spots[i + 1]);
            foreach (var piece in pieces)
                AssertBool(piece.Rect.Overlaps(lane))
                    .OverrideFailureMessage($"{piece.Name} stands in the queue between spots {i} and {i + 1}")
                    .IsFalse();
        }
    }

    [TestCase]
    public void CustomerSpawnerIsWiredUnderServices()
    {
        var spawner = _shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");

        AssertThat(spawner.CustomerScene).IsNotNull();
        AssertThat(spawner.CustomersPerDay).IsEqual(6);
        AssertThat(spawner.SpawnRoot).IsSame(_shop.GetNode("World"));
        AssertThat(spawner.SpawnMarker).IsSame(_shop.GetNode("World/Markers/CustomerSpawn"));
        AssertThat(spawner.DespawnMarker).IsSame(_shop.GetNode("World/Markers/CustomerDespawn"));
        AssertThat(spawner.CounterMarker).IsSame(_shop.GetNode("World/Markers/CustomerCounter"));

        var customer = spawner.CustomerScene!.Instantiate<Customer>();
        AssertThat(customer.GetNode("Agent")).IsInstanceOf<NavigationAgent3D>();
        AssertThat(customer.CollisionLayer).IsEqual(0u);
        AssertThat(customer.CollisionMask).IsEqual(0u);
        customer.Free();
    }

    // The floor a customer covers walking straight from one spot to the next.
    private static FurnitureRect Lane(Vector3 from, Vector3 to) => new(
        Mathf.Min(from.X, to.X) - CustomerRadius, Mathf.Min(from.Z, to.Z) - CustomerRadius,
        Mathf.Max(from.X, to.X) + CustomerRadius, Mathf.Max(from.Z, to.Z) + CustomerRadius);

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var grandchild in Descendants(child))
                yield return grandchild;
        }
    }
}
