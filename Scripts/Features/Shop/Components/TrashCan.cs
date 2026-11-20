using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A bin the player throws an emptied cardboard box into: a click while holding an empty box frees the box. A
///     cardboard box is storage and is never freed when its last item comes out, so this is the one way to be rid of
///     it. The hand router decides the click (ItemTransfer.DecideTrash); the bin itself offers none.
/// </summary>
public partial class TrashCan : StaticBody3D, IInteractable
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    public bool CanInteract => false;

    public Node3D InteractionBody => this;

    public string Prompt => string.Empty;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
    }

    // The hand router plans every click on the bin (it claims this target).
    public void Interact()
    {
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
}
