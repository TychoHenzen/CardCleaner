using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop's live player, for scene tests that look at the level rather than play it.
/// </summary>
public static class ShopScenePlayer
{
    /// <summary>
    /// Takes the player out of play without freeing it: the seams, the register and every other node that
    /// exports the player keep a valid object, but it never processes, takes input or moves, and with no
    /// collision layer or mask no physics query, capsule sweep or room volume can see it.
    /// </summary>
    public static PlayerController Sideline(Node3D shop)
    {
        var player = shop.GetNode<PlayerController>("Player");
        player.ProcessMode = Node.ProcessModeEnum.Disabled;
        player.CollisionLayer = 0;
        player.CollisionMask = 0;
        player.ControlEnabled = false;
        return player;
    }
}
