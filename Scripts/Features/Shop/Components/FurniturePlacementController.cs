using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Carries one piece of furniture at a time through the shop: a box being placed from the player's hand, or a
///     placed piece being moved. The ghost shows the pose, and Confirm or Cancel ends the session. What the session
///     carries is a <see cref="PlacementSession" />, the keys are <see cref="PlacementKeys" />, and the physics check
///     is a <see cref="PlacementPreview" />.
/// </summary>
[Service(typeof(IFurniturePlacement))]
public partial class FurniturePlacementController : Node, IFurniturePlacement, IDependent
{
    /// <summary>The floor point a new box starts at, before the first aim.</summary>
    public const float StartX = 8f;
    public const float StartZ = 6f;

        private IServiceResolver? _services;
    private readonly PlacementPreview _preview;
    private PlacementKeys? _keys;
    private PlacementSession? _session;

    public FurniturePlacementController()
    {
        _preview = new PlacementPreview { Name = "PlacementPreview" };
        AddChild(_preview);
    }

    [Export]
    public Node3D? FurnitureRoot { get; set; }

    [Export]
    public ShopNavigationRegion? Navigation { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopExpansion? Expansion { get; set; }

    public bool IsActive => _session != null;

    public FurniturePose CurrentPose { get; private set; }

    public bool CurrentValid { get; private set; }

    /// <summary>When true, the camera aim is ignored so a test can place the pose directly.</summary>
    internal bool AimOverride { get; set; }

    /// <summary>The placed furniture under the camera's aim while no session is active, or null.</summary>
    internal PlacedFurniture? AimedFurniture { get; private set; }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _keys = new PlacementKeys(this, new PlacementActions(() => Rotate(), () => Confirm(), Cancel, TryPickUpAimed));
        _services.Get<IShopExpansion>(expansion => Expansion ??= expansion);
        _services.Get<IInputService>(_keys.Bind);
    }

    public override void _ExitTree()
    {
        _keys?.Unbind();
        // A controller leaving the tree mid-move puts the carried piece back, so it is never left hidden.
        _session?.PutBack();
    }

    public override void _PhysicsProcess(double delta)
    {
        AimedFurniture = IsActive ? null : PlacementAim.Furniture(GetViewport());
        if (!IsActive)
            return;
        if (!AimOverride && PlacementAim.FloorPoint(GetViewport()) is var (x, z))
            AimAt(x, z);

        ProbePhysics();
    }

    public bool Begin(FurnitureBox box)
    {
        var item = box.Item;
        if (IsActive || item == null)
            return false;
        if (FurnitureRoot == null)
        {
            ILog.Error($"{Name} has no FurnitureRoot");
            return false;
        }

        Start(PlacementSession.ForBox(item, box));
        return true;
    }

    public bool BeginMove(PlacedFurniture furniture)
    {
        if (IsActive)
            return false;
        if (!furniture.CanPickUp)
        {
            ILog.Warning("Empty the shelf before moving it.");
            return false;
        }

        var item = furniture.Item;
        if (FurnitureRoot == null || item == null)
        {
            ILog.Error($"{Name} cannot move {furniture.Name}");
            return false;
        }

        var session = PlacementSession.ForMove(item, furniture);
        Navigation?.Rebake();
        Start(session);
        return true;
    }

    public void AimAt(float x, float z)
    {
        if (!IsActive)
            return;
        CurrentPose = new FurniturePose(
            FurniturePlacementModel.Snap(x), FurniturePlacementModel.Snap(z), CurrentPose.QuarterTurns);
        Refresh();
    }

    public void Rotate()
    {
        if (!IsActive)
            return;
        CurrentPose = CurrentPose with { QuarterTurns = FurniturePlacementModel.Turn(CurrentPose.QuarterTurns, 1) };
        Refresh();
    }

    public bool Confirm()
    {
        if (_session is not { } session)
            return false;

        // Rotate and aim only refresh the floor check; the physics check runs on the next tick. Judge the exact
        // pose being committed, so a confirm that follows a rotate within one tick cannot use the old clearance.
        ProbePhysics();
        if (!CurrentValid)
            return false;

        var outcome = session.Commit(CurrentPose, FurnitureRoot);
        if (outcome == CommitOutcome.Kept)
            return false;

        if (outcome == CommitOutcome.Placed)
            Navigation?.Rebake();
        EndSession();
        return outcome == CommitOutcome.Placed;
    }

    public void Cancel()
    {
        if (_session is not { } session)
            return;
        if (session.PutBack())
            Navigation?.Rebake();

        EndSession();
    }

    private void TryPickUpAimed()
    {
        if (!IsActive && AimedFurniture != null)
            BeginMove(AimedFurniture);
    }

    private void Start(PlacementSession session)
    {
        _session = session;
        CurrentPose = session.StartPose(new FurniturePose(StartX, StartZ, 0));
        _preview.Open(session.Item);
        Refresh();
    }

    private void EndSession()
    {
        _session = null;
        CurrentValid = false;
        _preview.Close();
    }

    private void ProbePhysics()
    {
        if (_session is { } session && _preview.Probe(session.Item, CurrentPose))
            Refresh();
    }

    private void Refresh()
    {
        if (_session is not { } session)
            return;

        CurrentValid = !_preview.Blocked && FurniturePlacementModel.IsValid(
            session.Item.Footprint,
            CurrentPose,
            PlacedFurniture.StandingRects(FurnitureRoot),
            ExpansionFloor.For(Expansion));
        if (!CurrentValid)
        {
            _preview.ShowInvalid(CurrentPose);
            return;
        }

        _preview.ShowValid(CurrentPose);
    }
}
