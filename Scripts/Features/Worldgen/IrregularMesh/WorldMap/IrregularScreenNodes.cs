using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Creates the irregular map screen's children in its viewport: the 2D camera (kept when the scene has one), the
/// debug renderer and the player sprite.
/// </summary>
internal static class IrregularScreenNodes
{
    internal static Camera2D FindOrCreateCamera(SubViewport viewport)
    {
        var camera = viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera != null) return camera;

        camera = new Camera2D { Name = "Camera2D", Enabled = true };
        viewport.AddChild(camera);
        return camera;
    }

    internal static IrregularMeshDebugRenderer CreateDebugRenderer(SubViewport viewport, bool visible)
    {
        var debugRenderer = new IrregularMeshDebugRenderer
        {
            Name = "DebugRenderer",
            Visible = visible,
            ZIndex = 90
        };
        viewport.AddChild(debugRenderer);
        return debugRenderer;
    }

    internal static Sprite2D CreatePlayerSprite(SubViewport viewport)
    {
        var playerSprite = new Sprite2D { Name = "PlayerSprite", ZIndex = 100 };
        viewport.AddChild(playerSprite);
        playerSprite.Texture = ColoredTextureFactory.Create(new Color(0, 0.8f, 0), 12, 12);
        return playerSprite;
    }
}
