using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Shows the first expansion lot as sealed or open. The lot is sealed by <see cref="SealingWall" />; once the
///     expansion service owns the lot this node turns that wall's collision and art off and rebakes the navigation,
///     so the lot is reachable. It holds no purchase logic: it only reads <see cref="IShopExpansion" />.
/// </summary>
public partial class ExpansionLotView : Node3D, IDependent
{
    private const string OpenSign = "Expansion lot (open)";

    private IServiceResolver? _services;
    private IShopExpansion? _expansion;
    private bool _open;

    /// <summary>The lot that this node shows; its id in the expansion catalog.</summary>
    [Export] public string LotId { get; set; } = "side_wing_1";

    /// <summary>The wall between the storefront and the lot. It stays in the scene; opening switches it off.</summary>
    [Export] public StaticBody3D? SealingWall { get; set; }

    [Export] public Label3D? Sign { get; set; }

    [Export] public ShopNavigationRegion? Navigation { get; set; }

    /// <summary>True once the lot has been opened.</summary>
    public bool IsOpen => _open;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IShopExpansion>(Connect);
    }

    // The subscription is dropped on every exit and made again on every re-entry (a reparent), so the service
    // never keeps a handler of a node that left the tree.
    public override void _EnterTree()
    {
        if (_expansion != null)
            _expansion.Opened += OnOpened;
    }

    public override void _ExitTree()
    {
        if (_expansion != null)
            _expansion.Opened -= OnOpened;
    }

    private void Connect(IShopExpansion expansion)
    {
        _expansion = expansion;
        expansion.Opened += OnOpened;
        if (expansion.Plan.Owned.Contains(LotId))
            Open();
    }

    private void OnOpened(string lotId)
    {
        if (lotId == LotId)
            Open();
    }

    private void Open()
    {
        if (_open)
            return;
        _open = true;

        if (SealingWall == null)
        {
            ILog.Error($"{Name} has no SealingWall");
            return;
        }

        // Set directly, not deferred: the rebake below must already see the wall without collision.
        foreach (var child in SealingWall.GetChildren())
        {
            if (child is CollisionShape3D shape)
                shape.Disabled = true;
        }

        SealingWall.Visible = false;
        if (Sign != null)
            Sign.Text = OpenSign;
        Navigation?.Rebake();
    }
}
