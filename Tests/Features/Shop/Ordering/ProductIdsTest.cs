using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Tests.Features.Shop.Ordering;

/// <summary>Every kind of sealed product has a stable id, and the order catalog sells each kind under that id.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ProductIdsTest
{
    private const string Catalog = "res://Data/Shop/BackofficeCatalog.tres";

    [TestCase]
    public static void EachKindHasItsOwnId()
    {
        AssertThat(ProductIds.IdOf(CardContainerKind.Box)).IsEqual("cardboard_box");
        AssertThat(ProductIds.IdOf(CardContainerKind.Pack)).IsEqual("pack");
        AssertThat(ProductIds.IdOf(CardContainerKind.Booster)).IsEqual("booster");
    }

    [TestCase]
    public static void TheOrderCatalogSellsEveryKindUnderItsId()
    {
        var ids = GD.Load<OrderCatalog>(Catalog).Items.Select(item => item.Id).ToList();

        foreach (var kind in new[] { CardContainerKind.Box, CardContainerKind.Pack, CardContainerKind.Booster })
            AssertThat(ids.Contains(ProductIds.IdOf(kind))).IsTrue();
    }
}
