using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The shop sign. Interacting opens the shop when it is closed and closes it (charging the day's bills)
///     when it is open. A label on the sign shows whether the shop is open.
/// </summary>
public partial class ShopSign : StaticBody3D, IInteractable, IDependent
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask
    private const string OpenText = "Open - interact to close";
    private const string ClosedText = "Closed - interact to open";

    private IShopDayService? _dayService;
    private IServiceResolver? _services;

    [Export]
    public Label3D? StatusLabel { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopDayService? DayService
    {
        get => _dayService;
        set
        {
            Unsubscribe();
            _dayService = value;
            Subscribe();
            ShowState();
        }
    }

    public bool CanInteract => true;

    public Node3D InteractionBody => this;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        if (_services == null)
        {
            if (DayService == null)
                ILog.Error($"{Name} was not injected");
        }
        else
        {
            _services.Get<IShopDayService>(service => DayService ??= service);
        }

        ShowState();
    }

    public override void _EnterTree()
    {
        // A sign that leaves and re-enters the tree must listen again and show the current state.
        Unsubscribe();
        Subscribe();
        ShowState();
    }

    public override void _ExitTree() => Unsubscribe();

    public void Interact()
    {
        if (DayService == null)
            return;

        if (DayService.IsOpen)
            DayService.CloseShop();
        else
            DayService.OpenShop();
    }

    public void Highlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = false;
    }

    private void Subscribe()
    {
        if (_dayService == null)
            return;
        _dayService.Opened += OnStateChanged;
        _dayService.Closed += OnClosed;
        _dayService.Restored += OnStateChanged;
    }

    private void Unsubscribe()
    {
        if (_dayService == null)
            return;
        _dayService.Opened -= OnStateChanged;
        _dayService.Closed -= OnClosed;
        _dayService.Restored -= OnStateChanged;
    }

    private void OnStateChanged() => ShowState();

    private void OnClosed(DayReport report) => ShowState();

    private void ShowState()
    {
        if (StatusLabel == null)
            return;
        StatusLabel.Text = _dayService is { IsOpen: true } ? OpenText : ClosedText;
    }
}
