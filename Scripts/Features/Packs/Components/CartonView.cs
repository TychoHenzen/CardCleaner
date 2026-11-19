using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Shows what a cardboard box holds where the packer put each item: a still copy of each booster pack and booster
///     box (<see cref="StockItemLook" />), turned to its pose, and each stack of cards as one block with a card back
///     on top. Built from scratch on every change; nothing in it collides. No node of it lives outside the tree: the
///     old copies are freed at once and each scene's template only lasts one build.
/// </summary>
public partial class CartonView : Node3D
{
    private const float Epsilon = 1e-4f;

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
        // The copies are plain meshes and labels that nothing else holds, so they go at once, not at the end of the
        // frame: a box that changes several times in one frame never keeps old copies around outside the tree.
        foreach (var child in GetChildren())
            child.Free();

        // One still copy per scene, copied again for each item of that scene and freed when this build is done.
        var looks = new Dictionary<string, StockItemLook?>();
        try
        {
            AddItems(contents, looks);
        }
        finally
        {
            foreach (var look in looks.Values)
                look?.Discard();
        }
    }

    private void AddItems(BoxContents contents, Dictionary<string, StockItemLook?> looks)
    {
        var index = 0;
        while (index < contents.Count)
        {
            if (contents.Items[index] is StoredContainer container)
            {
                AddContainer(LookOf(container.ScenePath, looks), contents.Placed[index]);
                index++;
                continue;
            }

            var end = CardRunEnd(contents, index);
            AddCardStack(contents.Placed[index], contents.Placed[end - 1].Max.Y - contents.Placed[index].Min.Y);
            index = end;
        }
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

    private void AddContainer(StockItemLook? look, PackedItem item)
    {
        if (look?.Node.Duplicate() is not Node3D copy)
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
        CardBlockLook.AddBlock(this, new Vector3(card.X, height, card.Z),
            new Transform3D(BasisOf(bottom.Pose), centre),
            _edgeMaterial ??= CardBlockLook.EdgeMaterial(), _backMaterial ??= CardBlockLook.BackMaterial());
    }

    private static StockItemLook? LookOf(string scenePath, Dictionary<string, StockItemLook?> looks)
    {
        if (looks.TryGetValue(scenePath, out var look))
            return look;

        var body = ResourceLoader.Load<PackedScene>(scenePath)?.Instantiate<Node3D>();
        look = body == null ? null : StockItemLook.Capture(body);
        body?.Free();
        looks[scenePath] = look;
        return look;
    }
}
