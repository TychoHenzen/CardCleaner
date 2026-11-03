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
/// the backoffice catalog orders the new furniture and the customer spawner is wired under the services. The
/// geometry contract is in <see cref="ShopSceneContractTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneStockContractTest
{
    private const string BackofficeCatalogPath = "res://Data/Shop/BackofficeCatalog.tres";

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
    [TestCategory("Unit")]
    public void SidelinedPlayerStaysTheLiveTargetOfBothSeams()
    {
        var player = _shop.GetNode<PlayerController>("Player");

        AssertBool(player.IsInsideTree()).IsTrue();
        AssertThat(_shop.GetNode<WallSeam>("World/Markers/SeamLocation/Seam").Player).IsSame(player);
        AssertThat(_shop.GetNode<WallSeam>("World/Workshop/ReturnSeam").Player).IsSame(player);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExactlyOneStorageRackJoinsTheStorageRackGroup()
    {
        AssertThat(_shop.GetTree().GetNodesInGroup(CardShelf.StorageGroupName).Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public void AProductShelfStandsUnderWorld()
    {
        var shelves = Descendants(_shop.GetNode("World")).OfType<CardShelf>().ToArray();

        AssertThat(shelves.Count(shelf => shelf.Kind == ShelfKind.ProductShelf)).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BackofficeCatalogOrdersTheProductShelfAndTheStorageRack()
    {
        var items = GD.Load<OrderCatalog>(BackofficeCatalogPath).Items;

        foreach (var id in new[] { "product_shelf", "storage_rack" })
        {
            var item = items.FirstOrDefault(entry => entry.Id == id);
            AssertThat(item).IsNotNull();
            AssertThat(item!.Scene).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
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
