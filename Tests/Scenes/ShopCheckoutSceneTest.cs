using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the checkout counter is reachable by the player's interaction ray while a
/// payment waits, scanning
/// the basket and paying by card or cash through the payment screen adds the total to the balance once, and a
/// wrong entry or a cancelled screen takes nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCheckoutSceneTest
{
    private const float StandingDistanceToCounter = 2.9f;

    // The player's eyes above the floor: the body's origin stands 0.9 m up and its Head 0.9 m above that.
    private const float EyeHeight = 1.8f;
    private const uint InteractableLayerBit = 4;
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
    public void CounterHasItsRegisterDisplayMarkersAndAPaymentScreenWiredToIt()
    {
        AssertThat(_rig.Counter.Display).IsNotNull();
        // With nobody at the counter the display says nothing; the money is on the HUD.
        AssertThat(_rig.Counter.Display!.Text).IsEqual(string.Empty);
        AssertThat(CheckoutCounterLayout.MarkersNamed(_rig.Counter, "QueueSlot").Length).IsEqual(4);
        AssertThat(CheckoutCounterLayout.MarkersNamed(_rig.Counter, "ItemSpot").Length).IsEqual(4);
        AssertThat(_rig.Ui.Counter).IsEqual(_rig.Counter);
    }

    [TestCase]
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
    public async Task CashPaymentWithTheRightChangeAddsTheTotal()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4);
        var balance = _rig.Money.Balance;
        _rig.Counter.Checkout(customer);
        _rig.ScanAll();

        // The player reads what is owed back on the screen itself: 7 due, 10 handed over, 3 to give back.
        AssertThat(_rig.Ui.ChangePanel.Readout.Text).IsEqual("Due 7   Handed 10\nGive back 3   Picked 0");

        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(1));
        AssertThat(_rig.Ui.ChangePanel.Readout.Text).IsEqual("Due 7   Handed 10\nGive back 3   Picked 1");
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(1));
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.DenominationButton(1));
        await ShopCheckoutRig.Click(_rig.Ui.ChangePanel.ConfirmButton);

        AssertThat(_rig.Money.Balance).IsEqual(balance + 7);
        AssertThat(customer.LeaveCount).IsEqual(1);
        AssertBool(_rig.Ui.Visible).IsFalse();
    }

    [TestCase]
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
    public void CounterUsesAnExistingPackMeshWithAPlaceholderFallback()
    {
        var art = _rig.Counter.GetNode<ShopArtSlot>("CounterArt");

        AssertThat(art.ArtPath).IsEqual(CounterArtPath);
        AssertBool(SyntyAssets.Exists(CounterArtPath[SyntyAssets.Root.Length..])).IsTrue();
        AssertThat(art.Placeholder).IsNotNull();
        AssertBool(art.ArtLoaded || art.Placeholder!.Visible).IsTrue();
    }

    [TestCase]
    public async Task PlayerInteractionRayReachesTheCounterFromInFrontOfItOnlyWhileAPaymentWaits()
    {
        var from = _rig.Counter.GlobalPosition + new Vector3(0f, EyeHeight, StandingDistanceToCounter);

        AssertThat(AimedFromTheFront(from)).IsNotEqual(_rig.Counter);

        _rig.Counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _rig.ScanAll();
        await ISceneRunner.SyncProcessFrame;

        AssertBool(_rig.Counter.CanInteract).IsTrue();
        AssertThat(_rig.Counter.CollisionLayer & InteractableLayerBit).IsEqual(InteractableLayerBit);
        AssertThat(AimedFromTheFront(from)).IsEqual(_rig.Counter);
        AssertBool(from.DistanceTo(_rig.Counter.GlobalPosition) <= _rig.Counter.InteractionRange).IsTrue();
    }

    // The first body the player's interaction ray meets on its way from a standing spot to the middle of the
    // counter's deck (its main collision shape), which is where a player looks to use the counter.
    private GodotObject? AimedFromTheFront(Vector3 from)
    {
        var player = _rig.Shop.GetNode<PlayerController>("Player");
        var interaction = player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var hit = _rig.Shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from,
            To = _rig.Counter.GetNode<CollisionShape3D>("CollisionShape3D").GlobalPosition,
            CollideWithBodies = true,
            CollisionMask = interaction.InteractableCollisionMask
        });
        return hit.Count == 0 ? null : hit["collider"].Obj as GodotObject;
    }
}
