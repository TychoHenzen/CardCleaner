using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models
{
    public partial class TileMapScreen : Node2D
    {
        [Export] public PackedScene TileSetScene;
        [Export] public NodePath TileMapPath;
        [Export] public NodePath PlayerAgentPath;

        private TileMapLayer _tileMap;
        private Node2D _playerAgent;

        private CardSignature _mapSeed;
        private List<CardSignature> _abilityDeck;

        public override void _Ready()
        {
            _tileMap = GetNode<TileMapLayer>(TileMapPath);
            _playerAgent = GetNode<Node2D>(PlayerAgentPath);
        }

        public void Initialize(CardSignature mapSeed, List<CardSignature> abilities)
        {
            _mapSeed = mapSeed;
            _abilityDeck = abilities;

            GenerateMap();
            SpawnPlayer();
        }

        private void GenerateMap()
        {
            // TODO: generate tile layout based on _mapSeed.signatureBytes
            // placeholder: fill a 10x10 area with tile ID 0
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    _tileMap.SetCell(new Vector2I(x, y), 0);
                }
            }
        }

        private void SpawnPlayer()
        {
            // position player at center of map
            Vector2I center = new Vector2I(5, 5);
            Vector2 worldPos = _tileMap.GetCellAtlasCoords(center);
            _playerAgent.Position = worldPos;

            // TODO: start autonomous movement
        }
    }
}