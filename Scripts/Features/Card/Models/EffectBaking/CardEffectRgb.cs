namespace CardCleaner.Scripts.Features.Card.Models.EffectBaking;

internal readonly record struct CardEffectRgb(float R, float G, float B)
{
    public float DistanceSquaredTo(CardEffectRgb other)
    {
        var dr = R - other.R;
        var dg = G - other.G;
        var db = B - other.B;
        return dr * dr + dg * dg + db * db;
    }
}
