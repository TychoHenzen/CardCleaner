using System;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Bakes a card's art into its bevel map once, off the main thread, and hands the same
///     <see cref="ImageTexture" /> to everyone who asks for that art texture.
/// </summary>
public static class CardEffectNormalMapCache
{
    private static CardEffectBakePipeline _pipeline = new();

    /// <summary>
    ///     Hands the bevel map of <paramref name="art" /> to <paramref name="onReady" /> on the main thread: at once
    ///     when it is already baked, otherwise once it is. A request never bakes while the caller waits.
    /// </summary>
    public static void Request(Texture2D art, Action<ImageTexture> onReady)
    {
        _pipeline.Request(art, onReady);
    }

    /// <summary>Forgets every map and drops the bakes in flight, so one test's bakes never reach the next.</summary>
    public static void ResetForTesting()
    {
        _pipeline.Stop();
        _pipeline = new CardEffectBakePipeline();
    }
}
