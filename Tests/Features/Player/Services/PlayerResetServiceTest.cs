using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Player.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Player.Services;

[TestSuite]
[RequireGodotRuntime]
public class PlayerResetServiceTest
{
    private const string PlayerScene = "res://Scenes/Components/player.tscn";
    private static readonly Vector3 SafePosition = new(0f, 1f, 0f);

    private PlayerResetService _resetService = null!;
    private IPlayerResetService _service = null!;

    [BeforeTest]
    public void Setup()
    {
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<ISafePositionTracker, SafePositionTracker>();
        _resetService = new PlayerResetService();
        AddNode(_resetService);
        ServiceLocator.Container.RegisterSingleton<IPlayerResetService>(_resetService);
        _service = ServiceLocator.Container.Resolve<IPlayerResetService>();
    }

    [AfterTest]
    public static void TearDown() => ServiceLocator.ResetForTesting();

    [TestCase]
    [TestCategory("Unit")]
    public void ResetPlayerToSafety_AfterFallingOutOfBounds_RestoresSafePositionAndStartsCooldown()
    {
        // Arrange - the player's _Ready records the position it has when it enters the tree
        var player = GD.Load<PackedScene>(PlayerScene).Instantiate<PlayerController>();
        player.Position = SafePosition;
        AddNode(player);
        player.Velocity = new Vector3(4f, -6f, 2f);
        player.GlobalPosition = new Vector3(0f, -60f, 0f);
        var resets = 0;
        _service.PlayerReset += () => resets++;

        // Act - no physics frame may run before the reset, or CheckOutOfBounds would reset the player itself
        var first = _service.ResetPlayerToSafety();
        var second = _service.ResetPlayerToSafety();

        // Assert
        AssertBool(first).IsTrue();
        AssertThat(player.GlobalPosition).IsEqual(SafePosition);
        AssertThat(player.Velocity).IsEqual(Vector3.Zero);
        AssertThat(resets).IsEqual(1);
        AssertBool(_service.IsOnCooldown).IsTrue();
        AssertBool(second).IsFalse();
    }
}
