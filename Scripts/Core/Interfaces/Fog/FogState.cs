namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Visibility state for fog of war cells.
/// </summary>
public enum FogState
{
    /// <summary>Never been seen - completely hidden.</summary>
    Hidden,

    /// <summary>Previously seen but not currently visible - shown dimmed.</summary>
    Revealed,

    /// <summary>Currently visible - shown fully.</summary>
    Visible
}
