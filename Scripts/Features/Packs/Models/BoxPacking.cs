using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     What packing a list of items gives: the contents with every item that fit, and the items left over, in their
///     order (the ones that did not fit, or that had to come out to make the change).
/// </summary>
public sealed record BoxPacking(BoxContents Contents, IReadOnlyList<StoredItem> Left);
