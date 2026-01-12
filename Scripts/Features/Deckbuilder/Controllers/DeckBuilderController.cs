using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// Enum to select between map generation types.
/// </summary>
public enum MapGenerationType
{
    RegularGrid,
    IrregularMesh
}

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

    [Export] public DeckSlot AbilityDeckSlot { get; set; } = null!;
    [Export] public DeckSlot MapCardSlot { get; set; } = null!;
    [Export] public InteractableButton ActivateButton { get; set; } = null!;
    [Export] public PackedScene BattleScreenScene { get; set; } = null!;
    [Export] public SimpleWorldMapScreen WorldTileMapScreenScene { get; set; } = null!;
    [Export] public IrregularWorldMapScreen IrregularMapScreen { get; set; }
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

        // Subscribe to game session state changes
        ServiceLocator.Get<IGameSessionService>(gameSession =>
        {
            _gameSession = gameSession;
            gameSession.StateChanged += OnSessionStateChanged;
        });

        // Initially disable the button
        UpdateButtonState();
    }

    public override void _ExitTree()
    {
        // Clean up event subscriptions
        if (_gameSession != null)
        {
            _gameSession.StateChanged -= OnSessionStateChanged;
        }
    }

    /// <summary>
    /// Called when the game session state changes.
    /// Re-enables the button when session completes (map stays visible).
    /// </summary>
    private void OnSessionStateChanged(SessionState newState)
    {
        if (newState == SessionState.SessionComplete)
        {
            ILog.Print("Session complete - map revealed, awaiting reset");

            // Enable button and flag that next press should just reset the view
            _awaitingMapReset = true;
            ActivateButton.SetEnabled(true);
        }
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

            _awaitingMapReset = false;
            ResetCurrentScreen();

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

        ILog.Print($"Cards present - starting {MapType} map generation...");

        // Consume the seed cards and ability deck
        var mapSeeds = MapCardSlot.ConsumeAllCardSignatures();
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        if (mapSeeds.Count == 0) return;

        // Initialize the appropriate screen based on MapType
        switch (MapType)
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

        ILog.Print($"{MapType} map screen generated successfully!");
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
        if (IrregularMapScreen == null)
        {
            ILog.Error("IrregularMapScreen not assigned! Cannot generate irregular mesh map.");
            return;
        }

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
    /// Resets the currently active screen to initial state.
    /// </summary>
    private void ResetCurrentScreen()
    {
        switch (MapType)
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
