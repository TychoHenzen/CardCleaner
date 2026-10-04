using System;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Exception thrown when map generation is cancelled.
/// </summary>
public class MapGenerationCancelledException : OperationCanceledException
{
    public MapGenerationCancelledException()
        : base("Map generation was cancelled")
    {
    }

    public MapGenerationCancelledException(string message)
        : base(message)
    {
    }

    public MapGenerationCancelledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
