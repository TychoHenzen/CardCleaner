using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Workshop.Components;

/// <summary>
///     Seats the workshop's credit card in a card holder once the card spawning service is available.
///     The cabinet reads its holders, so the seated card makes that holder count as filled; the
///     slot itself locks the card when the spawned body enters its area.
/// </summary>
public partial class CreditCardSeeder : Node
{
    private static readonly CardSignature CreditSignature = new() { Ordinem = 0.5f, Subsidium = 0.5f };

    /// <summary>The holder that receives the credit card.</summary>
    [Export]
    public DeckSlot? Slot { get; set; }

    /// <summary>Node that owns the card until the slot takes it over.</summary>
    [Export]
    public Node3D? SpawnParent { get; set; }

    /// <summary>The credit card once it has been spawned, otherwise null.</summary>
    public Node3D? Card { get; private set; }

    public override void _Ready()
    {
        ServiceLocator.Get<ICardSpawningService>(spawner => Callable.From(() => Seed(spawner)).CallDeferred());
    }

    private void Seed(ICardSpawningService spawner)
    {
        if (!IsInsideTree() || Card != null || Slot == null || SpawnParent == null)
            return;

        var seat = Slot.GlobalTransform;
        seat.Origin += Slot.PositionOffset;
        Card = spawner.SpawnCard(CreditSignature, seat, SpawnParent);
    }
}
