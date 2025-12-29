using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Single tile affinity entry mapping a tile ID to an affinity multiplier.
/// Values > 1.0 increase likelihood, values < 1.0 decrease likelihood.
/// </summary>
[Tool]
[GlobalClass]
public partial class TileAffinityEntry : Resource
{
    private const string DefaultTileId = "";
    private const float DefaultAffinity = 1.0f;

    public TileAffinityEntry() { }

    public TileAffinityEntry(string tileId, float affinity = DefaultAffinity)
    {
        TileId = tileId;
        Affinity = affinity;
    }

    /// <summary>The tile ID this affinity applies to.</summary>
    [Export] public string TileId { get; set; } = DefaultTileId;

    /// <summary>
    /// Affinity multiplier. Values > 1.0 increase likelihood, < 1.0 decrease.
    /// A value of 2.0 doubles the chance, 0.5 halves it.
    /// </summary>
    [Export(PropertyHint.Range, "0,10,0.1")]
    public float Affinity { get; set; } = DefaultAffinity;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileId) => true,
            nameof(Affinity) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileId) => DefaultTileId,
            nameof(Affinity) => DefaultAffinity,
            _ => base._PropertyGetRevert(property)
        };
    }
}
