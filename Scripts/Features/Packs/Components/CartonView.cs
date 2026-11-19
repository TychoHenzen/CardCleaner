using System.Collections.Generic;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Shows what a cardboard box holds where the packer put each item: a still copy of each booster pack and booster
///     box (<see cref="StockItemLook" />), turned to its pose, and each stack of cards as one block with a card back
///     on top. Built from scratch on every change; nothing in it collides.
/// </summary>
public partial class CartonView : Node3D
{
    private const string CardBackPath = "res://Assets/Graphics/Card back.png";
    private const float Epsilon = 1e-4f;
    private const float BackLift = 0.0005f;
    private static readonly Color CardEdgeColor = new(0.93f, 0.91f, 0.86f);
    private static readonly Color PlainBackColor = new(0.16f, 0.2f, 0.36f);

    // One still copy per scene, copied again for each item; the copies are freed with the view.
    private readonly Dictionary<string, StockItemLook?> _looks = new();
    private StandardMaterial3D? _edgeMaterial;
    private StandardMaterial3D? _backMaterial;

    /// <summary>The rotation that turns an item from lying flat into <paramref name="pose" />.</summary>
    public static Basis BasisOf(ItemPose pose)
    {
        var quarter = Mathf.Pi / 2f;
        return pose switch
        {
            ItemPose.Turned => new Basis(Vector3.Up, quarter),
            ItemPose.OnEnd => new Basis(Vector3.Right, -quarter),
            ItemPose.OnEndTurned => new Basis(Vector3.Up, quarter) * new Basis(Vector3.Right, -quarter),
            _ => Basis.Identity
        };
    }

    /// <summary>Replaces what the view shows with <paramref name="contents" />.</summary>
    public void Show(BoxContents contents)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var index = 0;
        while (index < contents.Count)
        {
            if (contents.Items[index] is StoredContainer container)
            {
                AddContainer(container.ScenePath, contents.Placed[index]);
                index++;
                continue;
            }

            var end = CardRunEnd(contents, index);
            AddCardStack(contents.Placed[index], contents.Placed[end - 1].Max.Y - contents.Placed[index].Min.Y);
            index = end;
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete)
            return;

        foreach (var look in _looks.Values)
            look?.Discard();
        _looks.Clear();
    }

    // The end of the run of cards starting at start: each card lies straight on the one before it.
    private static int CardRunEnd(BoxContents contents, int start)
    {
        var end = start + 1;
        while (end < contents.Count && contents.Items[end] is StoredCard && OnTop(contents.Placed[end - 1],
                   contents.Placed[end]))
            end++;

        return end;
    }

    private static bool OnTop(PackedItem below, PackedItem above) =>
        above.Pose == below.Pose && Mathf.Abs(above.Min.X - below.Min.X) < Epsilon
                                 && Mathf.Abs(above.Min.Z - below.Min.Z) < Epsilon
                                 && Mathf.Abs(above.Min.Y - below.Max.Y) < Epsilon;

    private void AddContainer(string scenePath, PackedItem item)
    {
        if (LookOf(scenePath) is not { } look || look.Node.Duplicate() is not Node3D copy)
            return;

        // The copy's origin is the middle of its base, so it is lowered by half its height from the item's centre.
        var basis = BasisOf(item.Pose);
        var halfHeight = new Vector3(0f, ItemShapes.Size(item.Shape).Y / 2f, 0f);
        copy.Transform = new Transform3D(basis, CartonSpace.CenterOf(item) - basis * halfHeight);
        AddChild(copy);
    }

    private void AddCardStack(PackedItem bottom, float height)
    {
        var centre = CartonSpace.CenterOf(bottom);
        centre.Y = CartonSpace.Floor + bottom.Min.Y + height / 2f;
        var card = ItemShapes.Size(ItemShape.Card);
        var basis = BasisOf(bottom.Pose);
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(card.X, height, card.Z) },
            MaterialOverride = _edgeMaterial ??= new StandardMaterial3D { AlbedoColor = CardEdgeColor },
            Transform = new Transform3D(basis, centre),
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(card.X, card.Z) },
            MaterialOverride = _backMaterial ??= BackMaterial(),
            Transform = new Transform3D(basis, centre + Vector3.Up * (height / 2f + BackLift)),
        });
    }

    private static StandardMaterial3D BackMaterial()
    {
        var material = new StandardMaterial3D();
        if (ResourceLoader.Exists(CardBackPath))
            material.AlbedoTexture = ResourceLoader.Load<Texture2D>(CardBackPath);
        else
            material.AlbedoColor = PlainBackColor;
        return material;
    }

    private StockItemLook? LookOf(string scenePath)
    {
        if (_looks.TryGetValue(scenePath, out var look))
            return look;

        var body = ResourceLoader.Load<PackedScene>(scenePath)?.Instantiate<Node3D>();
        look = body == null ? null : StockItemLook.Capture(body);
        body?.Free();
        _looks[scenePath] = look;
        return look;
    }
}
