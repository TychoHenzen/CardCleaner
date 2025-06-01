using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// Updated DeckBuilder Controller that spawns 3D world screens.
/// </summary>
public partial class DeckBuilderController : Node
{
    [Export] public DeckSlot AbilityDeckSlot { get; set; }
    [Export] public CardSlot MapCardSlot { get; set; }
    [Export] public InteractableButton ActivateButton { get; set; }
    [Export] public PackedScene BattleScreenScene { get; set; }
    [Export] public WorldTileMapScreen WorldTileMapScreenScene { get; set; } // Changed from TileMapScreenScene
    [Export] public Vector3 ScreenSpawnPosition { get; set; } = Vector3.Zero;

    public override void _Ready()
    {
        // Listen for when cards are dropped into slots (for UI feedback only)
        AbilityDeckSlot.CardsChanged += OnSlotsUpdated;
        MapCardSlot.CardChanged += OnSlotsUpdated;
        
        // Listen for button activation
        ActivateButton.ButtonPressed += OnButtonPressed;
        
        // Initially disable the button
        UpdateButtonState();
    }

    /// <summary>
    /// Called when slots are updated - only updates button state, doesn't trigger processing
    /// </summary>
    private void OnSlotsUpdated()
    {
        UpdateButtonState();
    }

    /// <summary>
    /// Updates the button's enabled state based on whether both slots are populated
    /// </summary>
    private void UpdateButtonState()
    {
        bool canActivate = AbilityDeckSlot.HasCards && MapCardSlot.HasCard;
        ActivateButton.SetEnabled(canActivate);
        
        if (canActivate)
        {
            GD.Print("[DeckBuilder] Ready to generate! Press the activation button.");
        }
        else
        {
            GD.Print("[DeckBuilder] Need cards in both slots before activation.");
        }
    }

    /// <summary>
    /// Called when the activation button is pressed
    /// </summary>
    private void OnButtonPressed()
    {
        // Double-check that we have cards in both slots
        if (!AbilityDeckSlot.HasCards || !MapCardSlot.HasCard)
        {
            GD.PrintErr("[DeckBuilder] Button pressed but slots not properly filled!");
            return;
        }

        GD.Print("[DeckBuilder] Activation button pressed! Processing cards...");

        // Consume the seed card and ability deck
        var mapSeed = MapCardSlot.ConsumeCardSignature();
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        // Instantiate and initialize the 3D map screen
        WorldTileMapScreenScene.Initialize(mapSeed, abilities);

        // Clear slots and disable button
        AbilityDeckSlot.Clear();
        MapCardSlot.Clear();
        UpdateButtonState();
        
        GD.Print("[DeckBuilder] 3D map screen generated successfully!");
    }
}