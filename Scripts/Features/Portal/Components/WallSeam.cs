using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Portal.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Portal.Components;

/// <summary>
///     A glowing seam in a wall that opens into a doorway between the shop and the workshop. The backoffice seam
///     stays hidden and silent until the player stands in the backoffice carrying a special card, then glows and
///     hums; the workshop's way back is <see cref="AlwaysOpen" />. Coming close opens it into a doorway, and
///     stepping into the doorway moves the player to <see cref="Exit" /> with no cut: the camera
///     keeps its offset to the doorway and its facing relative to it. The node sits at the wall marker and
///     the doorway faces its local +Z, into the room. The decisions live in <see cref="SeamTriggerRule" />,
///     <see cref="SeamPhaseRule" /> and <see cref="PortalTransform" />; this node reads the scene, animates
///     the glow, door and hum, and applies the teleport (including the landing clearance check).
/// </summary>
public partial class WallSeam : Node3D
{
    private const float DefaultOpenRange = 3.5f;
    private const float DefaultCloseRange = 4.5f;
    private const float DefaultDoorHalfWidth = 0.8f;
    private const float DefaultCrossDepth = 0.9f;
    private const float DefaultDoorBottom = -1.5f;
    private const float DefaultDoorTop = 0.9f;
    private const float LandingStep = 0.25f;
    private const int LandingAttempts = 5;
    private const float DoorTweenSeconds = 0.6f;
    private const float ClosedDoorScaleX = 0.05f;

    private Tween? _tween;
    private bool _warnedNoExit;
    private bool _refusedThisOpening;
    private bool _doorOpen;

    /// <summary>The backoffice room volume. Decides whether the player is "in the backoffice".</summary>
    [Export]
    public Area3D? Zone { get; set; }

    /// <summary>The player's hand: its cards decide whether the seam shows.</summary>
    [Export]
    public CardHolder? PlayerHolder { get; set; }

    [Export]
    public CharacterBody3D? Player { get; set; }

    /// <summary>
    ///     Where the doorway leads. It is placed like the seam marker, at the doorway's height above its floor,
    ///     because the teleport keeps the player's height offset, and turned the way the player should walk on.
    /// </summary>
    [Export]
    public Node3D? Exit { get; set; }

    /// <summary>Shows without a zone or a special card, for a doorway the player must always be able to use.</summary>
    [Export]
    public bool AlwaysOpen { get; set; }

    [Export]
    public MeshInstance3D? Glow { get; set; }

    [Export]
    public Node3D? Door { get; set; }

    [Export]
    public AudioStreamPlayer3D? Hum { get; set; }

    /// <summary>Optional pack texture for the glow. A missing file leaves the plain emissive material.</summary>
    [Export]
    public string GlowTexturePath { get; set; } = string.Empty;

    [Export]
    public float OpenRange { get; set; } = DefaultOpenRange;

    [Export]
    public float CloseRange { get; set; } = DefaultCloseRange;

    [Export]
    public float DoorHalfWidth { get; set; } = DefaultDoorHalfWidth;

    [Export]
    public float CrossDepth { get; set; } = DefaultCrossDepth;

    /// <summary>Local Y of the doorway floor, relative to the seam marker.</summary>
    [Export]
    public float DoorBottom { get; set; } = DefaultDoorBottom;

    /// <summary>Local Y of the doorway top, relative to the seam marker.</summary>
    [Export]
    public float DoorTop { get; set; } = DefaultDoorTop;

    public SeamPhase Phase { get; private set; } = SeamPhase.Hidden;

    public bool GlowTextureLoaded { get; private set; }

    /// <summary>
    ///     True while the hum should be audible: the seam is glowing or open. Tracked apart from the
    ///     player node because a headless run has no audio driver and never reports playing.
    /// </summary>
    public bool HumRequested { get; private set; }

    /// <summary>How many times the player has been moved through the doorway.</summary>
    public int CrossingCount { get; private set; }

    public override void _Ready()
    {
        GlowTextureLoaded = TryApplyGlowTexture();
        if (Hum != null)
            Hum.Stream = CreateHumStream();
        ApplyPhase(SeamPhase.Hidden, false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player == null)
            return;

        var next = SeamPhaseRule.Next(Phase, IsTriggered(), HorizontalDistanceToPlayer(), OpenRange, CloseRange);
        if (next != Phase)
            ApplyPhase(next, true);

        if (Phase == SeamPhase.Open
            && PortalTransform.HasCrossed(GlobalTransform, Player.GlobalPosition, DoorHalfWidth, CrossDepth,
                DoorBottom, DoorTop))
            TryCross();
    }

    /// <summary>True when the doorway is always open, or the player is in the backoffice with a special card in hand.</summary>
    public bool IsTriggered()
    {
        if (AlwaysOpen)
            return true;

        var inBackoffice = Zone != null && Player != null && Zone.OverlapsBody(Player);
        var carried = PlayerHolder?.HeldCards.OfType<CardController>().Select(card => card.Signature).ToArray() ?? [];
        return SeamTriggerRule.ShouldShow(inBackoffice, carried);
    }

    private float HorizontalDistanceToPlayer()
    {
        var offset = Player!.GlobalPosition - GlobalPosition;
        return new Vector2(offset.X, offset.Z).Length();
    }

    private void ApplyPhase(SeamPhase phase, bool animate)
    {
        var previous = Phase;
        Phase = phase;
        if (animate)
            ILog.Print($"Wall seam {previous} -> {phase}");

        if (Glow != null)
            Glow.Visible = phase != SeamPhase.Hidden;

        if (phase != SeamPhase.Open)
            _refusedThisOpening = false;

        SetHum(phase != SeamPhase.Hidden);
        SetDoor(phase == SeamPhase.Open, animate);
    }

    private void SetHum(bool on)
    {
        HumRequested = on;
        if (Hum == null)
            return;

        if (on && !Hum.Playing)
            Hum.Play();
        else if (!on && Hum.Playing)
            Hum.Stop();
    }

    private void SetDoor(bool open, bool animate)
    {
        if (Door == null || (animate && open == _doorOpen))
            return;

        _doorOpen = open;
        _tween?.Kill();
        var target = open ? 1f : ClosedDoorScaleX;
        if (!animate || !IsInsideTree())
        {
            Door.Scale = new Vector3(target, 1f, 1f);
            Door.Visible = open;
            return;
        }

        Door.Visible = true;
        _tween = CreateTween();
        _tween.TweenProperty(Door, "scale:x", target, DoorTweenSeconds);
        if (!open)
            _tween.TweenCallback(Callable.From(() => Door.Visible = false));
    }

    private void TryCross()
    {
        if (Exit == null)
        {
            if (!_warnedNoExit)
                ILog.Warning("Wall seam has no Exit; the doorway leads nowhere");
            _warnedNoExit = true;
            return;
        }

        var player = Player!;
        var entry = GlobalTransform;
        var exit = Exit.GlobalTransform;
        var landing = PortalTransform.Map(player.GlobalTransform, entry, exit);
        if (!TryFindClearSpot(player, landing.Origin, out var spot))
        {
            if (!_refusedThisOpening)
                ILog.Warning($"Wall seam crossing refused: no room to land near {landing.Origin}");
            _refusedThisOpening = true;
            return;
        }

        var velocity = PortalTransform.MapVelocity(player.Velocity, entry, exit);
        player.GlobalTransform = new Transform3D(landing.Basis, spot);
        player.Velocity = velocity;
        player.ResetPhysicsInterpolation();
        CrossingCount++;
        ILog.Print($"Wall seam crossed: player moved to {spot}");
    }

    private bool TryFindClearSpot(CharacterBody3D player, Vector3 landing, out Vector3 spot)
    {
        var shape = player.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape;
        var space = GetWorld3D().DirectSpaceState;
        for (var attempt = 0; attempt < LandingAttempts; attempt++)
        {
            spot = PortalTransform.Candidate(landing, attempt, LandingStep);
            if (shape == null || !Overlaps(space, shape, spot, player))
                return true;
        }

        spot = landing;
        return false;
    }

    private static bool Overlaps(PhysicsDirectSpaceState3D space, Shape3D shape, Vector3 at, CharacterBody3D player)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, at),
            CollisionMask = player.CollisionMask,
            Exclude = [player.GetRid()]
        };
        return space.IntersectShape(query, 1).Count > 0;
    }

    private bool TryApplyGlowTexture()
    {
        if (Glow?.MaterialOverride is not StandardMaterial3D material
            || string.IsNullOrEmpty(GlowTexturePath)
            || !ResourceLoader.Exists(GlowTexturePath))
            return false;

        var texture = ResourceLoader.Load<Texture2D>(GlowTexturePath);
        if (texture == null)
            return false;

        var textured = (StandardMaterial3D)material.Duplicate();
        textured.AlbedoTexture = texture;
        textured.EmissionTexture = texture;
        Glow.MaterialOverride = textured;
        return true;
    }

    private static AudioStreamWav CreateHumStream()
    {
        var pcm = SeamTone.Pcm16();
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SeamTone.SampleRate,
            Stereo = false,
            Data = pcm,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = pcm.Length / sizeof(short)
        };
    }
}
