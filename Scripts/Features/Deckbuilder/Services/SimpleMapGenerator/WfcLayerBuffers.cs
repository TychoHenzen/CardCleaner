using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;

internal sealed class WfcLayerBuffers
{
    public WfcLayerBuffers(Vector2I size)
    {
        BackgroundLayer = new string[size.Y, size.X];
        ForegroundLayer = new string[size.Y, size.X];
        MergedGrid = new string[size.Y, size.X];
    }

    public string[,] BackgroundLayer { get; }
    public string[,] ForegroundLayer { get; }
    public string[,] MergedGrid { get; }
}
