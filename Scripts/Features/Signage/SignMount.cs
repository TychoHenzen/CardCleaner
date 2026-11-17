namespace CardCleaner.Scripts.Features.Signage;

/// <summary>How a <see cref="SignBoard" /> is held up.</summary>
public enum SignMount
{
    /// <summary>The back of the board lies on a wall; nothing else is drawn.</summary>
    Wall,

    /// <summary>Two rods rise <see cref="SignBoard.MountLength" /> from the top edge to the ceiling.</summary>
    Hanging,

    /// <summary>One post drops <see cref="SignBoard.MountLength" /> from the bottom edge to the floor.</summary>
    Post
}
