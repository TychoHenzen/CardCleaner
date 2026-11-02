using System;
using System.Linq;

namespace CardCleaner.Scripts.Features.SaveSlots.Models;

/// <summary>
///     A player-chosen save slot name. Its key is the lower-case form, which is also its folder name, so two names that
///     differ only by case are the same slot.
/// </summary>
public readonly record struct SaveSlotName
{
    public const int MaxLength = 32;

    public SaveSlotName(string value)
    {
        Value = value;
    }

    public string Value { get; init; }

    private static readonly string[] ReservedNames = { "con", "prn", "aux", "nul" };

    public string Key => Value.ToLowerInvariant();

    public string Folder => Key;

    // ASSUMPTION: letters and digits are limited to ASCII, so a folder name never depends on the Windows code page or
    // on Unicode normalisation. The plan says "letters" and "digits" without naming a character set.
    public static bool TryCreate(string? raw, out SaveSlotName name, out string reason)
    {
        name = default;
        var value = (raw ?? string.Empty).Trim();

        if (value.Length == 0)
        {
            reason = "The slot name is empty.";
            return false;
        }

        if (value.Length > MaxLength)
        {
            reason = $"The slot name is longer than {MaxLength} characters.";
            return false;
        }

        var bad = value.FirstOrDefault(c => !IsAllowed(c));
        if (bad != default)
        {
            reason = $"The slot name contains '{bad}'. Use only letters, digits, spaces, '-' and '_'.";
            return false;
        }

        if (ReservedNames.Contains(value.ToLowerInvariant()))
        {
            reason = $"'{value}' is a reserved name on Windows.";
            return false;
        }

        name = new SaveSlotName(value);
        reason = string.Empty;
        return true;
    }

    public bool Equals(SaveSlotName other) => Key == other.Key;

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);

    private static bool IsAllowed(char c) =>
        char.IsAsciiLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';
}
