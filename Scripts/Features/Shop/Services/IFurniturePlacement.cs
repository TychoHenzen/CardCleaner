using CardCleaner.Scripts.Features.Shop.Components;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>The placement session that a furniture box starts when the player interacts with it.</summary>
public interface IFurniturePlacement
{
    /// <summary>True while a placement session is already carrying a piece of furniture.</summary>
    bool IsActive { get; }

    /// <summary>Starts a placement session for the box. Returns false when none could start.</summary>
    bool Begin(FurnitureBox box);
}
