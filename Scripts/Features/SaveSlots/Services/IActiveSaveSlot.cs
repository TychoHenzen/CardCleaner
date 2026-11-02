using System;
using CardCleaner.Scripts.Features.SaveSlots.Models;

namespace CardCleaner.Scripts.Features.SaveSlots.Services;

/// <summary>
///     The slot the game is currently playing in. It starts as the "default" slot until one is selected.
/// </summary>
public interface IActiveSaveSlot
{
    SaveSlotName Current { get; }

    void Select(SaveSlotName name);

    /// <summary>Raised on every <see cref="Select" />, with the newly selected slot.</summary>
    event Action<SaveSlotName>? Changed;
}
