using System;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     The look of a <see cref="CardBundle" />: a deck is a block of cards with a strap crossed round it; a pile is a
///     cream mound covered in card backs lying every which way, as many as the pile holds up to
///     <see cref="MostShownCards" />. Every part is built under the bundle's look node and collides with nothing.
/// </summary>
internal static class CardBundleLook
{
    /// <summary>A pile shows at most this many cards on its surface; the mound stands for the rest.</summary>
    public const int MostShownCards = 360;

    private const int DomeSegments = 12;
    private const float StrapWidth = 0.025f;
    private const float StrapThickness = 0.002f;

    // A card on the pile lies this much flatter than the mound under it, so the cards at the rim do not stand on edge.
    private const float FlattenTowardUp = 0.6f;
    private const float MostTiltRadians = 0.35f;
    private const float CardLift = 0.002f;
    private const float CoreShare = 0.95f;
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

    /// <summary>
    ///     A mound of <paramref name="radius" /> and <paramref name="height" /> standing on the origin, covered in up
    ///     to <see cref="MostShownCards" /> of the <paramref name="count" /> cards. The same <paramref name="seed" />
    ///     lays the cards the same way.
    /// </summary>
    public static void AddPile(Node3D parent, float radius, float height, int count, int seed)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = "Mound",
            Mesh = new SphereMesh
            {
                Radius = radius * CoreShare,
                Height = height * CoreShare,
                IsHemisphere = true,
            },
            MaterialOverride = CardBlockLook.EdgeMaterial(),
        });

        var shown = Math.Min(count, MostShownCards);
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new PlaneMesh
            {
                Size = new Vector2(CardBundleRules.CardSize.X, CardBundleRules.CardSize.Z),
                Material = CardBlockLook.BackMaterial(twoSided: true),
            },
            InstanceCount = shown,
        };
        var random = new Random(seed);
        for (var i = 0; i < shown; i++)
            multimesh.SetInstanceTransform(i, CardOnDome(random, radius, height, i));
        parent.AddChild(new MultiMeshInstance3D { Name = "Cards", Multimesh = multimesh });
    }

    private static void AddStrap(Node3D parent, Vector3 size, Transform3D at, Material material) =>
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            Transform = at,
        });

    // A card lying on the dome at a random spot (even over the base area), turned at random about the dome's normal
    // there and tilted a little. Later cards lie a hair higher, so overlapping cards never flicker.
    private static Transform3D CardOnDome(Random random, float radius, float height, int index)
    {
        var angle = (float)random.NextDouble() * MathF.Tau;
        var reach = MathF.Sqrt((float)random.NextDouble());
        var x = reach * radius * MathF.Cos(angle);
        var z = reach * radius * MathF.Sin(angle);
        var y = height * MathF.Sqrt(Math.Max(0f, 1f - reach * reach));

        var normal = new Vector3(x / (radius * radius), y / (height * height), z / (radius * radius)).Normalized();
        var up = (normal + Vector3.Up * FlattenTowardUp).Normalized();
        var side = up.Cross(Vector3.Forward).Normalized();
        var basis = new Basis(side, up, side.Cross(up));
        basis = new Basis(up, (float)random.NextDouble() * MathF.Tau) * basis;
        var tiltAxis = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
        if (tiltAxis.LengthSquared() > 1e-6f)
            basis = new Basis(tiltAxis.Normalized(), ((float)random.NextDouble() - 0.5f) * MostTiltRadians) * basis;

        var lift = CardLift * (1f + index % 4);
        return new Transform3D(basis.Orthonormalized(), new Vector3(x, y, z) + up * lift);
    }
}
