using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     The look of cards lying in one neat stack, drawn as a single block instead of one body per card: a cream block
///     as tall as the stack, with a card back on its top face. Nothing in it collides.
/// </summary>
public static class CardBlockLook
{
    private const string CardBackPath = "res://Assets/Graphics/Card back.png";
    private const float BackLift = 0.0005f;
    private static readonly Color CardEdgeColor = new(0.93f, 0.91f, 0.86f);
    private static readonly Color PlainBackColor = new(0.16f, 0.2f, 0.36f);

    /// <summary>The cream of the cards' edges.</summary>
    public static StandardMaterial3D EdgeMaterial() => new() { AlbedoColor = CardEdgeColor };

    /// <summary>The card back, or a plain blue when the art is not there (a clone without the assets).</summary>
    public static StandardMaterial3D BackMaterial(bool twoSided = false)
    {
        var material = new StandardMaterial3D();
        if (ResourceLoader.Exists(CardBackPath))
            material.AlbedoTexture = ResourceLoader.Load<Texture2D>(CardBackPath);
        else
            material.AlbedoColor = PlainBackColor;
        if (twoSided)
            material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return material;
    }

    /// <summary>
    ///     Adds a block of <paramref name="size" /> centred at <paramref name="at" /> under <paramref name="parent" />,
    ///     with a card back on its top face.
    /// </summary>
    public static void AddBlock(Node3D parent, Vector3 size, Transform3D at, Material edge, Material back)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = edge,
            Transform = at,
        });
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(size.X, size.Z) },
            MaterialOverride = back,
            Transform = at.TranslatedLocal(Vector3.Up * (size.Y / 2f + BackLift)),
        });
    }
}
