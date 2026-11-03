using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class SlotPricingTest
{
    private const string CardSlotScene = "res://Scenes/UI/CardSlot.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";

    private DeckSlot _slot = null!;
    private Label3D _label = null!;
    private SlotPricing _pricing = null!;

    [BeforeTest]
    public async Task Setup()
    {
        TestServices.Reset();
        AddNode(new Node3D());
        _slot =AddNode(GD.Load<PackedScene>(CardSlotScene).Instantiate<DeckSlot>());
        _label = AddNode(new Label3D());
        _pricing = AddNode(new SlotPricing { Slot = _slot, TagLabel = _label });
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnEmptySlotShowsNoTagAndCannotBeInteracted()
    {
        AssertThat(_label.Text).IsEqual(string.Empty);
        AssertBool(_pricing.CanInteract).IsFalse();
        AssertThat(_pricing.AskingValue).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AStockedCardShowsTheReadout()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(card);

        AssertThat(_label.Text).IsEqual(AskingPrice.Readout(CardValue.MinimumCardPrice, 0));
        AssertThat(_pricing.AskingValue).IsEqual(CardValue.MinimumCardPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractStepsTheMarkupAndRepaintsTheLabel()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(card);

        _pricing.Interact();

        AssertThat(_pricing.MarkupPercent).IsEqual(10);
        AssertThat(_label.Text).IsEqual(AskingPrice.Readout(CardValue.MinimumCardPrice, 10));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractWrapsAfterTheTopStep()
    {
        _pricing.SetMarkup(50);

        _pricing.Interact();

        AssertThat(_pricing.MarkupPercent).IsEqual(-20);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnOffLadderMarkupIsReset()
    {
        _pricing.SetMarkup(7);

        AssertThat(_pricing.MarkupPercent).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheTagFollowsTheSlotWhenTheStockLeaves()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(card);

        _slot.ReleaseCard(card);

        AssertThat(_label.Text).IsEqual(string.Empty);
        AssertBool(_pricing.CanInteract).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheValueReadsTheCurrentStock()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature().With(Element.Lumines, 0.4f)));
        _slot.TryLock(card);

        AssertBool(_pricing.MarketValue >= CardValue.SpecialMultiplier * CardValue.MinimumCardPrice).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AMarkupSurvivesTheStockChanging()
    {
        var first = AddNode(ShopTestCards.Create(new CardSignature()));
        var second = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(first);
        _pricing.SetMarkup(30);

        _slot.ReleaseCard(first);
        _slot.TryLock(second);

        AssertThat(_pricing.MarkupPercent).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ABoosterShowsTheProductPrice()
    {
        _slot.Kind = ShelfKind.ProductShelf;
        var booster = AddNode(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());
        AssertBool(_slot.TryLock(booster)).IsTrue();

        AssertThat(_label.Text).IsEqual(AskingPrice.Readout(ProductMarketPrices.BoosterPrice, 0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ABoxShowsNoTag()
    {
        _slot.Kind = ShelfKind.StorageRack;
        var box = AddNode(GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>());
        AssertBool(_slot.TryLock(box)).IsTrue();

        AssertThat(_label.Text).IsEqual(string.Empty);
        AssertBool(_pricing.CanInteract).IsFalse();
        AssertThat(_pricing.AskingValue).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HoldsIsTrueOnlyForTheStockedBody()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        var other = AddNode(new RigidBody3D());
        _slot.TryLock(card);

        AssertBool(_pricing.Holds(card)).IsTrue();
        AssertBool(_pricing.Holds(other)).IsFalse();
    }
}
