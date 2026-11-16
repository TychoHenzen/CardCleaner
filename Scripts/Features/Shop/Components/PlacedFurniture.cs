using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A piece of furniture standing on the shop floor. The wrapper is a StaticBody3D with the item's box as its only
///     collider, so the navigation bake sees it as an obstacle (it parses static colliders only). The real scene
///     is its child named <see cref="ContentName" />. A piece is either built by <see cref="Create" /> (a placed
///     box, a restored record) or authored in a scene with its <see cref="Item" /> and a Content instance; either
///     way <see cref="_Ready" /> gives it the collider and freezes the content, and its yaw is its quarter turns.
/// </summary>
public partial class PlacedFurniture : StaticBody3D
{
    public const string ContentName = "Content";

    /// <summary>The prefix of a generated piece name; the number after it is the first one free under the root.</summary>
    public const string NamePrefix = "Furniture";

    private const string FootprintName = "Footprint";
    private const uint FloorCollisionLayer = 1;

    /// <summary>The catalog entry of the piece. Set by <see cref="Create" />, or in the scene for an authored piece.</summary>
    [Export]
    public FurnitureItem? Item { get; set; }

    public int QuarterTurns { get; private set; }

    public FurniturePose Pose => new(Position.X, Position.Z, QuarterTurns);

    public FurnitureRect Rect => FurniturePlacementModel.RectOf(Item!.Footprint, Pose);

    /// <summary>True when the piece is a shelf that still holds stock; such a piece cannot be picked up.</summary>
    public bool HoldsStock => GetNodeOrNull<CardShelf>(ContentName) is { StockedCount: > 0 };

    public bool CanPickUp => !HoldsStock;

    /// <summary>True while a placement session carries the piece.</summary>
    public bool IsCarried { get; private set; }

    /// <summary>
    ///     Marks the piece as carried or put down. A carried piece is hidden and leaves the physics space, its content's
    ///     own bodies, slots and price tags included, so the placement probe and the interaction ray pass through the
    ///     spot it left; it stays in the tree and in the saved furniture. The wrapper also leaves the collision layer,
    ///     because the navigation bake reads the layer of a node, not its place in the space.
    /// </summary>
    public void SetCarried(bool carried)
    {
        IsCarried = carried;
        Visible = !carried;
        CollisionLayer = carried ? 0 : FloorCollisionLayer;
        // A disabled CollisionObject3D is removed from the space (its default DisableMode), for every body under it.
        ProcessMode = carried ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
    }

    /// <summary>
    ///     Builds the wrapper for an item at a pose. Returns null when the item has no scene that is a Node3D.
    /// </summary>
    public static PlacedFurniture? Create(FurnitureItem item, FurniturePose pose)
    {
        var instance = item.Scene?.Instantiate();
        if (instance is not Node3D content)
        {
            instance?.Free();
            ILog.Error($"Furniture {item.Id} has no scene that is a Node3D");
            return null;
        }

        var placed = new PlacedFurniture { Item = item };
        content.Name = ContentName;
        placed.AddChild(content);
        placed.Frame();
        placed.Place(pose);
        return placed;
    }

    public override void _Ready()
    {
        if (Item == null)
        {
            ILog.Error($"{Name} is placed furniture with no Item");
            return;
        }

        Frame();
        QuarterTurns = FurniturePlacementModel.Turn((int)Mathf.Round(Rotation.Y / (Mathf.Pi / 2f)), 0);
    }

    /// <summary>
    ///     Adds the piece under <paramref name="root" />. It keeps <paramref name="savedName" /> when that is a valid
    ///     node name no sibling has, so a restored piece has the node path it was saved with, and with it the save id
    ///     of every price tag on it. Otherwise it takes the first free <see cref="NamePrefix" /> name.
    /// </summary>
    public void StandUnder(Node root, string? savedName = null)
    {
        Name = savedName is { Length: > 0 } && savedName.ValidateNodeName() == savedName && !root.HasNode(savedName)
            ? savedName
            : FreeName(root);
        root.AddChild(this);
    }

    private static string FreeName(Node root)
    {
        var index = 0;
        while (root.HasNode($"{NamePrefix}{index}"))
            index++;
        return $"{NamePrefix}{index}";
    }

    // The collider and the frozen content, built once: an authored piece has neither until it is ready.
    private void Frame()
    {
        CollisionLayer = IsCarried ? 0 : FloorCollisionLayer;
        CollisionMask = 0;
        if (GetNodeOrNull(FootprintName) == null && Item != null)
        {
            AddChild(new CollisionShape3D
            {
                Name = FootprintName,
                Shape = new BoxShape3D { Size = new Vector3(Item.Width, Item.Height, Item.Depth) },
                Position = new Vector3(0f, Item.Height / 2f, 0f),
            });
        }

        if (GetNodeOrNull(ContentName) is RigidBody3D body)
            body.Freeze = true;
    }

    /// <summary>The floor rects of the pieces standing under <paramref name="root" />, not the carried one.</summary>
    public static List<FurnitureRect> StandingRects(Node? root) =>
        root == null
            ? []
            : root.GetChildren().OfType<PlacedFurniture>()
                .Where(placed => placed is { Item: not null, IsCarried: false })
                .Select(placed => placed.Rect)
                .ToList();

    /// <summary>Puts the piece at a pose: its floor position and its yaw in quarter turns.</summary>
    public void Place(FurniturePose pose)
    {
        Position = new Vector3(pose.X, 0f, pose.Z);
        Rotation = new Vector3(0f, pose.QuarterTurns * Mathf.Pi / 2f, 0f);
        QuarterTurns = pose.QuarterTurns;
    }
}
