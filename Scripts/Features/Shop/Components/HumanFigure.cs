using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A person in the shop. It wears one look from <see cref="Looks" />, scaled to <see cref="Height" />, turns the
///     body it belongs to toward where it walks (or toward the point it was told to face while it stands), loops
///     idle or walk by speed and plays one-shot actions such as picking an item up. While no model is worn, its
///     child named Placeholder shows instead, so a scene stays usable without the asset files.
/// </summary>
public partial class HumanFigure : Node3D
{
    private const string PlaceholderName = "Placeholder";
    private const string ModelName = "Model";
    private const float DefaultHeight = 1.7f;
    private const float DefaultTurnDegreesPerSecond = 540f;
    private const float BlendSeconds = 0.2f;
    private const float FacingMeters = 0.01f;

    private Node3D? _model;
    private AnimationPlayer? _player;
    private FigureClip? _action;
    private FigureClip _loop = FigureClip.Idle;
    private Vector3? _facePoint;

    [Export]
    public FigureLooks? Looks { get; set; }

    /// <summary>The look worn when the figure readies. -1 leaves the choice to <see cref="Dress" />.</summary>
    [Export]
    public int Look { get; set; } = -1;

    /// <summary>The height in metres a worn model is scaled to, feet on the figure's origin.</summary>
    [Export]
    public float Height { get; set; } = DefaultHeight;

    [Export]
    public float TurnDegreesPerSecond { get; set; } = DefaultTurnDegreesPerSecond;

    public int LookCount => Looks?.Models.Length ?? 0;

    /// <summary>The index of the worn look in <see cref="Looks" />, or -1 while none is worn.</summary>
    public int Worn { get; private set; } = -1;

    /// <summary>The worn model, or null while the placeholder shows.</summary>
    public Node3D? Model => _model;

    /// <summary>The worn model's animation player, or null when it has none.</summary>
    public AnimationPlayer? Animations => _player;

    /// <summary>What the figure is doing: a one-shot action until it ends, else its loop.</summary>
    public FigureClip Playing => _action ?? _loop;

    public override void _Ready()
    {
        if (Look >= 0 && Worn < 0)
            Wear(Look);
    }

    /// <summary>Wears a look drawn from <paramref name="random" />. Draws nothing when there are no looks.</summary>
    public void Dress(IRandom random)
    {
        if (LookCount > 0)
            Wear(random.RandiRange(0, LookCount - 1));
    }

    /// <summary>
    ///     Wears look <paramref name="index" />. False, with the placeholder kept, when it cannot be loaded.
    /// </summary>
    public bool Wear(int index)
    {
        if (Looks is null || index < 0 || index >= Looks.Models.Length)
            return false;

        var path = Looks.Models[index];
        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model)
        {
            ILog.Warning($"{Name}: look {index} ({path}) is missing or is not a 3D scene; the placeholder stays.");
            return false;
        }

        Put(model);
        Worn = index;
        return true;
    }

    /// <summary>
    ///     Wears <paramref name="model" /> in place of any earlier one. Tests hand-build models through it.
    /// </summary>
    internal void Put(Node3D model)
    {
        if (_model != null)
        {
            RemoveChild(_model);
            _model.QueueFree();
        }

        model.Name = ModelName;
        model.Transform = FigureFit.Standing(model, Height, Looks?.ModelYawDegrees ?? 0f);
        AddChild(model);
        _model = model;
        var placeholder = GetNodeOrNull<Node3D>(PlaceholderName);
        if (placeholder != null)
            placeholder.Visible = false;

        var players = model.FindChildren("*", nameof(AnimationPlayer), true, false);
        _player = players.Count > 0 ? (AnimationPlayer)players[0] : null;
        _action = null;
        if (_player != null)
        {
            MakeLoops(_player);
            _player.AnimationFinished += OnActionFinished;
        }

        PlayLoop(0f);
    }

    /// <summary>
    ///     One frame of movement: turns <paramref name="body" /> toward its walk, or while it stands toward the
    ///     point set by <see cref="FaceToward" />, and loops walk or idle by the speed of <paramref name="velocity" />.
    /// </summary>
    public void Follow(Node3D body, Vector3 velocity, double delta)
    {
        var flat = new Vector2(velocity.X, velocity.Z);
        var speed = flat.Length();
        if (TargetYaw(body, flat, speed) is { } target)
        {
            var rotation = body.GlobalRotation;
            var step = Mathf.DegToRad(TurnDegreesPerSecond) * (float)delta;
            var turned = FigureMotion.TurnToward(rotation.Y, target, step);
            body.GlobalRotation = new Vector3(rotation.X, turned, rotation.Z);
        }

        _loop = FigureMotion.LoopFor(speed);
        PlayLoop(speed);
    }

    // Walking, the body turns down its walk and forgets the face point; standing, it turns to that point, if any.
    private float? TargetYaw(Node3D body, Vector2 flat, float speed)
    {
        if (speed >= FigureMotion.WalkingSpeed)
        {
            _facePoint = null;
            return FigureMotion.YawToward(flat.X, flat.Y);
        }

        if (_facePoint is not { } point)
            return null;

        var toPoint = point - body.GlobalPosition;
        return new Vector2(toPoint.X, toPoint.Z).Length() > FacingMeters
            ? FigureMotion.YawToward(toPoint.X, toPoint.Z)
            : null;
    }

    /// <summary>Turns toward <paramref name="point" /> the next time the figure stands still.</summary>
    public void FaceToward(Vector3 point) => _facePoint = point;

    /// <summary>
    ///     Plays a one-shot action and returns how many seconds it lasts; the loop comes back when it ends. A loop
    ///     clip, or a clip the worn model lacks, plays nothing and returns 0.
    /// </summary>
    public float Act(FigureClip clip)
    {
        if (FigureMotion.Loops(clip) || _player is null || Looks is null || !_player.HasAnimation(Looks.ClipName(clip)))
            return 0f;

        var name = Looks.ClipName(clip);
        _action = clip;
        _player.Play(name, BlendSeconds);
        _player.SpeedScale = 1f;
        return (float)_player.GetAnimation(name).Length;
    }

    private void OnActionFinished(StringName clip)
    {
        if (_action is not { } action || Looks is null || clip != Looks.ClipName(action))
            return;

        _action = null;
        PlayLoop(_loop == FigureClip.Walk ? FigureMotion.WalkingSpeed : 0f);
    }

    private void PlayLoop(float speed)
    {
        if (_player is null || Looks is null || _action != null)
            return;

        var clip = Looks.ClipName(_loop);
        if (!_player.HasAnimation(clip))
            return;
        if (_player.CurrentAnimation != clip)
            _player.Play(clip, BlendSeconds);
        _player.SpeedScale = _loop == FigureClip.Walk ? FigureMotion.WalkPlayback(speed, Looks.WalkClipSpeed) : 1f;
    }

    // Imported glTF clips do not loop, so idle and walk would play once and freeze; they loop from here on.
    private void MakeLoops(AnimationPlayer player)
    {
        foreach (var clip in new[] { FigureClip.Idle, FigureClip.Walk })
        {
            var name = Looks?.ClipName(clip);
            if (name != null && player.HasAnimation(name))
                player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }
    }
}
