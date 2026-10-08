using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Debug;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using Godot;

namespace CardCleaner.Tests.Features.Card.Debug;

[TestSuite]
[RequireGodotRuntime]
public class CardEffectComparisonLayoutTest
{
    private static readonly Vector2 CardSize =
        new(CardEffectComparisonLayout.CardWidth, CardEffectComparisonLayout.CardHeight);

    private static Rect2 CardRect(Vector2 center)
    {
        return new Rect2(center - CardSize / 2f, CardSize);
    }

    private static IEnumerable<Vector2> AllCenters(int batchCount)
    {
        foreach (var rarity in Enum.GetValues<CardRarity>())
        foreach (var tier in Enum.GetValues<IntensityTier>())
            yield return CardEffectComparisonLayout.CellCenter(rarity, tier);
        for (var i = 0; i < batchCount; i++)
            yield return CardEffectComparisonLayout.BatchCenter(i);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RaritiesRunLeftToRightAndTiersTopToBottom()
    {
        var commonDormant = CardEffectComparisonLayout.CellCenter(CardRarity.Common, IntensityTier.Dormant);
        var rareDormant = CardEffectComparisonLayout.CellCenter(CardRarity.Rare, IntensityTier.Dormant);
        var commonIntense = CardEffectComparisonLayout.CellCenter(CardRarity.Common, IntensityTier.Intense);

        AssertThat(rareDormant.X).IsGreater(commonDormant.X);
        AssertThat(rareDormant.Y).IsEqual(commonDormant.Y);
        AssertThat(commonIntense.Y).IsLess(commonDormant.Y);
        AssertThat(commonIntense.X).IsEqual(commonDormant.X);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoCardOverlapsAnotherCardOrALabel()
    {
        var cards = AllCenters(100).Select(CardRect).ToArray();
        var labels = AllCenters(0).Select(CardEffectComparisonLayout.LabelArea).ToArray();

        for (var i = 0; i < cards.Length; i++)
        {
            for (var j = i + 1; j < cards.Length; j++)
                AssertThat(cards[i].Intersects(cards[j])).OverrideFailureMessage($"cards {i} and {j}").IsFalse();
            foreach (var label in labels)
                AssertThat(cards[i].Intersects(label)).OverrideFailureMessage($"card {i} and a label").IsFalse();
        }
    }

    [TestCase(0)]
    [TestCase(10)]
    [TestCase(100)]
    [TestCategory("Unit")]
    public static void TheBoundsHoldEveryCardAndLabel(int batchCount)
    {
        var bounds = CardEffectComparisonLayout.Bounds(batchCount).Grow(0.0001f);

        foreach (var center in AllCenters(batchCount))
            AssertThat(bounds.Encloses(CardRect(center))).IsTrue();
        foreach (var center in AllCenters(0))
            AssertThat(bounds.Encloses(CardEffectComparisonLayout.LabelArea(center))).IsTrue();
    }

    [TestCase(16f / 9f)]
    [TestCase(4f / 3f)]
    [TestCase(9f / 16f)]
    [TestCategory("Unit")]
    public static void TheCameraDistanceFitsTheBoundsTightlyAtAnyAspect(float aspect)
    {
        const float fov = 35f;
        var size = CardEffectComparisonLayout.Bounds(0).Size;

        var distance = CardEffectComparisonLayout.CameraDistance(size, fov, aspect);

        var halfHeight = distance * Mathf.Tan(Mathf.DegToRad(fov) / 2f);
        var visible = new Vector2(halfHeight * aspect, halfHeight) * 2f;
        AssertThat(visible.X).IsGreaterEqual(size.X);
        AssertThat(visible.Y).IsGreaterEqual(size.Y);
        // Tight on the binding side: the margin is the only slack.
        var slack = Mathf.Min(visible.X / size.X, visible.Y / size.Y);
        AssertThat(slack).IsLessEqual(1f + 2f * CardEffectComparisonLayout.FrameMargin + 0.001f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheBatchCyclesThroughEveryRarityAndTier()
    {
        var cells = Enum.GetValues<CardRarity>()
            .SelectMany(rarity => Enum.GetValues<IntensityTier>()
                .Select(tier => CardEffectComparisonGrid.SignatureFor(rarity, tier).ToDebugString()))
            .ToHashSet();

        var batch = Enumerable.Range(0, 15)
            .Select(i => CardEffectComparisonLayout.BatchSignature(i).ToDebugString())
            .ToHashSet();

        AssertThat(batch.SetEquals(cells)).IsTrue();
        AssertThat(CardEffectComparisonLayout.BatchSignature(15).ToDebugString())
            .IsEqual(CardEffectComparisonLayout.BatchSignature(0).ToDebugString());
    }
}
