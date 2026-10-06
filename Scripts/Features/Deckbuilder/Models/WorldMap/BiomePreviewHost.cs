using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Creates the biome distribution preview inside the world map viewport and keeps it in sync with the map seed cards.
/// </summary>
internal sealed class BiomePreviewHost
{
    private BiomeRegistry? _biomeRegistry;
    private BiomeDistributionPreview? _biomePreview;

    internal void Setup(SubViewport? viewport)
    {
        // A preview without a viewport would have no parent, so nothing would ever free it.
        if (viewport == null)
            return;

        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes();

        _biomePreview = CreateFullRectPreview();
        viewport.AddChild(_biomePreview);
    }

    private static BiomeDistributionPreview CreateFullRectPreview()
    {
        var preview = new BiomeDistributionPreview();
        preview.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        preview.OffsetLeft = 0;
        preview.OffsetTop = 0;
        preview.OffsetRight = 0;
        preview.OffsetBottom = 0;
        return preview;
    }

    internal void Update(CardSignature[] cards)
    {
        if (_biomePreview == null || _biomeRegistry == null) return;

        if (cards.Length == 0)
        {
            _biomePreview.Clear();
            return;
        }

        var distribution = BiomeDistributionCalculator.Calculate(cards, _biomeRegistry);
        _biomePreview.UpdateDistribution(distribution);
    }

    internal void Hide()
    {
        if (_biomePreview != null) _biomePreview.Visible = false;
    }

    internal void ShowCleared()
    {
        if (_biomePreview == null) return;

        _biomePreview.Visible = true;
        _biomePreview.Clear();
    }
}
