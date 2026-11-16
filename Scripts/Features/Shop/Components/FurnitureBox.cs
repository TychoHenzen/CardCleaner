using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A loose piece of furniture the player can pick up and place in the shop. Interacting hands the box to the
///     placement service, which carries it until the player puts it down.
/// </summary>
public partial class FurnitureBox : RigidBody3D, IInteractable, IDependent
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    /// <summary>Scene-tree group of every furniture box, for code that lists the unopened boxes.</summary>
    public const string GroupName = "furniture_boxes";

    private IServiceResolver? _services;
    private IFurniturePlacement? _placement;

    [Export]
    public FurnitureItem? Item { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    public bool CanInteract => Item?.Scene != null && _placement is { IsActive: false };

    public Node3D InteractionBody => this;

    public string Prompt => $"Unbox and place {Item?.DisplayName ?? "furniture"}";

    public void Inject(IServiceResolver services) => _services = services;

    public override void _EnterTree() => AddToGroup(GroupName);

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        if (_services == null)
            ILog.Error($"{Name} was not injected");
        else
            _services.Get<IFurniturePlacement>(placement => _placement = placement);
    }

    public void Interact()
    {
        if (CanInteract)
            _placement!.Begin(this);
    }

    /// <summary>
    ///     Lifts the box out of the shop while its piece is being placed, or sets it down again. A lifted box is hidden
    ///     and out of the physics space (a disabled body leaves the space), so the piece can go down where the box stood;
    ///     it stays in the tree, so a save meanwhile still records the unopened box.
    /// </summary>
    public void SetCarried(bool carried)
    {
        Visible = !carried;
        ProcessMode = carried ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
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
