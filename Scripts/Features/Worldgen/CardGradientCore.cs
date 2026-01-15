using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Core gradient generation logic shared between grid-based and mesh-based systems.
/// Generates a sample grid of CardSignatures based on input cards using
/// sphere (1 card), capsule (2 cards), or Bezier curve (3+ cards) distributions.
/// </summary>
public class CardGradientCore
{
    public const int GridResolution = 16;
    public const float MultiCardIntensityBonus = 0.3f;
    public const float MultiCardVariationBonus = 0.2f;

    private readonly CardSignature[] _inputCards;
    private readonly RandomNumberGenerator _rng;

    private CardSignature[,]? _sampleGrid;
    private bool _needsRegeneration = true;

    private float SamplingRadius => 0.1f * _inputCards.Length;

    public CardGradientCore(CardSignature[] inputCards, RandomNumberGenerator rng)
    {
        _inputCards = inputCards;
        _rng = rng;
    }

    /// <summary>
    /// Gets a signature at normalized coordinates [0, 1].
    /// </summary>
    /// <param name="normalizedX">X position normalized to [0, 1].</param>
    /// <param name="normalizedY">Y position normalized to [0, 1].</param>
    public CardSignature GetSignatureAtNormalized(float normalizedX, float normalizedY)
    {
        EnsureGridGenerated();

        if (_sampleGrid == null || _inputCards.Length == 0)
            return new CardSignature();

        var gridX = Mathf.Clamp(normalizedX, 0, 1) * (GridResolution - 1);
        var gridY = Mathf.Clamp(normalizedY, 0, 1) * (GridResolution - 1);

        return BilinearInterpolate(gridX, gridY);
    }

    /// <summary>
    /// Marks the grid for regeneration (call when input cards change).
    /// </summary>
    public void MarkForRegeneration()
    {
        _needsRegeneration = true;
    }

    private void EnsureGridGenerated()
    {
        if (!_needsRegeneration && _sampleGrid != null) return;

        GenerateSampleGrid();
        _needsRegeneration = false;
    }

    private void GenerateSampleGrid()
    {
        if (_inputCards.Length == 0)
        {
            _sampleGrid = null;
            return;
        }

        _sampleGrid = new CardSignature[GridResolution, GridResolution];

        switch (_inputCards.Length)
        {
            case 1:
                GenerateSphereGradient();
                break;
            case 2:
                GenerateCapsuleGradient();
                break;
            default:
                GenerateBezierGradient();
                break;
        }

        ApplyMultiCardBonuses();
    }

    private void GenerateSphereGradient()
    {
        var center = _inputCards[0];

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
            _sampleGrid![y, x] = SampleFromHypersphere(center, SamplingRadius);
    }

    private void GenerateCapsuleGradient()
    {
        var start = _inputCards[0];
        var end = _inputCards[1];

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
        {
            var t = _rng.Randf();
            var linePoint = LerpSignatures(start, end, t);
            _sampleGrid![y, x] = SampleFromHypersphere(linePoint, SamplingRadius * 0.8f);
        }
    }

    private void GenerateBezierGradient()
    {
        var controlPoints = _inputCards.ToArray();

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
        {
            var t = _rng.Randf();
            var curvePoint = SampleBezierCurve(controlPoints, t);
            _sampleGrid![y, x] = SampleFromHypersphere(curvePoint, SamplingRadius * 0.6f);
        }
    }

    private CardSignature SampleFromHypersphere(CardSignature center, float radius)
    {
        var result = new CardSignature();
        var attempts = 0;

        while (attempts < 20)
        {
            var components = new float[8];
            var lengthSquared = 0f;

            for (var i = 0; i < 8; i++)
            {
                components[i] = _rng.RandfRange(-1f, 1f);
                lengthSquared += components[i] * components[i];
            }

            if (lengthSquared <= 1f && lengthSquared > 0f)
            {
                var actualRadius = Mathf.Pow(_rng.Randf(), 1f / 8f) * radius;
                var scale = actualRadius / Mathf.Sqrt(lengthSquared);

                for (var i = 0; i < 8; i++)
                    result[i] = Mathf.Clamp(center[i] + components[i] * scale, -1f, 1f);

                break;
            }

            attempts++;
        }

        return result;
    }

    private static CardSignature SampleBezierCurve(CardSignature[] controlPoints, float t)
    {
        if (controlPoints.Length == 0) return new CardSignature();
        if (controlPoints.Length == 1) return controlPoints[0];

        var points = controlPoints.ToList();
        points.Add(controlPoints[0]);

        var tempPoints = points.ToArray();

        for (var level = tempPoints.Length - 1; level > 0; level--)
        for (var i = 0; i < level; i++)
            tempPoints[i] = LerpSignatures(tempPoints[i], tempPoints[i + 1], t);

        return tempPoints[0];
    }

    private void ApplyMultiCardBonuses()
    {
        if (_inputCards.Length <= 1) return;

        var bonusMultiplier = 1f + (_inputCards.Length - 1) * MultiCardIntensityBonus;
        var variationFactor = 1f + (_inputCards.Length - 1) * MultiCardVariationBonus;

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
        {
            var signature = _sampleGrid![y, x];

            for (var i = 0; i < 8; i++)
                signature[i] = Mathf.Clamp(signature[i] * bonusMultiplier, -1f, 1f);

            if (x > 0 && y > 0)
            {
                var neighbor = _sampleGrid[y - 1, x - 1];
                for (var i = 0; i < 8; i++)
                {
                    var diff = signature[i] - neighbor[i];
                    signature[i] = Mathf.Clamp(signature[i] + diff * variationFactor * 0.1f, -1f, 1f);
                }
            }

            _sampleGrid[y, x] = signature;
        }
    }

    private CardSignature BilinearInterpolate(float x, float y)
    {
        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var x1 = Mathf.Min(x0 + 1, GridResolution - 1);
        var y1 = Mathf.Min(y0 + 1, GridResolution - 1);

        var fx = x - x0;
        var fy = y - y0;

        var sample00 = _sampleGrid![y0, x0];
        var sample10 = _sampleGrid[y0, x1];
        var sample01 = _sampleGrid[y1, x0];
        var sample11 = _sampleGrid[y1, x1];

        var top = LerpSignatures(sample00, sample10, fx);
        var bottom = LerpSignatures(sample01, sample11, fx);
        return LerpSignatures(top, bottom, fy);
    }

    private static CardSignature LerpSignatures(CardSignature a, CardSignature b, float t)
    {
        var result = new CardSignature();
        for (var i = 0; i < 8; i++)
            result[i] = Mathf.Lerp(a[i], b[i], t);
        return result;
    }
}
