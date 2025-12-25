using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

/// <summary>
/// Updated DeckBuilder Controller that spawns 3D world screens.
/// </summary>
public partial class DeckBuilderController : Node
{
    [Export] public DeckSlot AbilityDeckSlot { get; set; } = null!;
    [Export] public DeckSlot MapCardSlot { get; set; } = null!;
    [Export] public InteractableButton ActivateButton { get; set; } = null!;
    [Export] public PackedScene BattleScreenScene { get; set; } = null!;
    [Export] public SimpleWorldMapScreen WorldTileMapScreenScene { get; set; } = null!;
    [Export] public Vector3 ScreenSpawnPosition { get; set; } = Vector3.Zero;

    public override void _Ready()
    {
        // Ensure MapCardSlot has capacity of 1 for single-card usage
        MapCardSlot.Capacity = 5;

        // Listen for when cards are dropped into slots (for UI feedback only)
        AbilityDeckSlot.CardsChanged += OnSlotsUpdated;
        MapCardSlot.CardsChanged += OnSlotsUpdated; // Changed from CardChanged to CardsChanged

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
        var canActivate = AbilityDeckSlot.HasCards && MapCardSlot.HasCard; // Using HasCard for semantic clarity
        ActivateButton.SetEnabled(canActivate);
    }

    /// <summary>
    /// Called when the activation button is pressed
    /// </summary>
    private void OnButtonPressed()
    {
        // Double-check that we have cards in both slots
        if (!AbilityDeckSlot.HasCards || !MapCardSlot.HasCard)
        {
            ILog.Error("Button pressed but slots not properly filled!");
            return;
        }

        ILog.Print("Activation button pressed! Processing cards...");

        // Consume the seed card and ability deck
        var mapSeed = MapCardSlot.ConsumeCardSignature(); // Using convenience method
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        // Instantiate and initialize the 3D map screen
        if (mapSeed != null)
            WorldTileMapScreenScene.Initialize(new[] { mapSeed }, abilities.ToArray());

        // Clear slots and disable button
        AbilityDeckSlot.Clear();
        MapCardSlot.Clear();
        UpdateButtonState();

        ILog.Print("3D map screen generated successfully!");
    }
}
