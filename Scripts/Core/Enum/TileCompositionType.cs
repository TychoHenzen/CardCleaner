public enum TileCompositionType
{
    Monolithic,    // Complete tile, no overlays needed
    Base,          // Accepts overlays from higher layers
    Overlay,       // Sits on compatible base tiles
    Transition     // Special transition between biomes
}