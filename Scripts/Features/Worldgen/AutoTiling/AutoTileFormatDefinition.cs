using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Features.Worldgen.AutoTiling;

/// <summary>
/// Complete definition of an auto-tile format, specifying the bitmask algorithm,
/// which bitmask values are allowed, and the atlas coordinates for each variant.
/// </summary>
public sealed class AutoTileFormatDefinition
{
    private readonly Dictionary<int, VariantDefinition> _variantMappings;
    private readonly HashSet<int> _allowedBitmasks;

    /// <summary>
    /// Creates a new auto-tile format definition.
    /// </summary>
    /// <param name="name">Unique name for this format (e.g., "corner16", "hedge4").</param>
    /// <param name="bitmaskType">The neighbor computation algorithm to use.</param>
    /// <param name="allowedBitmasks">Set of valid bitmask values. Missing values are forbidden.</param>
    /// <param name="variantMappings">Mapping from bitmask values to variant definitions.</param>
    /// <param name="isBuiltIn">True if this is a built-in format that cannot be modified.</param>
    public AutoTileFormatDefinition(
        string name,
        BitmaskType bitmaskType,
        HashSet<int> allowedBitmasks,
        Dictionary<int, VariantDefinition> variantMappings,
        bool isBuiltIn = false)
    {
        Name = name;
        BitmaskType = bitmaskType;
        IsBuiltIn = isBuiltIn;
        _allowedBitmasks = allowedBitmasks;
        _variantMappings = variantMappings;
    }

    /// <summary>
    /// Unique name for this format (case-insensitive for lookup).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The neighbor computation algorithm used by this format.
    /// </summary>
    public BitmaskType BitmaskType { get; }

    /// <summary>
    /// True if this is a built-in format that cannot be modified or deleted.
    /// </summary>
    public bool IsBuiltIn { get; }

    /// <summary>
    /// Read-only view of allowed bitmask values.
    /// </summary>
    public IReadOnlySet<int> AllowedBitmasks => _allowedBitmasks;

    /// <summary>
    /// Read-only view of variant mappings.
    /// </summary>
    public IReadOnlyDictionary<int, VariantDefinition> VariantMappings => _variantMappings;

    /// <summary>
    /// Gets the variant definition for a specific bitmask value.
    /// </summary>
    /// <param name="bitmask">The bitmask value (0-15 for 4-bit, 0-255 for 8-bit).</param>
    /// <returns>The variant definition if the bitmask is allowed and mapped; null otherwise.</returns>
    public VariantDefinition? GetVariant(int bitmask)
    {
        if (!_allowedBitmasks.Contains(bitmask))
            return null;

        return _variantMappings.TryGetValue(bitmask, out var variant) ? variant : null;
    }

    /// <summary>Checks whether a bitmask value is allowed in this format.</summary>
    public bool IsBitmaskAllowed(int bitmask) => _allowedBitmasks.Contains(bitmask);

    /// <summary>
    /// Gets the expected variant count based on the bitmask type.
    /// For custom formats with filtered bitmasks, returns the actual count of allowed values.
    /// </summary>
    public int GetExpectedVariantCount()
    {
        // For custom formats, return the actual allowed count
        if (!IsBuiltIn)
            return _allowedBitmasks.Count;

        // For built-in formats, return the theoretical maximum
        return BitmaskType switch
        {
            BitmaskType.Corner4 => 16,
            BitmaskType.Edge4 => 16,
            BitmaskType.Full8 => 47, // Only 47 valid blob combinations
            _ => 16
        };
    }

    /// <summary>
    /// Gets the maximum possible bitmask value for this format's bitmask type.
    /// </summary>
    public int GetMaxBitmaskValue()
    {
        return BitmaskType switch
        {
            BitmaskType.Corner4 => 15,   // 4-bit: 0-15
            BitmaskType.Edge4 => 15,     // 4-bit: 0-15
            BitmaskType.Full8 => 255,    // 8-bit: 0-255
            _ => 15
        };
    }

    /// <summary>
    /// Gets the maximum multi-cell bounds across all variants in this format.
    /// Returns null if no variants are multi-cell.
    /// </summary>
    /// <returns>Tuple of (maxSize, offset) for reservation, or null if all variants are 1x1.</returns>
    public (Vector2I Size, Vector2I Offset)? GetMaxMultiCellBounds()
    {
        Vector2I? maxSize = null;
        Vector2I? maxOffset = null;

        foreach (var variant in _variantMappings.Values)
        {
            if (variant.IsMultiCell)
            {
                if (maxSize == null ||
                    variant.Size.X * variant.Size.Y > maxSize.Value.X * maxSize.Value.Y)
                {
                    maxSize = variant.Size;
                    maxOffset = variant.Offset;
                }
            }
        }

        return maxSize.HasValue ? (maxSize.Value, maxOffset!.Value) : null;
    }

    /// <summary>
    /// Creates a simple variant definition using just atlas coordinates.
    /// Useful for building variant mappings in factory methods.
    /// </summary>
    public static VariantDefinition SimpleVariant(int x, int y) =>
        new(new Vector2I(x, y));

    /// <summary>
    /// Creates a set of all possible bitmask values for a bitmask type.
    /// </summary>
    public static HashSet<int> AllBitmasksFor(BitmaskType type)
    {
        return type switch
        {
            BitmaskType.Corner4 => Enumerable.Range(0, 16).ToHashSet(),
            BitmaskType.Edge4 => Enumerable.Range(0, 16).ToHashSet(),
            BitmaskType.Full8 => NeighborBitmask8.GetValid47Masks().ToHashSet(),
            _ => Enumerable.Range(0, 16).ToHashSet()
        };
    }
}
