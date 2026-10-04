namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Extended fog of war interface for active visibility calculation.
/// Implementations calculate visibility from an observer position.
/// </summary>
public interface IActiveFogOfWar : IFogOfWar
{
    /// <summary>
    /// Update visibility from an observer cell position.
    /// </summary>
    void UpdateVisibility(int observerCellId);

    /// <summary>
    /// Vision range in world units.
    /// </summary>
    float VisionRange { get; set; }
}
