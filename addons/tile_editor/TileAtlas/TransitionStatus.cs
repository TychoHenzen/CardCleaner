#if TOOLS
namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Status of a terrain transition pair.
/// </summary>
internal enum TransitionStatus
{
    /// <summary>No transition defined for this pair.</summary>
    Missing,
    /// <summary>Covered by a compositable auto-tile.</summary>
    Compositable,
    /// <summary>Covered by a fixed transition auto-tile.</summary>
    Fixed,
    /// <summary>Same terrain - no transition needed.</summary>
    Self
}
#endif
