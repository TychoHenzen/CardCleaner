using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>
///     Waits, frame by frame, for bevel maps that <see cref="CardEffectNormalMapCache" /> bakes off the main thread;
///     not a suite.
/// </summary>
public static class CardEffectBakeWait
{
    /// <summary>
    ///     A wall-clock bound, because headless frames are not throttled and a frame count would run out before the
    ///     bakes do. It is a timeout, not a speed check: one bake takes milliseconds.
    /// </summary>
    public const double BoundSeconds = 30;

    /// <summary>Waits until <paramref name="done" /> holds and fails the test if it still does not after the bound.</summary>
    public static Task Until(Func<bool> done, string what) => BoundedWait.Expect(done, what, BoundSeconds);

    /// <summary>Requests the bevel map of <paramref name="art" /> and waits until the cache hands it over.</summary>
    public static async Task<ImageTexture> BakeOf(Texture2D art)
    {
        ImageTexture? map = null;
        CardEffectNormalMapCache.Request(art, baked => map = baked);
        await Until(() => map != null, "the bevel map bake");
        return map!;
    }

    public static Task Frames(int count) => BoundedWait.ProcessFrames(count);
}
