using System;

namespace CardCleaner.Scripts.Features.Card.Models.EffectBaking;

/// <summary>
///     For each pixel of a mask, the offset to the nearest pixel outside it (8SSEDT: two sweeps of nearest-seed
///     propagation). The length is the inward distance and the direction points at the closest edge. Integer
///     arithmetic only, so the same mask always gives the same field. The mask needs an outside ring around it.
/// </summary>
internal sealed class CardEffectDistanceField
{
    private const int Far = 10000;

    // Neighbours to read, as x, y offset pairs.
    private static readonly int[] DownSweepKernel = { -1, 0, 0, -1, -1, -1, 1, -1 };
    private static readonly int[] UpSweepKernel = { 1, 0, 0, 1, -1, 1, 1, 1 };
    private static readonly int[] LookRight = { 1, 0 };
    private static readonly int[] LookLeft = { -1, 0 };

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _offsetX;
    private readonly int[] _offsetY;

    public CardEffectDistanceField(bool[] inside, int width, int height)
    {
        _width = width;
        _height = height;
        _offsetX = new int[inside.Length];
        _offsetY = new int[inside.Length];
        for (var i = 0; i < inside.Length; i++)
        {
            _offsetX[i] = inside[i] ? Far : 0;
            _offsetY[i] = inside[i] ? Far : 0;
        }

        SweepDown();
        SweepUp();
    }

    public int OffsetXAt(int index)
    {
        return _offsetX[index];
    }

    public int OffsetYAt(int index)
    {
        return _offsetY[index];
    }

    public float DistanceAt(int index)
    {
        return MathF.Sqrt(_offsetX[index] * _offsetX[index] + _offsetY[index] * _offsetY[index]);
    }

    private void SweepDown()
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
                Relax(x, y, DownSweepKernel);

            for (var x = _width - 1; x >= 0; x--)
                Relax(x, y, LookRight);
        }
    }

    private void SweepUp()
    {
        for (var y = _height - 1; y >= 0; y--)
        {
            for (var x = _width - 1; x >= 0; x--)
                Relax(x, y, UpSweepKernel);

            for (var x = 0; x < _width; x++)
                Relax(x, y, LookLeft);
        }
    }

    private void Relax(int x, int y, int[] kernel)
    {
        var here = y * _width + x;
        for (var k = 0; k < kernel.Length; k += 2)
        {
            var nx = x + kernel[k];
            var ny = y + kernel[k + 1];
            if (nx < 0 || nx >= _width || ny < 0 || ny >= _height) continue;

            var candidateX = _offsetX[ny * _width + nx] + kernel[k];
            var candidateY = _offsetY[ny * _width + nx] + kernel[k + 1];
            if (SquaredLength(candidateX, candidateY) >= SquaredLength(_offsetX[here], _offsetY[here])) continue;

            _offsetX[here] = candidateX;
            _offsetY[here] = candidateY;
        }
    }

    private static int SquaredLength(int x, int y)
    {
        return x * x + y * y;
    }
}
