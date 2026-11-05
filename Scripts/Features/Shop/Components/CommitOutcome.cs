namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>What a commit did to a placement session.</summary>
internal enum CommitOutcome
{
    /// <summary>The piece stands on the floor; the session is over.</summary>
    Placed,

    /// <summary>Nothing was committed; the session goes on.</summary>
    Kept,

    /// <summary>The carried piece was freed by a load; the session has nothing left to place.</summary>
    Gone
}
