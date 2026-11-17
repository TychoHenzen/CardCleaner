using System;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The people a <c>HumanFigure</c> can be drawn as: one model file per look, all rigged alike, and the names of
///     the clips in them that play each <see cref="FigureClip" />. The models are paths, not packed scenes, so a
///     scene that uses this still loads when the asset files are missing; the figure then shows its placeholder.
/// </summary>
[GlobalClass]
public partial class FigureLooks : Resource
{
    /// <summary>One model file (a .glb or a scene) per look.</summary>
    [Export]
    public string[] Models { get; set; } = [];

    [Export]
    public string IdleClip { get; set; } = "idle";

    [Export]
    public string WalkClip { get; set; } = "walk";

    [Export]
    public string PickUpClip { get; set; } = "pick-up";

    [Export]
    public string HandOverClip { get; set; } = "interact-right";

    [Export]
    public string RefuseClip { get; set; } = "emote-no";

    /// <summary>The speed in metres a second at which the walk clip's feet keep still on the floor.</summary>
    [Export]
    public float WalkClipSpeed { get; set; } = 1.6f;

    /// <summary>The turn in degrees about the up axis that points a model's face down -Z, the way nodes look.</summary>
    [Export]
    public float ModelYawDegrees { get; set; }

    /// <summary>The clip name that plays <paramref name="clip" />.</summary>
    public string ClipName(FigureClip clip) => clip switch
    {
        FigureClip.Idle => IdleClip,
        FigureClip.Walk => WalkClip,
        FigureClip.PickUp => PickUpClip,
        FigureClip.HandOver => HandOverClip,
        FigureClip.Refuse => RefuseClip,
        _ => throw new ArgumentOutOfRangeException(nameof(clip), clip, null),
    };
}
