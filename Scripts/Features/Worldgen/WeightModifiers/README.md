# Weight Modifier System

An extensible tile selection system using the Chain of Responsibility pattern. Modifiers adjust tile weights multiplicatively during map generation.

## Architecture

```
WeightedTileSelector
    |
    v
WeightModifierPipeline (applies modifiers in order)
    |
    +-- BiomeAffinityModifier (biome preferences)
    +-- AdjacencyBoostModifier (neighbor clustering)
    +-- DecorationSpacingModifier (decoration distribution)
    +-- ... (custom modifiers)
```

## Core Components

### IWeightModifier Interface
```csharp
public interface IWeightModifier
{
    void ApplyModifier(TileSelectionContext context);
}
```

Modifiers receive a context with:
- `Position`: Current tile position
- `PlacedTiles`: Already placed tiles (IReadOnlyDictionary)
- `CurrentBiome`: Biome at this position
- `Rng`: Random number generator
- `Weights`: Mutable dictionary of tile ID → weight

### TileSelectionContext
Provides helper methods:
- `GetNeighbors()`: Cardinal neighbor positions
- `GetNeighborTiles()`: Placed neighbor tiles
- `GetTileAt(offset)`: Get tile at relative position

### WeightModifierPipeline
```csharp
var pipeline = new WeightModifierPipeline()
    .AddModifier(new BiomeAffinityModifier())
    .AddModifier(new AdjacencyBoostModifier())
    .AddModifier(new DecorationSpacingModifier(decorationIds));
```

### WeightedTileSelector
```csharp
var selector = new WeightedTileSelector(tileRegistry, pipeline);
var tileId = selector.SelectTile(position, placedTiles, biome, rng, candidateTiles);
```

## Built-in Modifiers

### BiomeAffinityModifier
Multiplies tile weights by biome-specific affinity values.
```csharp
var modifier = new BiomeAffinityModifier()
    .WithAffinity(forestAffinity)  // BiomeTileAffinity resource
    .WithAffinity(desertAffinity);
```

### AdjacencyBoostModifier
Boosts tiles matching adjacent already-placed tiles (creates clustering).
```csharp
// Default: 1.5x boost per matching neighbor
var modifier = new AdjacencyBoostModifier();

// With custom grouping (grass_1, grass_2 treated as same type)
var modifier = new AdjacencyBoostModifier(AdjacencyBoostModifier.StripNumericSuffix);
modifier.BoostPerNeighbor = 2.0f; // Stronger clustering
```

### DecorationSpacingModifier
Reduces decoration weights near other decorations.
```csharp
var decorationIds = new HashSet<string> { "tree", "rock", "bush" };
var modifier = new DecorationSpacingModifier(decorationIds)
{
    BaseRadius = 2,      // Any decoration within 2 tiles reduces weight
    SameTypeRadius = 4,  // Same decoration within 4 tiles reduces more
    MinWeight = 0.1f     // Never go below 10% weight
};
```

## Creating Custom Modifiers

```csharp
public sealed class ElevationModifier : IWeightModifier
{
    private readonly float[,] _elevationMap;
    private readonly float _preferredElevation;

    public ElevationModifier(float[,] elevationMap, float preferredElevation)
    {
        _elevationMap = elevationMap;
        _preferredElevation = preferredElevation;
    }

    public void ApplyModifier(TileSelectionContext context)
    {
        var elevation = _elevationMap[context.Position.Y, context.Position.X];
        var difference = Math.Abs(elevation - _preferredElevation);
        var multiplier = 1.0f / (1.0f + difference);  // Closer = higher weight

        foreach (var tileId in new List<string>(context.Weights.Keys))
        {
            context.Weights[tileId] *= multiplier;
        }
    }
}
```

## Integration with SimpleMapGenerator

```csharp
// Create pipeline with modifiers
var pipeline = new WeightModifierPipeline()
    .AddModifier(new BiomeAffinityModifier().WithAffinity(forestAffinity))
    .AddModifier(new AdjacencyBoostModifier(AdjacencyBoostModifier.StripNumericSuffix))
    .AddModifier(new DecorationSpacingModifier(decorationTileIds));

// Create selector
var selector = new WeightedTileSelector(tileRegistry, pipeline);

// Pass to generator
var generator = new SimpleMapGenerator(
    rng, biomeProvider, tileRegistry,
    blobGenerator: null,
    autoTileResolver: autoTileResolver,
    weightedSelector: selector  // New parameter
);
```

## Weight Semantics

- All tiles start with weight **1.0**
- Modifiers **multiply** weights (not add)
- Weight **0 or negative** = tile excluded
- Final selection is **weighted random** based on remaining weights

## Best Practices

1. **Order matters**: Modifiers are applied sequentially. Put broader modifiers (biome) before specific ones (spacing).

2. **Avoid division**: Multiply by values < 1 instead of dividing.

3. **Preserve keys**: When iterating weights, copy keys first to avoid modification during iteration:
   ```csharp
   foreach (var tileId in new List<string>(context.Weights.Keys))
   ```

4. **Use grouping functions**: For tiles with variants (grass_1, grass_2), use grouping to treat them as one type.
