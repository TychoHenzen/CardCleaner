using System;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     The look of a deck (a block of cards with a strap crossed round it) and the dome a pile's collision follows.
///     A pile's own look is <see cref="PileLook" />. Every part is built under the bundle's look node and collides with
///     nothing.
/// </summary>
internal static class CardBundleLook
{
    private const int DomeSegments = 12;
    private const float StrapWidth = 0.025f;
    private const float StrapThickness = 0.002f;

    private static readonly Color StrapColor = new(0.35f, 0.12f, 0.1f);
    private static readonly float[] DomeRings = [1f, 0.85f, 0.6f, 0.3f];

    /// <summary>A block of <paramref name="size" /> standing on the origin, with a strap crossed round it.</summary>
    public static void AddDeck(Node3D parent, Vector3 size)
    {
        var centre = new Transform3D(Basis.Identity, new Vector3(0f, size.Y / 2f, 0f));
        CardBlockLook.AddBlock(parent, size, centre, CardBlockLook.EdgeMaterial(), CardBlockLook.BackMaterial());

        var strap = new StandardMaterial3D { AlbedoColor = StrapColor };
        var around = size.Y + 2f * StrapThickness;
        AddStrap(parent, new Vector3(size.X + 2f * StrapThickness, around, StrapWidth), centre, strap);
        AddStrap(parent, new Vector3(StrapWidth, around, size.Z + 2f * StrapThickness), centre, strap);
    }

    /// <summary>
    ///     The points of a dome of <paramref name="radius" /> and <paramref name="height" /> standing on the origin
    ///     (half an ellipsoid), for a convex collision shape: a few rings of <see cref="DomeSegments" /> points and
    ///     the top.
    /// </summary>
    public static Vector3[] DomePoints(float radius, float height)
    {
        var points = new Vector3[DomeRings.Length * DomeSegments + 1];
        var i = 0;
        foreach (var ring in DomeRings)
        {
            var y = height * MathF.Sqrt(1f - ring * ring);
            for (var s = 0; s < DomeSegments; s++)
            {
                var angle = s * MathF.Tau / DomeSegments;
                points[i++] = new Vector3(ring * radius * MathF.Cos(angle), y, ring * radius * MathF.Sin(angle));
            }
        }

        points[i] = new Vector3(0f, height, 0f);
        return points;
    }

    private static void AddStrap(Node3D parent, Vector3 size, Transform3D at, Material material) =>
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            Transform = at,
        });
}
