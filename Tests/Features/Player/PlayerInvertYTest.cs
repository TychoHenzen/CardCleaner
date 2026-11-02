using System.Reflection;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Player.Controllers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Player;

/// <summary>
/// Drives the real PlayerController's mouse look with a positive vertical delta and reads the head pitch back. The
/// settings come from a ServiceHost holding a GameSettings node, so the controller's own _Ready resolves them.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlayerInvertYTest
{
    private const float VerticalDelta = 10f;

    [TestCase]
    [TestCategory("Unit")]
    public void InvertY_true_raises_pitch_on_positive_delta()
    {
        var headPitch = HeadPitchAfterMouseMove(invertY: true);

        Assertions.AssertThat(headPitch).IsGreater(0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InvertY_false_lowers_pitch_on_positive_delta()
    {
        var headPitch = HeadPitchAfterMouseMove(invertY: false);

        Assertions.AssertThat(headPitch).IsLess(0f);
    }

    private static float HeadPitchAfterMouseMove(bool invertY)
    {
        var settings = new GameSettings { InvertY = invertY };
        var host = new ServiceHost();
        host.Container.RegisterSingleton<IGameSettings>(settings);

        var player = BuildPlayer();
        try
        {
            player.Inject(host);
            // Built outside the tree on purpose: once it enters the tree the Services autoload injects its own host
            // over this one before _Ready, so the controller would never see the GameSettings above. Calling _Ready
            // directly is what CardControllerTestBase does for the same reason.
            player._Ready();

            var onMouseMoved = typeof(PlayerController).GetMethod("OnMouseMoved", BindingFlags.NonPublic | BindingFlags.Instance)!;
            onMouseMoved.Invoke(player, new object[] { new Vector2(0f, VerticalDelta) });

            return player.GetNode<Node3D>("Head").RotationDegrees.X;
        }
        finally
        {
            player.Free();
            settings.Free();
        }
    }

    /// <summary>The real player, built with the Head, Camera3D and SpotLight3D nodes its _Ready reads.</summary>
    private static PlayerController BuildPlayer()
    {
        var player = new PlayerController { Name = "Player" };
        var head = new Node3D { Name = "Head" };
        var camera = new Camera3D { Name = "Camera3D" };
        camera.AddChild(new SpotLight3D { Name = "SpotLight3D" });
        head.AddChild(camera);
        player.AddChild(head);
        return player;
    }
}
