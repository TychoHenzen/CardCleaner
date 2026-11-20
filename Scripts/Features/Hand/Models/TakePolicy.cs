namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>How much a plain click on a store's stock takes.</summary>
public enum TakePolicy
{
    /// <summary>One item: a pile, an open cardboard box, a shelf level.</summary>
    One,

    /// <summary>Every item when they all fit in the hand, nothing otherwise: a deck.</summary>
    WholeIfFits
}
