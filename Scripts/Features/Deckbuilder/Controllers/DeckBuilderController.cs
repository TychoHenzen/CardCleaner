using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// Updated DeckBuilder Controller that spawns 3D world screens.
/// </summary>
public partial class DeckBuilderController : Node, IDependent
{
    private static readonly Vector3 DefaultScreenSpawnPosition = Vector3.Zero;
    private const MapGenerationType DefaultMapType = MapGenerationType.RegularGrid;

    private IGameSessionService? _gameSession;
    private IGridMapGenerator? _gridGenerator;
    private IMeshMapGenerator? _meshGenerator;
    private IServiceResolver? _services;
    private readonly CabinetCycle _cycle = new();

    [Export] public DeckSlot AbilityDeckSlot { get; set; } = null!;
    [Export] public DeckSlot MapCardSlot { get; set; } = null!;
    [Export] public InteractableButton ActivateButton { get; set; } = null!;
    [Export] public InteractableButton? MapKindButton { get; set; }
    [Export] public PackedScene BattleScreenScene { get; set; } = null!;
    [Export] public SimpleWorldMapScreen WorldTileMapScreenScene { get; set; } = null!;
    [Export] public IrregularWorldMapScreen IrregularMapScreen { get; set; } = null!;
    [Export] public MapGenerationType MapType { get; set; } = DefaultMapType;
    [Export] public Vector3 ScreenSpawnPosition { get; set; } = DefaultScreenSpawnPosition;

    public void Inject(IServiceResolver services) => _services = services;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(ScreenSpawnPosition) => true,
            nameof(MapType) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(ScreenSpawnPosition) => Variant.From(DefaultScreenSpawnPosition),
            nameof(MapType) => (int)DefaultMapType,
            _ => base._PropertyGetRevert(property)
        };
    }

    public override void _Ready()
    {
        // MapCardSlot accepts up to 5 cards for multi-card map generation
        MapCardSlot.Capacity = 5;

        // Listen for when cards are dropped into slots (for UI feedback only)
        AbilityDeckSlot.CardsChanged += OnSlotsUpdated;
        MapCardSlot.CardsChanged += OnSlotsUpdated;

        ActivateButton.ButtonPressed += OnButtonPressed;
        if (MapKindButton != null)
            MapKindButton.ButtonPressed += OnMapKindButtonPressed;

        ResolveServices();

        // Show the picked kind, and initially disable the button
        ShowPickedKind();
        UpdateButtonState();
    }

    // A separate method so the slot, button and screen setup in _Ready still runs when not injected.
    private void ResolveServices()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IGameSessionService>(gameSession =>
        {
            _gameSession = gameSession;
            gameSession.StateChanged += OnSessionStateChanged;
        });

        // Each kind's generator, handed to the session when a session of that kind starts
        _services.Get<IGridMapGenerator>(generator => _gridGenerator = generator);
        _services.Get<IMeshMapGenerator>(generator => _meshGenerator = generator);
    }

    public override void _ExitTree()
    {
        if (_gameSession != null)
            _gameSession.StateChanged -= OnSessionStateChanged;
        if (MapKindButton != null)
            MapKindButton.ButtonPressed -= OnMapKindButtonPressed;
    }

    /// <summary>
    /// Called when the map kind toggle is pressed: switches between the grid and the irregular map while the
    /// cabinet is idle, and shows the picked kind's screen with the kind in its status text.
    /// </summary>
    private void OnMapKindButtonPressed()
    {
        if (!_cycle.CanToggleMapKind) return;

        MapType = MapKindToggle.Next(MapType);
        ILog.Print($"Map kind switched to {MapType}");

        ShowPickedKind();
        UpdateBiomePreview();
    }

    /// <summary>
    /// Shows the screen of the picked kind, hides the other, and has the shown screen name the kind in its status.
    /// This is the only place that decides which map screen is visible.
    /// </summary>
    private void ShowPickedKind()
    {
        var grid = MapType == MapGenerationType.RegularGrid;
        if (WorldTileMapScreenScene != null) WorldTileMapScreenScene.Visible = grid;
        if (IrregularMapScreen != null) IrregularMapScreen.Visible = !grid;

        var kindText = MapKindToggle.Describe(MapType);
        if (grid) WorldTileMapScreenScene?.ShowMapKind(kindText);
        else IrregularMapScreen?.ShowMapKind(kindText);
    }

    /// <summary>
    /// Called when the game session state changes; it acts only while a session the cabinet started runs.
    /// A completed session waits for the reset press (map stays visible). A session that falls back to waiting
    /// without completing (its map generation failed or was cancelled) has nothing to reset, so the cabinet is
    /// idle again at once.
    /// </summary>
    private void OnSessionStateChanged(SessionState newState)
    {
        if (!_cycle.IsRunning) return;

        if (newState == SessionState.SessionComplete)
        {
            AwaitMapReset();
            return;
        }

        if (newState != SessionState.WaitingForCards) return;

        ILog.Print("Session ended without completing - cabinet idle again");
        _cycle.ReturnToIdle();
        UpdateButtonState();
    }

    /// <summary>Enables the button and flags that the next press should just reset the view.</summary>
    private void AwaitMapReset()
    {
        ILog.Print("Session complete - map revealed, awaiting reset");

        _cycle.Complete();
        ActivateButton.SetEnabled(true);
        UpdateMapKindButtonState();
    }

    /// <summary>Called when slots are updated - updates button state and biome preview</summary>
    private void OnSlotsUpdated()
    {
        UpdateButtonState();
        UpdateBiomePreview();
    }

    /// <summary>
    /// Updates the biome distribution preview based on current MapCardSlot cards.
    /// Only applicable for regular grid maps.
    /// </summary>
    private void UpdateBiomePreview()
    {
        if (MapType != MapGenerationType.RegularGrid) return;

        var signatures = MapCardSlot.GetCardSignatures();
        WorldTileMapScreenScene.UpdateBiomePreview(signatures.ToArray());
    }

    /// <summary>Updates the button's enabled state based on whether both slots are populated</summary>
    private void UpdateButtonState()
    {
        var canActivate = AbilityDeckSlot.HasCards && MapCardSlot.HasCard;
        ActivateButton.SetEnabled(canActivate);
        UpdateMapKindButtonState();
    }

    // The toggle is lit only while the cabinet is idle: from the activate press until the reset press it stays dark.
    private void UpdateMapKindButtonState() => MapKindButton?.SetEnabled(_cycle.CanToggleMapKind);

    /// <summary>
    /// Called when the activation button is pressed.
    /// If awaiting reset (after session complete): just reset the view.
    /// Otherwise: start a new session if cards are present.
    /// </summary>
    private void OnButtonPressed()
    {
        ILog.Print("Activation button pressed!");

        if (_cycle.IsAwaitingReset)
        {
            ResetFinishedSession();
            return;
        }

        var refusal = CabinetCycle.StartRefusal(MapType, AbilityDeckSlot.HasCards && MapCardSlot.HasCard,
            IrregularMapScreen != null, _gridGenerator != null, _meshGenerator != null);
        if (refusal != null)
        {
            ILog.Error(refusal);
            return;
        }

        StartSession(MapType);
    }

    // Consumes the slotted cards and has the kind's screen start the session with the kind's generator.
    private void StartSession(MapGenerationType kind)
    {
        ILog.Print($"Cards present - starting {kind} map session...");

        // Record the kind before the slots change, so every refresh from here on sees a running session.
        _cycle.Start(kind);
        ShowPickedKind();

        var mapSeeds = MapCardSlot.ConsumeAllCardSignatures();
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        switch (kind)
        {
            case MapGenerationType.RegularGrid:
                WorldTileMapScreenScene.Initialize(mapSeeds.ToArray(), abilities.ToArray(), _gridGenerator!);
                break;

            case MapGenerationType.IrregularMesh:
                IrregularMapScreen.Initialize(mapSeeds.ToArray(), abilities.ToArray(), _meshGenerator!);
                break;
        }

        AbilityDeckSlot.Clear();
        MapCardSlot.Clear();
        UpdateButtonState();

        ILog.Print($"{kind} map session started");
    }

    /// <summary>
    /// The reset press after a finished session: the cabinet is idle again, the finished screen is reset, and the
    /// picked kind's screen shows.
    /// </summary>
    private void ResetFinishedSession()
    {
        ILog.Print("Resetting map view after session complete");

        ResetScreen(_cycle.Reset());
        ShowPickedKind();

        // Update biome preview and button state based on current slot contents
        UpdateBiomePreview();
        UpdateButtonState();
    }

    /// <summary>Resets the screen of the session that just finished to its initial state.</summary>
    private void ResetScreen(MapGenerationType finishedKind)
    {
        switch (finishedKind)
        {
            case MapGenerationType.RegularGrid:
                WorldTileMapScreenScene.ResetToInitialState();
                break;

            case MapGenerationType.IrregularMesh:
                IrregularMapScreen?.Reset();
                break;
        }
    }
}
