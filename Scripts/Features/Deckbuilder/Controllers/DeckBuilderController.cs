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

    private IGameSessionService? _gameSession;
    private bool _awaitingMapReset;

    // The kind of the session started by the last activate press, kept until the reset press so a change of MapType
    // in between cannot send the finish or the reset to the other screen.
    private MapGenerationType? _runningKind;

    private bool CanToggleMapKind =>
        MapKindToggle.CanToggle(sessionRunning: _runningKind != null && !_awaitingMapReset, awaitingReset: _awaitingMapReset);

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
    /// Shows the screen of the picked kind, hides the other, and names the kind in the shown screen's status label.
    /// </summary>
    private void ShowPickedKind()
    {
        var grid = MapType == MapGenerationType.RegularGrid;
        if (WorldTileMapScreenScene != null) WorldTileMapScreenScene.Visible = grid;
        if (IrregularMapScreen != null) IrregularMapScreen.Visible = !grid;

        var label = grid ? WorldTileMapScreenScene?.StatusLabel : IrregularMapScreen?.StatusLabel;
        if (label != null) label.Text = MapKindToggle.Describe(MapType);
    }

    /// <summary>
    /// Called when the game session state changes.
    /// Re-enables the button when session completes (map stays visible). A grid session that falls back to
    /// waiting without completing (its map generation failed or was cancelled) has nothing to reset, so it simply
    /// ends and the cabinet is idle again.
    /// </summary>
    private void OnSessionStateChanged(SessionState newState)
    {
        if (_runningKind != MapGenerationType.RegularGrid) return;

        if (newState == SessionState.SessionComplete)
        {
            AwaitMapReset();
        }
        else if (newState == SessionState.WaitingForCards && !_awaitingMapReset)
        {
            ILog.Print("Grid session ended without completing - cabinet idle again");
            _runningKind = null;
            UpdateButtonState();
        }
    }

    /// <summary>
    /// Called when the irregular mesh map finishes exploring (map stays visible).
    /// </summary>
    private void OnIrregularExplorationFinished()
    {
        if (_runningKind == MapGenerationType.IrregularMesh)
            AwaitMapReset();
    }

    /// <summary>
    /// Enables the button and flags that the next press should just reset the view.
    /// </summary>
    private void AwaitMapReset()
    {
        ILog.Print("Session complete - map revealed, awaiting reset");

        _awaitingMapReset = true;
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
    /// Otherwise: start new generation if cards are present.
    /// </summary>
    private void OnButtonPressed()
    {
        ILog.Print("Activation button pressed!");

        // If we're awaiting reset after session complete, just reset the view
        if (_awaitingMapReset)
        {
            ILog.Print("Resetting map view after session complete");

            var finishedKind = _runningKind;
            _awaitingMapReset = false;
            _runningKind = null;
            ResetScreen(finishedKind);
            ShowPickedKind();

            // Update biome preview and button state based on current slot contents
            UpdateBiomePreview();
            UpdateButtonState();
            return;
        }

        // Normal flow: start generation if cards are present
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

        ILog.Print($"Cards present - starting {MapType} map generation...");

        // Record the kind before the slots change, so every refresh from here on sees a running session.
        var kind = MapType;
        _runningKind = kind;

        // Consume the seed cards and ability deck
        var mapSeeds = MapCardSlot.ConsumeAllCardSignatures();
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        // Initialize the screen of the recorded kind
        switch (kind)
        {
            case MapGenerationType.RegularGrid:
                StartRegularGridGeneration(mapSeeds.ToArray(), abilities.ToArray());
                break;

            case MapGenerationType.IrregularMesh:
                StartIrregularMeshGeneration(mapSeeds.ToArray(), abilities.ToArray());
                break;
        }

        // Clear slots and disable button
        AbilityDeckSlot.Clear();
        MapCardSlot.Clear();
        UpdateButtonState();

        ILog.Print($"{kind} map screen generated successfully!");
    }

    /// <summary>
    /// Starts map generation using the regular grid system (via GameSessionService).
    /// </summary>
    private void StartRegularGridGeneration(CardSignature[] mapSeeds, CardSignature[] abilities)
    {
        // Show regular grid screen, hide irregular screen
        WorldTileMapScreenScene.Visible = true;
        if (IrregularMapScreen != null) IrregularMapScreen.Visible = false;

        WorldTileMapScreenScene.Initialize(mapSeeds, abilities);
    }

    /// <summary>
    /// Starts map generation using the irregular mesh system.
    /// </summary>
    private void StartIrregularMeshGeneration(CardSignature[] mapSeeds, CardSignature[] abilities)
    {
        // Show irregular screen, hide regular grid screen
        WorldTileMapScreenScene.Visible = false;
        IrregularMapScreen.Visible = true;

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
    private void ResetScreen(MapGenerationType? finishedKind)
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
