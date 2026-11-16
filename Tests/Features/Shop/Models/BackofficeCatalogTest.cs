using System.Linq;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

/// <summary>
/// The backoffice catalog is the Stock app's data: it sells stock only (a box, a pack, a booster), each a container the
/// player carries to a shelf. Furniture is sold boxed by the Furniture app (<see cref="FurnitureOrderCatalogTest"/>).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BackofficeCatalogTest
{
    private const string CatalogPath = "res://Data/Shop/BackofficeCatalog.tres";

    private static readonly string[] ExpectedIds =
        ["cardboard_box", "pack", "booster"];

    private OrderCatalog _catalog = null!;

    [BeforeTest]
    public void Setup()
    {
        _catalog = GD.Load<OrderCatalog>(CatalogPath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CatalogListsTheBoxThePackAndTheBooster()
    {
        AssertThat(_catalog).IsNotNull();
        AssertThat(_catalog.Items.Select(i => i.Id).ToArray()).IsEqual(ExpectedIds);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemHasANamePriceAndScene()
    {
        foreach (var item in _catalog.Items)
        {
            AssertBool(string.IsNullOrWhiteSpace(item.DisplayName)).IsFalse();
            AssertBool(item.Price > 0).IsTrue();
            AssertThat(item.Scene).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemsHaveNoThumbnailYet()
    {
        foreach (var item in _catalog.Items)
        {
            AssertThat(item.Thumbnail).IsNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemSceneIsAContainerThePlayerCanCarry()
    {
        foreach (var item in _catalog.Items)
        {
            var instance = item.Scene!.Instantiate<Node3D>();
            AddNode(instance);

            AssertThat(instance).IsInstanceOf<RigidBody3D>();
            AssertThat(instance).IsInstanceOf<CardContainer>();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryItemHasAnUnlockRow()
    {
        foreach (var item in _catalog.Items)
            AssertThat(UnlockCatalog.Default.RequiredLevel(item.Id)).IsNotNull();
    }
}
