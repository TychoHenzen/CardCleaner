using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class OrderTerminalTest
{
    private const int StartingBalance = 150;
    private const int CheapPrice = 100;
    private const int DearPrice = 400;

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private OrderTerminalUi _ui = null!;
    private OrderTerminal _terminal = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        var marker = new Marker3D();
        _world.AddChild(marker);
        _money = new MoneyService { StartingBalance = StartingBalance };
        _ordering = new OrderingService { DeliveryPoint = marker, SpawnRoot = _world, Money = _money };
        _ui = new OrderTerminalUi();
        _terminal = new OrderTerminal { Catalog = MakeCatalog(("cheap", CheapPrice), ("dear", DearPrice)), Ui = _ui };
        _world.AddChild(_money);
        _world.AddChild(_ordering);
        _world.AddChild(_ui);

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(_ordering);

        _world.AddChild(_terminal);
        AddNode(_world);
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UiRowsAreBuiltFromTheCatalogExport()
    {
        AssertThat(_ui.ItemRowCount).IsEqual(2);

        _ui.Populate(MakeCatalog(("a", 1), ("b", 2), ("c", 3)));

        AssertThat(_ui.ItemRowCount).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SecondTerminalWithItsOwnCatalogOrdersItsOwnItemsWithoutCodeChanges()
    {
        var workshopUi = new OrderTerminalUi();
        var workshop = new OrderTerminal { Catalog = MakeCatalog(("tool", 30)), Ui = workshopUi };
        _world.AddChild(workshopUi);
        _world.AddChild(workshop);

        workshop.Interact();
        workshopUi.PressItem(0);

        AssertThat(workshopUi.ItemRowCount).IsEqual(1);
        AssertThat(_ui.ItemRowCount).IsEqual(2);
        AssertThat(_money.Balance).IsEqual(StartingBalance - 30);
        AssertThat(_ordering.DeliveredCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractOpensTheUiWithTheCurrentBalanceAndBlocksFurtherInteraction()
    {
        AssertBool(_terminal.CanInteract).IsTrue();

        _terminal.Interact();

        AssertBool(_ui.Visible).IsTrue();
        AssertBool(_terminal.IsOpen).IsTrue();
        AssertBool(_terminal.CanInteract).IsFalse();
        AssertThat(_ui.BalanceText).IsEqual($"Balance: {StartingBalance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseHidesTheUiAndAllowsInteractionAgain()
    {
        _terminal.Interact();

        _terminal.Close();

        AssertBool(_ui.Visible).IsFalse();
        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_terminal.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseButtonClosesTheTerminal()
    {
        _terminal.Interact();

        _ui.PressClose();

        AssertBool(_terminal.IsOpen).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingAnAffordableItemChargesSpawnsAndUpdatesTheBalanceDisplay()
    {
        _terminal.Interact();

        _ui.PressItem(0);

        AssertThat(_money.Balance).IsEqual(StartingBalance - CheapPrice);
        AssertThat(_ui.BalanceText).IsEqual($"Balance: {StartingBalance - CheapPrice}");
        AssertThat(_ordering.DeliveredCount).IsEqual(1);
        AssertBool(_ui.MessageText.Contains("Ordered")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingAnUnaffordableItemShowsAMessageAndChangesNothing()
    {
        _terminal.Interact();

        _ui.PressItem(1);

        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_ordering.DeliveredCount).IsEqual(0);
        AssertBool(_ui.MessageText.Contains("Not enough money")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RapidDoubleOrderChargesAndSpawnsExactlyTwice()
    {
        _money.Add(500);
        _terminal.Interact();

        _ui.PressItem(0);
        _ui.PressItem(0);

        AssertThat(_money.Balance).IsEqual(StartingBalance + 500 - 2 * CheapPrice);
        AssertThat(_ordering.DeliveredCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeClosesTheOpenTerminal()
    {
        _terminal.Interact();

        _ui._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_ui.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EscapeDoesNothingWhileTheTerminalIsClosed()
    {
        _ui._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });

        AssertBool(_terminal.IsOpen).IsFalse();
        AssertBool(_terminal.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HighlightShowsAndClearsTheHighlightMesh()
    {
        var mesh = new MeshInstance3D { Visible = false };
        _terminal.AddChild(mesh);
        _terminal.HighlightMesh = mesh;

        _terminal.Highlight();
        AssertBool(mesh.Visible).IsTrue();

        _terminal.ClearHighlight();
        AssertBool(mesh.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalSitsOnTheInteractableLayerSeenByThePlayerRay()
    {
        AssertBool((_terminal.CollisionLayer & 4) != 0).IsTrue();
    }

    private static OrderCatalog MakeCatalog(params (string Id, int Price)[] entries)
    {
        var root = new Node3D();
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();

        var items = new OrderItem[entries.Length];
        for (var i = 0; i < entries.Length; i++)
            items[i] = new OrderItem { Id = entries[i].Id, DisplayName = entries[i].Id, Price = entries[i].Price, Scene = packed };
        return new OrderCatalog { Items = items };
    }
}
