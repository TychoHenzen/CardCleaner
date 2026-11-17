using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     The curve one item rises along out of its wrapper: a quadratic from inside the wrapper, pulled toward its
///     mouth, to the spot the item was created at, turning from the wrapper's yaw to the item's own.
/// </summary>
internal readonly struct RisePath
{
    // Where an item starts inside the wrapper and where it leaves it, in the wrapper's own frame. A box and a pack
    // open at the top; a booster's cards slide out of its torn end at -Z.
    private static readonly Vector3 BoxInside = new(0f, 0.01f, 0f);
    private static readonly Vector3 BoxMouth = new(0f, 0.7f, 0f);
    private static readonly Vector3 PackMouth = new(0f, 0.46f, 0f);
    private const float PackFloor = 0.007f;
    private const float BoosterHeight = 0.045f;
    private static readonly Vector3 BoosterInside = new(0f, 0.02f, 0f);
    private static readonly Vector3 BoosterMouth = new(0f, 0.022f, -0.6f);

    private readonly Vector3 _inside;
    private readonly Vector3 _mouth;
    private readonly Quaternion _start;

    private RisePath(Vector3 inside, Vector3 mouth, Quaternion start)
    {
        _inside = inside;
        _mouth = mouth;
        _start = start;
    }

    /// <summary>
    ///     The path of the item at <paramref name="index" /> out of a wrapper resting at <paramref name="wrapper" />.
    /// </summary>
    public static RisePath For(CardContainerKind kind, int index, Transform3D wrapper) =>
        new(wrapper * InsideOf(kind, index), wrapper * MouthOf(kind), wrapper.Basis.GetRotationQuaternion());

    /// <summary>The item's pose at <paramref name="t" /> (0 inside the wrapper, 1 at <paramref name="target" />).</summary>
    public Transform3D At(float t, Transform3D target)
    {
        var u = 1f - t;
        var position = u * u * _inside + 2f * u * t * _mouth + t * t * target.Origin;
        var rotation = _start.Slerp(target.Basis.GetRotationQuaternion(), t);
        return new Transform3D(new Basis(rotation), position);
    }

    // A pack's boosters lie stacked flat inside it and leave from the top of the stack down.
    private static Vector3 InsideOf(CardContainerKind kind, int index) => kind switch
    {
        CardContainerKind.Box => BoxInside,
        CardContainerKind.Pack => new Vector3(0f, PackFloor + StackLevel(index) * BoosterHeight, 0f),
        _ => BoosterInside
    };

    private static int StackLevel(int index) =>
        CardContainerLayout.ItemsPerContainer - 1 - index % CardContainerLayout.ItemsPerContainer;

    private static Vector3 MouthOf(CardContainerKind kind) => kind switch
    {
        CardContainerKind.Box => BoxMouth,
        CardContainerKind.Pack => PackMouth,
        _ => BoosterMouth
    };
}
