using System;

namespace CardCleaner.Scripts.Features.SaveSlots.Models;

/// <summary>
///     One slot as the slot list shows it. <see cref="SavedAtUtc" /> is null when the save has no readable time.
///     <see cref="IsReadable" /> is false when the slot has no save file or its save file is refused.
/// </summary>
public sealed record SaveSlotInfo(SaveSlotName Name, DateTime? SavedAtUtc, bool IsReadable);
