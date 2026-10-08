using System;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Debug;

/// <summary>
///     Where the comparison view puts its cards, in the plane the cards stand in (x right, y up, metres). The grid has
///     one column per rarity (common on the left) and one row per intensity tier (dormant at the top), each card with
///     a label below it. Batch cards, spawned to watch the frame time, fill rows below the grid.
/// </summary>
public static class CardEffectComparisonLayout
{
    public const float CardWidth = 0.32f;
    public const float CardHeight = 0.45f;
    public const float LabelGap = 0.02f;
    public const float LabelHeight = 0.11f;

    // Room on every side of the framed content, as a fraction of its size.
    public const float FrameMargin = 0.05f;

    private const float ColumnSpacing = 0.42f;
    private const float RowSpacing = 0.64f;
    // The batch keys add ten or a hundred cards, so batch rows are always full and centred.
    private const int BatchColumns = 10;
    private const float BatchGap = 0.3f;
    private const float BatchRowSpacing = 0.55f;

    private static readonly CardRarity[] Rarities = Enum.GetValues<CardRarity>();
    private static readonly IntensityTier[] Tiers = Enum.GetValues<IntensityTier>();

    public static Vector2 CellCenter(CardRarity rarity, IntensityTier tier)
    {
        return new Vector2(
            ((int)rarity - (Rarities.Length - 1) / 2f) * ColumnSpacing,
            ((Tiers.Length - 1) / 2f - (int)tier) * RowSpacing);
    }

    /// <summary>The area below a grid card that its label takes up.</summary>
    public static Rect2 LabelArea(Vector2 cardCenter)
    {
        var top = cardCenter.Y - CardHeight / 2f - LabelGap;
        return new Rect2(cardCenter.X - CardWidth / 2f, top - LabelHeight, CardWidth, LabelHeight);
    }

    /// <summary>
    ///     The signature of the batch card at <paramref name="index" />: a copy of a grid cell's, cycling through all
    ///     of them so a batch shows every effect equally often.
    /// </summary>
    public static CardSignature BatchSignature(int index)
    {
        var cell = index % (Rarities.Length * Tiers.Length);
        return CardEffectComparisonGrid.SignatureFor(Rarities[cell / Tiers.Length], Tiers[cell % Tiers.Length]);
    }

    public static Vector2 BatchCenter(int index)
    {
        var column = index % BatchColumns;
        var row = index / BatchColumns;
        var firstRow = GridBottom() - BatchGap - CardHeight / 2f;
        return new Vector2((column - (BatchColumns - 1) / 2f) * ColumnSpacing, firstRow - row * BatchRowSpacing);
    }

    /// <summary>The rectangle holding the grid, its labels and <paramref name="batchCount" /> batch cards.</summary>
    public static Rect2 Bounds(int batchCount)
    {
        var top = CellCenter(Rarities[0], Tiers[0]).Y + CardHeight / 2f;
        var right = CellCenter(Rarities[^1], Tiers[0]).X + CardWidth / 2f;
        var bounds = new Rect2(-right, GridBottom(), 2f * right, top - GridBottom());
        if (batchCount <= 0)
            return bounds;

        var first = BatchCenter(0);
        var lastInFirstRow = BatchCenter(Math.Min(batchCount, BatchColumns) - 1);
        var bottom = BatchCenter(batchCount - 1).Y - CardHeight / 2f;
        var left = first.X - CardWidth / 2f;
        return bounds.Merge(new Rect2(left, bottom, lastInFirstRow.X + CardWidth / 2f - left,
            first.Y + CardHeight / 2f - bottom));
    }

    /// <summary>
    ///     How far a camera looking straight at the content must stand to show all of
    ///     <paramref name="contentSize" />, plus <see cref="FrameMargin" /> on every side.
    /// </summary>
    public static float CameraDistance(Vector2 contentSize, float verticalFovDegrees, float aspect)
    {
        var framed = contentSize * (1f + 2f * FrameMargin);
        var tanHalfHeight = Mathf.Tan(Mathf.DegToRad(verticalFovDegrees) / 2f);
        return Mathf.Max(framed.Y / 2f / tanHalfHeight, framed.X / 2f / (tanHalfHeight * aspect));
    }

    private static float GridBottom()
    {
        return LabelArea(CellCenter(Rarities[0], Tiers[^1])).Position.Y;
    }
}
