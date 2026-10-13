using CardCleaner.Scripts.Core.DependencyInjection;
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
public partial class DeckBuilderController : Node
{
    // Default values as constants
    private static readonly Vector3 DefaultScreenSpawnPosition = Vector3.Zero;
    private const MapGenerationType DefaultMapType = MapGenerationType.RegularGrid;

    /// <summary>Where the cabinet is in its activate-reset cycle.</summary>
    private enum CabinetPhase
    {
        Idle,
        Running,
        AwaitingReset
    }

    private IGameSessionService? _gameSession;
    private IGridMapGenerator? _gridGenerator;
    private CabinetPhase _phase = CabinetPhase.Idle;

    // The kind of the session started by the last activate press, read only while the cabinet is not idle, so a
    // change of MapType in between cannot send the finish or the reset to the other screen.
    private MapGenerationType _runningKind;

    // The map kind toggle works only while the cabinet is idle, so the kind never changes under a live map.
    private bool CanToggleMapKind => _phase == CabinetPhase.Idle;

    [Export] public DeckSlot AbilityDeckSlot { get; set; } = null!;
    [Export] public DeckSlot MapCardSlot { get; set; } = null!;
    [Export] public InteractableButton ActivateButton { get; set; } = null!;
    [Export] public InteractableButton? MapKindButton { get; set; }
    [Export] public PackedScene BattleScreenScene { get; set; } = null!;
    [Export] public SimpleWorldMapScreen WorldTileMapScreenScene { get; set; } = null!;
    [Export] public IrregularWorldMapScreen IrregularMapScreen { get; set; } = null!;
    [Export] public MapGenerationType MapType { get; set; } = DefaultMapType;
    [Export] public Vector3 ScreenSpawnPosition { get; set; } = DefaultScreenSpawnPosition;

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
        MapCardSlot.CardsChanged += OnSlotsUpdated; // Changed from CardChanged to CardsChanged

        // Listen for button activation
        ActivateButton.ButtonPressed += OnButtonPressed;
        if (MapKindButton != null)
            MapKindButton.ButtonPressed += OnMapKindButtonPressed;

        // Subscribe to game session state changes
        ServiceLocator.Get<IGameSessionService>(gameSession =>
        {
            _gameSession = gameSession;
            gameSession.StateChanged += OnSessionStateChanged;
        });

        // The grid kind's generator, handed to the session when a grid session starts
        ServiceLocator.Get<IGridMapGenerator>(generator => _gridGenerator = generator);

        if (IrregularMapScreen != null)
            IrregularMapScreen.ExplorationFinished += OnIrregularExplorationFinished;

        // Show the picked kind, and initially disable the button
        ShowPickedKind();
        UpdateButtonState();
    }

    public override void _ExitTree()
    {
        // Clean up event subscriptions
        if (_gameSession != null)
        {
            _gameSession.StateChanged -= OnSessionStateChanged;
        }

        if (IrregularMapScreen != null)
            IrregularMapScreen.ExplorationFinished -= OnIrregularExplorationFinished;

        if (MapKindButton != null)
            MapKindButton.ButtonPressed -= OnMapKindButtonPressed;
    }

    /// <summary>
    /// Called when the map kind toggle is pressed: switches between the grid and the irregular map while the
    /// cabinet is idle, and shows the picked kind's screen with the kind in its status text.
    /// </summary>
    private void OnMapKindButtonPressed()
    {
        if (!CanToggleMapKind) return;

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
    /// Called when the game session state changes; only a running grid session acts on it.
    /// A completed session waits for the reset press (map stays visible). A session that falls back to waiting
    /// without completing (its map generation failed or was cancelled) has nothing to reset, so the cabinet is
    /// idle again at once.
    /// </summary>
    private void OnSessionStateChanged(SessionState newState)
    {
        if (_phase != CabinetPhase.Running || _runningKind != MapGenerationType.RegularGrid) return;

        if (newState == SessionState.SessionComplete)
        {
            AwaitMapReset();
            return;
        }

        if (newState != SessionState.WaitingForCards) return;

        ILog.Print("Grid session ended without completing - cabinet idle again");
        _phase = CabinetPhase.Idle;
        UpdateButtonState();
    }

    /// <summary>
    /// Called when the irregular mesh map finishes exploring (map stays visible).
    /// </summary>
    private void OnIrregularExplorationFinished()
    {
        if (_phase == CabinetPhase.Running && _runningKind == MapGenerationType.IrregularMesh)
            AwaitMapReset();
    }

    /// <summary>
    /// Enables the button and flags that the next press should just reset the view.
    /// </summary>
    private void AwaitMapReset()
    {
        ILog.Print("Session complete - map revealed, awaiting reset");

        _phase = CabinetPhase.AwaitingReset;
        ActivateButton.SetEnabled(true);
        UpdateMapKindButtonState();
    }

    /// <summary>
    /// Called when slots are updated - updates button state and biome preview
    /// </summary>
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

    /// <summary>
    /// Updates the button's enabled state based on whether both slots are populated
    /// </summary>
    private void UpdateButtonState()
    {
        var canActivate = AbilityDeckSlot.HasCards && MapCardSlot.HasCard; // Using HasCard for semantic clarity
        ActivateButton.SetEnabled(canActivate);
        UpdateMapKindButtonState();
    }

    /// <summary>
    /// The toggle is lit only while the cabinet is idle: from the activate press until the reset press it stays dark.
    /// </summary>
    private void UpdateMapKindButtonState()
    {
        MapKindButton?.SetEnabled(CanToggleMapKind);
    }

    /// <summary>
    /// Called when the activation button is pressed.
    /// If awaiting reset (after session complete): just reset the view.
    /// Otherwise: start a new session if cards are present.
    /// </summary>
    private void OnButtonPressed()
    {
        ILog.Print("Activation button pressed!");

        if (_phase == CabinetPhase.AwaitingReset)
        {
            ResetFinishedSession();
            return;
        }

        // Normal flow: start a session if cards are present
        if (!AbilityDeckSlot.HasCards || !MapCardSlot.HasCard)
        {
            ILog.Error("Button pressed but slots not properly filled!");
            return;
        }

        if (MapType == MapGenerationType.IrregularMesh && IrregularMapScreen == null)
        {
            ILog.Error("IrregularMapScreen not assigned! Cannot generate irregular mesh map.");
            return;
        }

        if (MapType == MapGenerationType.RegularGrid && _gridGenerator == null)
        {
            ILog.Error("No IGridMapGenerator registered! Cannot generate a grid map.");
            return;
        }

        ILog.Print($"Cards present - starting {MapType} map session...");

        // Record the kind before the slots change, so every refresh from here on sees a running session.
        var kind = MapType;
        _runningKind = kind;
        _phase = CabinetPhase.Running;
        ShowPickedKind();

        // Consume the seed cards and ability deck
        var mapSeeds = MapCardSlot.ConsumeAllCardSignatures();
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        // Start the session on the screen of the recorded kind
        switch (kind)
        {
            case MapGenerationType.RegularGrid:
                StartRegularGridGeneration(mapSeeds.ToArray(), abilities.ToArray(), _gridGenerator!);
                break;

            case MapGenerationType.IrregularMesh:
                StartIrregularMeshGeneration(mapSeeds.ToArray(), abilities.ToArray());
                break;
        }

        // Clear slots and disable button
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

        _phase = CabinetPhase.Idle;
        ResetScreen(_runningKind);
        ShowPickedKind();

        // Update biome preview and button state based on current slot contents
        UpdateBiomePreview();
        UpdateButtonState();
    }

    /// <summary>
    /// Starts the grid session on the grid screen with the grid generator the container registered.
    /// </summary>
    private void StartRegularGridGeneration(CardSignature[] mapSeeds, CardSignature[] abilities, IMapGenerator generator)
    {
        WorldTileMapScreenScene.Initialize(mapSeeds, abilities, generator);
    }

    /// <summary>
    /// Starts map generation using the irregular mesh system.
    /// </summary>
    private void StartIrregularMeshGeneration(CardSignature[] mapSeeds, CardSignature[] abilities)
    {
        // Generate a seed from the card signatures
        var seed = GenerateSeedFromSignatures(mapSeeds);

        // Pass seed, card signatures for terrain, and abilities for combat
        IrregularMapScreen.GenerateMap(seed, mapSeeds, abilities);
    }

    /// <summary>
    /// Generates an integer seed from card signatures for reproducible generation.
    /// </summary>
    private static int GenerateSeedFromSignatures(CardSignature[] signatures)
    {
        if (signatures.Length == 0) return 0;

        // Combine signature elements into a seed
        var hash = 17;
        foreach (var sig in signatures)
        {
            foreach (var element in sig.Elements)
            {
                // Convert float to int bits and combine
                hash = hash * 31 + System.BitConverter.SingleToInt32Bits(element);
            }
        }
        return hash;
    }

    /// <summary>
    /// Resets the screen of the session that just finished to its initial state.
    /// </summary>
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
