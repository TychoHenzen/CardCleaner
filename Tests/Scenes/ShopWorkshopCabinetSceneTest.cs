using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end at the workshop cabinet: the button stays unlit while only the credit card is seated, lights when a
/// card joins it in the second holder, and pressing it runs the existing session flow.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopCabinetSceneTest
{
    private const int SmallMeshRings = 3;
    private const float EyeHeight = 1.6f;
    private const float StandingDistanceToButton = 2.5f;

    private WorkshopSceneRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await WorkshopSceneRig.Create();
        _rig.Screen.MeshRings = SmallMeshRings;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ButtonIsUnlitWhileOnlyTheCreditCardIsSeated()
    {
        AssertBool(_rig.DeckHolder.HasCards).IsTrue();
        AssertBool(_rig.Button.Enabled).IsFalse();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CardInTheSecondHolderLightsTheButton()
    {
        await _rig.Place(_rig.CardHolder, new CardSignature { Febris = 0.4f });

        AssertBool(_rig.CardHolder.HasCards).IsTrue();
        AssertBool(_rig.Button.Enabled).IsTrue();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TakingTheSecondCardBackOutPutsTheButtonOutAgain()
    {
        var card = await _rig.Place(_rig.CardHolder, new CardSignature { Febris = 0.4f });

        card.EmitPickupSignal();

        AssertBool(_rig.CardHolder.HasCards).IsFalse();
        AssertBool(_rig.Button.Enabled).IsFalse();
        AssertBool(_rig.Button.ButtonMesh!.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task UnlitButtonDoesNothingWhenPressed()
    {
        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        AssertBool(_rig.DeckHolder.HasCards).IsTrue();
        AssertBool(_rig.Screen.IsInitialized).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PressingTheLitButtonRunsTheSessionAndSpendsBothCards()
    {
        await _rig.Place(_rig.CardHolder, new CardSignature { Febris = 0.4f });

        _rig.Button.Interact();
        await WorkshopSceneRig.Settle();

        AssertBool(_rig.DeckHolder.HasCards).IsFalse();
        AssertBool(_rig.CardHolder.HasCards).IsFalse();
        AssertBool(_rig.Screen.Visible).IsTrue();
        AssertBool(_rig.Screen.IsInitialized).IsTrue();
        AssertBool(_rig.Button.Enabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerCanStandAtTheEntryOnTheFloor()
    {
        var player = _rig.Shop.GetNode<CharacterBody3D>("Player");
        var probe = new ShopSceneProbe(_rig.Shop, (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape);
        var feet = _rig.Entry.GlobalPosition with { Y = 0f };

        AssertBool(probe.SupportedByFloor(feet)).IsTrue();
        AssertBool(probe.CapsuleFits(feet with { Y = probe.CapsuleCenterHeight })).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerInteractionRayReachesTheButtonFromInFrontOfTheCabinet()
    {
        var interaction = _rig.Shop.GetNode<InteractionSystem>("Player/Head/Camera3D/InteractionSystem");
        var target = _rig.Button.GetNode<CollisionShape3D>("CollisionShape3D").GlobalPosition;
        var from = new Vector3(target.X, EyeHeight, target.Z + StandingDistanceToButton);

        var hit = _rig.Shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = target, CollideWithBodies = true, CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertBool(ReferenceEquals(hit["collider"].Obj, _rig.Button)).IsTrue();
        AssertBool(from.DistanceTo(target) <= _rig.Button.InteractionRange).IsTrue();
    }
}
