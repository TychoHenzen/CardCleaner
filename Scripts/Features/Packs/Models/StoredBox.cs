using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     A cardboard box as the transfer rule moves it: the scene it is built from, the seed of what its booster boxes
///     open into (null while it has none), what it holds (null while it is fresh, still packed with its eight booster
///     boxes) and whether its flaps are open. It exists only between a take and a put; a cardboard box never goes into
///     another one (its shape is larger than the inside of a box), and a save stores a box as a container record.
/// </summary>
public sealed record StoredBox(
    string ScenePath,
    ulong? Seed,
    IReadOnlyList<StoredItem>? Contents,
    bool LidOpen) : StoredItem
{
    public override ItemShape Shape => ItemShape.Box;
}
