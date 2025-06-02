using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public partial class AnimatedTileManager : Node
{
    private struct TileAnimation
    {
        public Vector2I Position;
        public WfcTile Tile;
        public int CurrentFrame;
        public float TimeUntilNextFrame;
    }
    
    private readonly List<TileAnimation> _animations = new();
    private TileMapLayer _tileMapLayer;
    
    public void Initialize(TileMapLayer tileMapLayer)
    {
        _tileMapLayer = tileMapLayer;
    }
    
    public void RegisterAnimatedTile(Vector2I position, WfcTile tile)
    {
        if (!tile.IsAnimated) return;
        
        _animations.Add(new TileAnimation
        {
            Position = position,
            Tile = tile,
            CurrentFrame = 0,
            TimeUntilNextFrame = tile.FrameDuration
        });
    }
    
    public override void _Process(double delta)
    {
        if (_tileMapLayer == null) return;
        
        for (int i = 0; i < _animations.Count; i++)
        {
            var animation = _animations[i];
            animation.TimeUntilNextFrame -= (float)delta;
            
            if (animation.TimeUntilNextFrame <= 0.0f)
            {
                // Advance to next frame
                animation.CurrentFrame = (animation.CurrentFrame + 1) % animation.Tile.AnimationFrames.Length;
                animation.TimeUntilNextFrame = animation.Tile.FrameDuration;
                
                // Update tile map
                var frameCoords = animation.Tile.AnimationFrames[animation.CurrentFrame];
                _tileMapLayer.SetCell(animation.Position, 0, frameCoords);
            }
            
            _animations[i] = animation;
        }
    }
}