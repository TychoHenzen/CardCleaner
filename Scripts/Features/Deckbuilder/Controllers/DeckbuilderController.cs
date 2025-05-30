using Godot;
using CardSlot = CardCleaner.Scripts.Features.Deckbuilder.Models.CardSlot;
using DeckSlot = CardCleaner.Scripts.Features.Deckbuilder.Models.DeckSlot;
using TileMapScreen = CardCleaner.Scripts.Features.Deckbuilder.Models.TileMapScreen;

// for CardSignature

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

public partial class DeckBuilderController : Node
{
    private DeckSlot _abilityDeckSlot;

    private CardSlot _mapCardSlot;

    // Slot where a stack of ability cards is dropped (expects a DeckSlot node)
    [Export] public NodePath AbilityDeckSlotPath;

    [Export] public PackedScene BattleScreenScene;

    // Slot where the single map‐seed card is dropped (expects a CardSlot node)
    [Export] public NodePath MapCardSlotPath;

    // Scenes for the two UI screens
    [Export] public PackedScene TileMapScreenScene;

    public override void _Ready()
    {
        _abilityDeckSlot = GetNode<DeckSlot>(AbilityDeckSlotPath);
        _mapCardSlot = GetNode<CardSlot>(MapCardSlotPath);

        // Listen for when cards are dropped into either slot
        _abilityDeckSlot.CardsChanged += OnSlotsUpdated;
        _mapCardSlot.CardChanged += OnSlotsUpdated;
    }

    private void OnSlotsUpdated()
    {
        // Only proceed once both slots are populated
        if (!_abilityDeckSlot.HasCards || !_mapCardSlot.HasCard)
            return;

        // Consume the seed card and ability deck
        var mapSeed = _mapCardSlot.ConsumeCardSignature();
        var abilities = _abilityDeckSlot.ConsumeAllCardSignatures();

        // Instantiate and initialize the map screen
        var mapScreen = TileMapScreenScene.Instantiate<TileMapScreen>();
        mapScreen.Initialize(mapSeed, abilities);
        AddChild(mapScreen);

        // Clear slots so the machine can’t be reused until the map closes
        _abilityDeckSlot.Clear();
        _mapCardSlot.Clear();
    }
}