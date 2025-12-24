using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class CardBasedGradient : BaselineGradient
{
    private CardSignature[] _inputCards = [];
    private float SamplingRadius => 0.1f * _inputCards.Length;
    private const int GridResolution = 16;
    private const float MultiCardIntensityBonus = 0.3f;
    private const float MultiCardVariationBonus = 0.2f;

    private CardSignature[,]? _sampleGrid;
    private Vector2I _gridSize;
    private bool _needsRegeneration = true;
    private RandomNumberGenerator _rng = new();

    public CardBasedGradient()
    {
    }

    public CardBasedGradient(CardSignature[] inputCards, RandomNumberGenerator rng)
    {
        _inputCards = inputCards;
        _rng = rng;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        if (_needsRegeneration || _sampleGrid == null)
        {
            GenerateSampleGrid();
            _needsRegeneration = false;
        }

        if (_sampleGrid == null) return new CardSignature();

        // Convert world position to grid coordinates
        var gridX = (float)position.X / mapSize.X * (_gridSize.X - 1);
        var gridY = (float)position.Y / mapSize.Y * (_gridSize.Y - 1);

        return BilinearInterpolate(gridX, gridY);
    }

    private void GenerateSampleGrid()
    {
        if (_inputCards.Length == 0)
        {
            _sampleGrid = null;
            return;
        }

        _gridSize = new Vector2I(GridResolution, GridResolution);
        _sampleGrid = new CardSignature[_gridSize.Y, _gridSize.X];

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

        for (var y = 0; y < _gridSize.Y; y++)
        for (var x = 0; x < _gridSize.X; x++)
            _sampleGrid![y, x] = SampleFromHypersphere(center, SamplingRadius);
    }

    private void GenerateCapsuleGradient()
    {
        var start = _inputCards[0];
        var end = _inputCards[1];

        for (var y = 0; y < _gridSize.Y; y++)
        for (var x = 0; x < _gridSize.X; x++)
        {
            // Sample along line segment with cylindrical distribution
            var t = _rng.Randf();
            var linePoint = LerpSignatures(start, end, t);
            _sampleGrid![y, x] = SampleFromHypersphere(linePoint, SamplingRadius * 0.8f);
        }
    }

    private void GenerateBezierGradient()
    {
        // Create closed Bezier curve through all input cards
        var controlPoints = _inputCards.ToArray();

        for (var y = 0; y < _gridSize.Y; y++)
        for (var x = 0; x < _gridSize.X; x++)
        {
            // Sample along Bezier curve
            var t = _rng.Randf();
            var curvePoint = SampleBezierCurve(controlPoints, t);
            _sampleGrid![y, x] = SampleFromHypersphere(curvePoint, SamplingRadius * 0.6f);
        }
    }

    private CardSignature SampleFromHypersphere(CardSignature center, float radius)
    {
        var result = new CardSignature();

        // Generate random point in 8D hypersphere using rejection sampling
        var attempts = 0;
        while (attempts < 20) // Prevent infinite loops
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
                var actualRadius = Mathf.Pow(_rng.Randf(), 1f / 8f) * radius; // Uniform distribution in 8D
                var scale = actualRadius / Mathf.Sqrt(lengthSquared);

                for (var i = 0; i < 8; i++) result[i] = Mathf.Clamp(center[i] + components[i] * scale, -1f, 1f);

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

        // Closed curve: add first point at end for continuity
        var points = controlPoints.ToList();
        points.Add(controlPoints[0]);

        // De Casteljau's algorithm for Bezier curve evaluation
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

        for (var y = 0; y < _gridSize.Y; y++)
        for (var x = 0; x < _gridSize.X; x++)
        {
            var signature = _sampleGrid![y, x];

            // Apply intensity bonus
            for (var i = 0; i < 8; i++) signature[i] = Mathf.Clamp(signature[i] * bonusMultiplier, -1f, 1f);

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

    private CardSignature BilinearInterpolate(float x, float y)
    {
        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var x1 = Mathf.Min(x0 + 1, _gridSize.X - 1);
        var y1 = Mathf.Min(y0 + 1, _gridSize.Y - 1);

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

    // Helper method to trigger regeneration when cards change
    public void SetInputCards(CardSignature[] cards)
    {
        _inputCards = cards;
        _needsRegeneration = true;
    }
}