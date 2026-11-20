namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>The move one click makes and how many items it moves.</summary>
public readonly record struct TransferDecision(TransferMove Move, int Count)
{
    public static TransferDecision None { get; } = new(TransferMove.None, 0);
}
