using CardCleaner.Scripts.Features.Deckbuilder.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers;

public partial class DeckBuilderController : Node
{
    [Export] public DeckSlot AbilityDeckSlot { get; set; }
    [Export] public CardSlot MapCardSlot { get; set; }
    [Export] public PackedScene BattleScreenScene { get; set; }
    [Export] public PackedScene TileMapScreenScene { get; set; }

    public override void _Ready()
    {

        // Listen for when cards are dropped into either slot
        AbilityDeckSlot.CardsChanged += OnSlotsUpdated;
        MapCardSlot.CardChanged += OnSlotsUpdated;
    }

    private void OnSlotsUpdated()
    {
        // Only proceed once both slots are populated
        if (!AbilityDeckSlot.HasCards || !MapCardSlot.HasCard)
            return;

        // Consume the seed card and ability deck
        var mapSeed = MapCardSlot.ConsumeCardSignature();
        var abilities = AbilityDeckSlot.ConsumeAllCardSignatures();

        // Instantiate and initialize the map screen
        var mapScreen = TileMapScreenScene.Instantiate<TileMapScreen>();
        mapScreen.Initialize(mapSeed, abilities);
        AddChild(mapScreen);

        // Clear slots so the machine can’t be reused until the map closes
        AbilityDeckSlot.Clear();
        MapCardSlot.Clear();
    }
}