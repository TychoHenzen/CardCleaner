using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A bin the player throws an emptied cardboard box into: a click while holding an empty box frees the box. A
///     cardboard box is storage and is never freed when its last item comes out, so this is the one way to be rid of
///     it.
/// </summary>
public partial class TrashCan : StaticBody3D, IInteractable
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>Only an empty cardboard box in hand can be thrown away; with anything else the aim passes on.</summary>
    public bool CanInteract => EmptyBoxInHand() != null;

    public Node3D InteractionBody => this;

    public string Prompt => "Throw the box away";

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
    }

    public void Interact()
    {
        if (EmptyBoxInHand() is { } holder)
            holder.Discard();
    }

    public void Highlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = false;
    }

    // The player's holder, when it holds a cardboard box with nothing left in it.
    private IContainerHolder? EmptyBoxInHand()
    {
        if (!IsInsideTree())
            return null;

        var holder = GetTree().GetNodesInGroup(ContainerHolder.GroupName).OfType<IContainerHolder>().FirstOrDefault();
        return holder?.Held is { Kind: CardContainerKind.Box, Remaining: 0 } ? holder : null;
    }
}
