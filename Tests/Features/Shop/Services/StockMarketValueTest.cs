using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class StockMarketValueTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";

    [TestCase]
    [TestCategory("Unit")]
    public void AZeroSignatureCardIsWorthTheMinimum()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));

        AssertThat(StockMarketValue.ForCard(card)).IsEqual(CardValue.MinimumCardPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ASpecialCardIsWorthAtLeastTheMultiplier()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature().With(Element.Lumines, 0.4f)));

        AssertBool(StockMarketValue.ForCard(card) >= CardValue.SpecialMultiplier * CardValue.MinimumCardPrice).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACardWithoutASignatureIsWorthZero()
    {
        var card = AddNode(new CardController { Name = "Card1" });

        AssertThat(StockMarketValue.ForCard(card)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NullIsWorthZero()
    {
        AssertThat(StockMarketValue.ForCard(null)).IsEqual(0);
        AssertThat(StockMarketValue.For(null)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BoosterAndPackUseTheProductPrices()
    {
        var booster = AddNode(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());
        var pack = AddNode(GD.Load<PackedScene>(PackScene).Instantiate<CardContainer>());

        AssertThat(StockMarketValue.For(booster)).IsEqual(ProductMarketPrices.BoosterPrice);
        AssertThat(StockMarketValue.For(pack)).IsEqual(ProductMarketPrices.PackPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ABoxIsWorthZeroAndDoesNotThrow()
    {
        var box = AddNode(GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>());

        AssertThat(StockMarketValue.For(box)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnyOtherBodyIsWorthZero()
    {
        var body = AddNode(new RigidBody3D());

        AssertThat(StockMarketValue.For(body)).IsEqual(0);
    }
}
