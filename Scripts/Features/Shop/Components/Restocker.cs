using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The hired restocker. While it is hired, the shop is open and the tree is not paused, it walks the baked
///     navigation mesh: it takes a pack or booster from a storage rack (or off the floor) to a product-shelf slot
///     with room (a booster may top up a booster stack), or sets a box down at a product shelf and unpacks it there.
///     <see cref="RestockPlanner" /> decides each move and <see cref="RestockerJob" /> tracks the walk. Idle, it
///     returns to where the scene placed it.
/// </summary>
public partial class Restocker : CharacterBody3D, IDependent
{
    private const float DefaultMoveSpeed = 2.5f;
    private const float DefaultStuckSeconds = 8f;
    private const float DefaultPickupRadius = 4f;
    private const float DefaultScanSeconds = 0.5f;
    private const int DefaultBoxUnpackThreshold = 8;
    private const float HomeMeters = 0.6f;

    private readonly RestockerJob _job = new();
    private RestockerScanResult _seen = new();
    private IServiceResolver? _services;
    private IStaffService? _staff;
    private IShopDayService? _day;
    private CustomerNavigator? _navigator;
    private RestockerHands? _hands;
    private HumanFigure? _figure;
    private RestockerScan? _scan;
    private Vector3 _home;
    private double _sinceScan;
    private bool _homing;
    private ShelfLevel? _putBackLevel;

    [Export] public float MoveSpeed { get; set; } = DefaultMoveSpeed;

    [Export] public float StuckSeconds { get; set; } = DefaultStuckSeconds;

    /// <summary>How far from a shelf a loose pack still counts as stock to collect.</summary>
    [Export] public float PickupRadius { get; set; } = DefaultPickupRadius;

    [Export] public float ScanSeconds { get; set; } = DefaultScanSeconds;

    /// <summary>Free product-shelf seats needed before a box is unpacked (see <see cref="RestockPlanner" />).</summary>
    [Export] public int BoxUnpackThreshold { get; set; } = DefaultBoxUnpackThreshold;

    /// <summary>The job phase, for tests.</summary>
    public RestockerPhase Phase => _job.Phase;

    private bool Active =>
        _staff?.IsHired(StaffRoleIds.Restocker) == true && _day is { IsOpen: true } && !GetTree().Paused;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        Visible = false;
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _home = GlobalPosition;
        _hands = new RestockerHands(this, GetNode<Node3D>("Hand"));
        _figure = GetNodeOrNull<HumanFigure>("Figure");
        _navigator = new CustomerNavigator(this, GetNode<NavigationAgent3D>("Agent"), OnArrived, OnBlocked);
        _scan = new RestockerScan(GetTree(), PickupRadius);
        _services.Get<IStaffService>(staff =>
        {
            _staff = staff;
            staff.Changed += RefreshVisible;
            RefreshVisible();
        });
        _services.Get<IShopDayService>(day => _day = day);
    }

    public override void _ExitTree()
    {
        if (_staff != null)
            _staff.Changed -= RefreshVisible;
    }

    public override void _PhysicsProcess(double delta)
    {
        Step(delta);
        _figure?.Follow(this, Velocity, delta);
    }

    /// <summary>Advances the restocker by one physics step. Public so tests can drive it by hand.</summary>
    public void Step(double delta)
    {
        _job.Tick(delta);
        // The figure walks by Velocity, so it holds only this step's walk; the navigator sets it when it moves.
        Velocity = Vector3.Zero;
        if (_navigator == null || !Active)
            return;

        if (_job.Phase == RestockerPhase.Idle && _putBackLevel == null)
        {
            _sinceScan += delta;
            if (_sinceScan >= ScanSeconds)
            {
                _sinceScan = 0;
                PlanNext();
            }
        }

        if (_job.Phase != RestockerPhase.Idle || _homing || _putBackLevel != null)
            _navigator.Step(delta, MoveSpeed, StuckSeconds);
    }

    private void RefreshVisible()
    {
        var hired = _staff?.IsHired(StaffRoleIds.Restocker) ?? false;
        Visible = hired;
        if (!hired)
            StandDown();
    }

    // A dismissed restocker sets what it carries down at its feet, unfrozen, where the next scan finds it as a loose
    // source, and forgets its walk, so nothing stays parked in a hidden hand.
    private void StandDown()
    {
        _hands?.SetDown();
        _putBackLevel = null;
        _homing = false;
        _job.Cancel();
    }

    private void PlanNext()
    {
        _seen = _scan!.Gather(GlobalPosition, _hands!.Carried);
        var step = RestockPlanner.Plan(
            _seen.Sources, _seen.Slots, _job.BlockedPairs, _seen.Distance, BoxUnpackThreshold,
            _job.UnreachableShelves);
        if (step.Kind == RestockStepKind.Idle || _seen.SourcePosition(step) is not { } source)
        {
            // Nothing can use what is in the hands (a box below the threshold, no free slot): put it back.
            if (_hands.Carried == null)
                GoHome();
            else
                PutBack();
            return;
        }

        _homing = false;
        _job.Begin(step);
        _navigator!.Start(source);
    }

    // Walks the carried container to the rack level it came from, or another that takes it. With no rack level free
    // it is set down on the floor, unfrozen, where the next scan finds it as a loose source.
    private void PutBack()
    {
        var level = _seen.PutBackLevel(_hands!.Carried!, _hands.Origin, GlobalPosition);
        if (level == null)
        {
            _hands.SetDown();
            return;
        }

        _putBackLevel = level;
        _navigator!.Start(level.GlobalPosition);
    }

    private void FinishPutBack()
    {
        var level = _putBackLevel;
        _putBackLevel = null;
        if (level == null || !GodotObject.IsInstanceValid(level) || !_hands!.Shelve(level))
            _hands!.SetDown();
    }

    private void GoHome()
    {
        if (_homing || FlatDistance(GlobalPosition, _home) <= HomeMeters)
            return;

        _homing = true;
        _navigator!.Start(_home);
    }

    private void OnArrived()
    {
        switch (_job.Phase)
        {
            case RestockerPhase.ToSource:
                ArriveAtSource();
                break;
            case RestockerPhase.ToShelf:
                ArriveAtShelf();
                break;
            default:
                _homing = false;
                if (_putBackLevel != null)
                    FinishPutBack();
                break;
        }
    }

    private void OnBlocked()
    {
        if (_job.Phase != RestockerPhase.Idle)
        {
            _job.NoPath();
            return;
        }

        _homing = false;
        if (_putBackLevel != null)
            FinishPutBack();
    }

    private void ArriveAtSource()
    {
        var step = _job.Current;
        if (_seen.SourceNode(step.SourceId) is not CardContainer container || !_hands!.Take(container)
            || _seen.TargetPosition(step) is not { } target)
        {
            _job.TargetLost();
            return;
        }

        _job.Arrived();
        _navigator!.Start(target);
    }

    private void ArriveAtShelf()
    {
        var step = _job.Current;
        var done = step.Kind == RestockStepKind.UnpackBox ? Unpack(step) : Shelve(step);
        if (done)
            _job.Arrived();
        else
            _job.TargetLost();
    }

    private bool Shelve(RestockStep step) =>
        _seen.SlotNode(step.TargetId) is ShelfLevel level && _hands!.Shelve(level);

    // The box stays in the hands: its booster boxes and boosters move straight onto the shelf's levels, and the next
    // scan puts the box back on a rack (or unpacks it at another shelf while it still holds products). Levels may have
    // been stocked on the walk, so the threshold is checked again, on free seats as the planner counts them.
    private bool Unpack(RestockStep step)
    {
        if (_hands!.Carried is not { } box || _seen.ShelfNode(step.TargetId) is not CardShelf shelf)
            return false;

        var seats = RestockPlanner.SeatsForBoxContents(_scan!.Gather(GlobalPosition, box).Slots);
        return seats >= RestockPlanner.FreeSlotsToUnpack(BoxUnpackThreshold, RestockerScan.ProductsIn(box))
               && shelf.TryUnpack(box);
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
