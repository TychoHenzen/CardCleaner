using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the checkout counter is reachable by the player's interaction ray, scanning
/// the basket and paying by card or cash through the payment screen adds the total to the balance once, and a
/// wrong entry or a cancelled screen takes nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCheckoutSceneTest
{
    private const float StandingDistanceToCounter = 2.9f;
    private const string CounterArtPath = "res://Assets/Synty/SimpleShopInterior/SI_Prop_CheckoutCounter_01.fbx";

    private ShopCheckoutRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCheckoutRig.Create();
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CounterHasItsStatusLabelMarkersAndAPaymentScreenWiredToIt()
    {
        AssertThat(_rig.Counter.StatusLabel).IsNotNull();
        AssertThat(_rig.Counter.StatusLabel!.Text).Contains($"Balance: {_rig.Money.Balance}");
        AssertThat(CheckoutCounterLayout.MarkersNamed(_rig.Counter, "QueueSlot").Length).IsEqual(4);
        AssertThat(CheckoutCounterLayout.MarkersNamed(_rig.Counter, "ItemSpot").Length).IsEqual(4);
        AssertThat(_rig.Ui.Counter).IsEqual(_rig.Counter);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneServicesResolveTheCounterAsTheCheckoutHandoff()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previous = tree.CurrentScene;
        tree.CurrentScene = _rig.Shop;
        try
        {
            var container = new ServiceContainer();
            _rig.Shop.GetNode<AutoServiceProvider>("Services").RegisterServices(container);

            AssertBool(ReferenceEquals(container.Resolve<ICheckoutHandoff>(), _rig.Counter)).IsTrue();
        }
        finally
        {
            tree.CurrentScene = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CardPaymentOfTheTotalAddsItOnceAndSendsTheCustomerAway()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var balance = _rig.Money.Balance;
        _rig.Counter.Checkout(customer);
        _rig.ScanAll();
        var props = _rig.Props();

        AssertBool(_rig.Ui.Visible).IsTrue();
        AssertThat(_rig.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);

        await ShopCheckoutRig.Click(_rig.Ui.Keypad.DigitButton(7));
        await ShopCheckoutRig.Click(_rig.Ui.Keypad.ConfirmButton);

        AssertThat(_rig.Money.Balance).IsEqual(balance + 7);
        AssertBool(props.All(prop => !GodotObject.IsInstanceValid(prop) || prop.IsQueuedForDeletion())).IsTrue();
        AssertThat(customer.LeaveCount).IsEqual(1);
        AssertBool(_rig.Ui.Visible).IsFalse();
        AssertThat(_rig.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CashPaymentWithTheRightChangeAddsTheTotal()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4);
        var balance = _rig.Money.Balance;
        _rig.Counter.Checkout(customer);
        _rig.ScanAll();

        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(1));
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(1));
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(1));
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.ConfirmButton);

        AssertThat(_rig.Money.Balance).IsEqual(balance + 7);
        AssertThat(customer.LeaveCount).IsEqual(1);
        AssertBool(_rig.Ui.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task WrongCardTotalKeepsTheScreenOpenAndClearsTheEntry()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var balance = _rig.Money.Balance;
        _rig.Counter.Checkout(customer);
        _rig.ScanAll();

        await ShopCheckoutRig.Click(_rig.Ui.Keypad.DigitButton(8));
        await ShopCheckoutRig.Click(_rig.Ui.Keypad.ConfirmButton);

        AssertBool(_rig.Ui.Visible).IsTrue();
        AssertBool(string.IsNullOrEmpty(_rig.Ui.MessageText.Text)).IsFalse();
        AssertThat(_rig.Ui.TypedText).IsEqual(string.Empty);
        AssertThat(_rig.Money.Balance).IsEqual(balance);
        AssertThat(customer.LeaveCount).IsEqual(0);

        await ShopCheckoutRig.Click(_rig.Ui.Keypad.DigitButton(7));
        await ShopCheckoutRig.Click(_rig.Ui.Keypad.ConfirmButton);

        AssertThat(_rig.Money.Balance).IsEqual(balance + 7);
        AssertThat(customer.LeaveCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task WrongCashChangeIsRejectedAndTakesNothing()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4);
        var balance = _rig.Money.Balance;
        _rig.Counter.Checkout(customer);
        _rig.ScanAll();

        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(5));
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.ConfirmButton);

        AssertBool(_rig.Ui.Visible).IsTrue();
        AssertBool(string.IsNullOrEmpty(_rig.Ui.MessageText.Text)).IsFalse();
        AssertThat(_rig.Money.Balance).IsEqual(balance);
        AssertThat(customer.LeaveCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CancelClosesTheScreenWithoutPayingAndTheCounterReopensIt()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var balance = _rig.Money.Balance;
        _rig.Counter.Checkout(customer);
        _rig.ScanAll();

        Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = true });
        await ISceneRunner.SyncProcessFrame;
        Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = false });
        await ISceneRunner.SyncProcessFrame;

        AssertBool(_rig.Ui.Visible).IsFalse();
        AssertThat(_rig.Counter.Session!.State).IsEqual(CheckoutState.AwaitingPayment);
        AssertThat(_rig.Money.Balance).IsEqual(balance);
        AssertThat(customer.LeaveCount).IsEqual(0);

        _rig.Counter.Interact();

        AssertBool(_rig.Ui.Visible).IsTrue();
        AssertThat(_rig.Ui.TypedText).IsEqual(string.Empty);

        await ShopCheckoutRig.Click(_rig.Ui.Keypad.DigitButton(7));
        await ShopCheckoutRig.Click(_rig.Ui.Keypad.ConfirmButton);

        AssertThat(_rig.Money.Balance).IsEqual(balance + 7);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task FreeingTheScreenWhileOpenGivesTheCursorAndControlBack()
    {
        var player = _rig.Shop.GetNode<PlayerController>("Player");
        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _rig.ScanAll();
        AssertBool(_rig.Ui.Visible).IsTrue();
        AssertBool(player.ControlEnabled).IsFalse();

        _rig.Shop.RemoveChild(_rig.Ui);
        await ISceneRunner.SyncProcessFrame;

        AssertBool(player.ControlEnabled).IsTrue();
        AssertThat(_rig.Cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
        _rig.Ui.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CounterUsesAnExistingPackMeshWithAPlaceholderFallback()
    {
        var art = _rig.Counter.GetNode<ShopArtSlot>("CounterArt");

        AssertThat(art.ArtPath).IsEqual(CounterArtPath);
        AssertBool(SyntyAssets.Exists(CounterArtPath[SyntyAssets.Root.Length..])).IsTrue();
        AssertThat(art.Placeholder).IsNotNull();
        AssertBool(art.ArtLoaded || art.Placeholder!.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerInteractionRayCanReachTheCounterFromInFrontOfIt()
    {
        var player = _rig.Shop.GetNode<PlayerController>("Player");
        var interaction = player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var from = _rig.Counter.GlobalPosition + new Vector3(0f, 1.0f, StandingDistanceToCounter);
        var to = _rig.Counter.GlobalPosition + new Vector3(0f, 1.0f, 0f);

        var hit = _rig.Shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = to, CollideWithBodies = true, CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(_rig.Counter);
        AssertBool(from.DistanceTo(_rig.Counter.GlobalPosition) <= _rig.Counter.InteractionRange).IsTrue();
    }
}
