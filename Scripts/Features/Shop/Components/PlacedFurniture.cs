using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A piece of furniture standing on the shop floor. The wrapper is a StaticBody3D with the item's box as its only
///     collider, so the navigation bake sees it as an obstacle (it parses static colliders only). The real scene
///     is its child named <see cref="ContentName" />.
/// </summary>
public partial class PlacedFurniture : StaticBody3D
{
    public const string ContentName = "Content";

    private const uint FloorCollisionLayer = 1;

    public FurnitureItem? Item { get; private set; }

    public int QuarterTurns { get; private set; }

    public FurniturePose Pose => new(Position.X, Position.Z, QuarterTurns);

    public FurnitureRect Rect => FurniturePlacementModel.RectOf(Item!.Footprint, Pose);

    /// <summary>True when the piece is a shelf that still holds stock; such a piece cannot be picked up.</summary>
    public bool HoldsStock => GetNodeOrNull<CardShelf>(ContentName) is { StockedCount: > 0 };

    public bool CanPickUp => !HoldsStock;

    /// <summary>True while a placement session carries the piece.</summary>
    public bool IsCarried { get; private set; }

    /// <summary>
    ///     Marks the piece as carried or put down. A carried piece is hidden and leaves the collision layer, so the
    ///     navigation bake and the physics probes ignore it, yet it stays in the tree and in the saved furniture.
    /// </summary>
    public void SetCarried(bool carried)
    {
        IsCarried = carried;
        Visible = !carried;
        CollisionLayer = carried ? 0 : FloorCollisionLayer;
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

        var placed = new PlacedFurniture { Item = item, CollisionLayer = FloorCollisionLayer, CollisionMask = 0 };
        placed.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(item.Width, item.Height, item.Depth) },
            Position = new Vector3(0f, item.Height / 2f, 0f),
        });

        if (content is RigidBody3D body)
        {
            body.Freeze = true;
        }

        content.Name = ContentName;
        placed.AddChild(content);
        placed.Place(pose);
        return placed;
    }

    /// <summary>
    ///     Adds the piece under <paramref name="root" /> with a name made from its position among the children, so its
    ///     node path, and with it the save id of every price tag on it, is the same after a reload.
    /// </summary>
    public void StandUnder(Node root)
    {
        Name = $"Furniture{root.GetChildCount()}";
        root.AddChild(this);
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
