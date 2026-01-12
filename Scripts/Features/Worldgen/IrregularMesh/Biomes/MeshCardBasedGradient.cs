using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Biomes;

/// <summary>
/// Card-based signature gradient for mesh vertex positions.
/// Creates spatial variation in biome influence based on input cards.
/// </summary>
public class MeshCardBasedGradient
{
    private readonly CardSignature[] _inputCards;
    private readonly RandomNumberGenerator _rng;
    private readonly Vector2 _mapBounds;

    private const int GridResolution = 16;
    private const float MultiCardIntensityBonus = 0.3f;
    private const float MultiCardVariationBonus = 0.2f;

    private CardSignature[,]? _sampleGrid;
    private bool _needsRegeneration = true;

    /// <summary>
    /// Sampling radius scales with number of cards.
    /// More cards = more variation from the base curve.
    /// </summary>
    private float SamplingRadius => 0.1f * _inputCards.Length;

    /// <summary>
    /// Creates a gradient from input cards.
    /// </summary>
    /// <param name="inputCards">The cards to use for gradient generation.</param>
    /// <param name="mapBounds">The size of the map (for normalizing positions).</param>
    /// <param name="rng">Random number generator for sampling.</param>
    public MeshCardBasedGradient(CardSignature[] inputCards, Vector2 mapBounds, RandomNumberGenerator rng)
    {
        _inputCards = inputCards;
        _mapBounds = mapBounds;
        _rng = rng;
    }

    /// <summary>
    /// Gets the signature at a world position.
    /// </summary>
    /// <param name="position">World position (x, y).</param>
    /// <returns>Interpolated signature at the position.</returns>
    public CardSignature GetSignatureAt(Vector2 position)
    {
        if (_needsRegeneration || _sampleGrid == null)
        {
            GenerateSampleGrid();
            _needsRegeneration = false;
        }

        if (_sampleGrid == null || _inputCards.Length == 0)
            return new CardSignature();

        // Convert world position to normalized [0, 1] coordinates
        var normalizedX = Mathf.Clamp(position.X / _mapBounds.X, 0, 1);
        var normalizedY = Mathf.Clamp(position.Y / _mapBounds.Y, 0, 1);

        // Convert to grid coordinates
        var gridX = normalizedX * (GridResolution - 1);
        var gridY = normalizedY * (GridResolution - 1);

        return BilinearInterpolate(gridX, gridY);
    }

    /// <summary>
    /// Regenerates the sample grid (call after changing cards).
    /// </summary>
    public void Regenerate()
    {
        _needsRegeneration = true;
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

    /// <summary>
    /// Single card: sphere gradient with random samples around the center.
    /// </summary>
    private void GenerateSphereGradient()
    {
        var center = _inputCards[0];

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
            _sampleGrid![y, x] = SampleFromHypersphere(center, SamplingRadius);
    }

    /// <summary>
    /// Two cards: capsule gradient (linear blend with cylindrical distribution).
    /// </summary>
    private void GenerateCapsuleGradient()
    {
        var start = _inputCards[0];
        var end = _inputCards[1];

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
        {
            // Sample along line segment with cylindrical distribution
            var t = _rng.Randf();
            var linePoint = LerpSignatures(start, end, t);
            _sampleGrid![y, x] = SampleFromHypersphere(linePoint, SamplingRadius * 0.8f);
        }
    }

    /// <summary>
    /// Three+ cards: Bezier curve gradient through all cards.
    /// </summary>
    private void GenerateBezierGradient()
    {
        var controlPoints = _inputCards.ToArray();

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
        {
            // Sample along Bezier curve
            var t = _rng.Randf();
            var curvePoint = SampleBezierCurve(controlPoints, t);
            _sampleGrid![y, x] = SampleFromHypersphere(curvePoint, SamplingRadius * 0.6f);
        }
    }

    /// <summary>
    /// Samples a point uniformly from within a hypersphere in 8D space.
    /// </summary>
    private CardSignature SampleFromHypersphere(CardSignature center, float radius)
    {
        var result = new CardSignature();
        var attempts = 0;

        // Generate random point in 8D hypersphere using rejection sampling
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
                // Inside unit sphere, scale to desired radius
                // Using 1/8 power for uniform distribution in 8D
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

    /// <summary>
    /// Evaluates a closed Bezier curve using De Casteljau's algorithm.
    /// </summary>
    private static CardSignature SampleBezierCurve(CardSignature[] controlPoints, float t)
    {
        if (controlPoints.Length == 0) return new CardSignature();
        if (controlPoints.Length == 1) return controlPoints[0];

        // Closed curve: add first point at end for continuity
        var points = controlPoints.ToList();
        points.Add(controlPoints[0]);

        // De Casteljau's algorithm
        var tempPoints = points.ToArray();

        for (var level = tempPoints.Length - 1; level > 0; level--)
        for (var i = 0; i < level; i++)
            tempPoints[i] = LerpSignatures(tempPoints[i], tempPoints[i + 1], t);

        return tempPoints[0];
    }

    /// <summary>
    /// Applies bonuses for multiple cards (intensity and variation).
    /// </summary>
    private void ApplyMultiCardBonuses()
    {
        if (_inputCards.Length <= 1) return;

        var bonusMultiplier = 1f + (_inputCards.Length - 1) * MultiCardIntensityBonus;
        var variationFactor = 1f + (_inputCards.Length - 1) * MultiCardVariationBonus;

        for (var y = 0; y < GridResolution; y++)
        for (var x = 0; x < GridResolution; x++)
        {
            var signature = _sampleGrid![y, x];

            // Apply intensity bonus
            for (var i = 0; i < 8; i++)
                signature[i] = Mathf.Clamp(signature[i] * bonusMultiplier, -1f, 1f);

            // Add variation bonus (increases local differences)
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

    /// <summary>
    /// Bilinear interpolation in the sample grid.
    /// </summary>
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
