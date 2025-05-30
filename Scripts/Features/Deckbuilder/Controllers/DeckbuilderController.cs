using System.Collections.Generic;
using CardCleaner.Scripts.Features.DeckBuilder;
using Godot;

// for CardSignature

namespace CardCleaner.Scripts.Features.Deckbuilder.Controllers
{
    public partial class DeckBuilderController : Node
    {
        // Slot where a stack of ability cards is dropped (expects a DeckSlot node)
        [Export] public NodePath AbilityDeckSlotPath;
        // Slot where the single map‐seed card is dropped (expects a CardSlot node)
        [Export] public NodePath MapCardSlotPath;

        // Scenes for the two UI screens
        [Export] public PackedScene TileMapScreenScene;
        [Export] public PackedScene BattleScreenScene;

        private DeckSlot _abilityDeckSlot;
        private CardSlot _mapCardSlot;

        public override void _Ready()
        {
            _abilityDeckSlot = GetNode<DeckSlot>(AbilityDeckSlotPath);
            _mapCardSlot     = GetNode<CardSlot>(MapCardSlotPath);

            // Listen for when cards are dropped into either slot
            _abilityDeckSlot.CardsChanged += OnSlotsUpdated;
            _mapCardSlot.CardChanged    += OnSlotsUpdated;
        }

        private void OnSlotsUpdated()
        {
            // Only proceed once both slots are populated
            if (!_abilityDeckSlot.HasCards || !_mapCardSlot.HasCard)
                return;

            // Consume the seed card and ability deck
            CardSignature mapSeed    = _mapCardSlot.ConsumeCardSignature();
            List<CardSignature> abilities = _abilityDeckSlot.ConsumeAllCardSignatures();

            // Instantiate and initialize the map screen
            var mapScreen = TileMapScreenScene.Instantiate<TileMapScreen>();
            mapScreen.Initialize(mapSeed, abilities);
            AddChild(mapScreen);

            // Clear slots so the machine can’t be reused until the map closes
            _abilityDeckSlot.Clear();
            _mapCardSlot.Clear();
        }
    }
}