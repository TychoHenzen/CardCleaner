using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>The rarity, base type and gem data the generator suites build cards from; not a suite.</summary>
public static class SignatureCardTestData
{
    /// <summary>Registers the test data where the generator looks for it.</summary>
    public static void Register()
    {
        ServiceLocator.Container.RegisterSingleton(CreateRarityVisuals());
        ServiceLocator.Container.RegisterSingleton(CreateBaseTypes());
        ServiceLocator.Container.RegisterSingleton(CreateGemVisuals());
    }

    private static RarityVisual[] CreateRarityVisuals()
    {
        var commonVisual = new RarityVisual();
        commonVisual.Rarity = CardRarity.Common;
        commonVisual.BaseOptions = new[] { CreateMockTexture() };
        commonVisual.BorderOptions = new[] { CreateMockTexture() };

        // A card with the test base type's signature scores Epic in SignatureCardHelper.DetermineRarity.
        var epicVisual = new RarityVisual();
        epicVisual.Rarity = CardRarity.Epic;
        epicVisual.BaseOptions = new[] { CreateMockTexture() };
        epicVisual.BorderOptions = new[] { CreateMockTexture() };

        return new[] { commonVisual, epicVisual };
    }

    private static BaseCardType[] CreateBaseTypes()
    {
        var baseType = new BaseCardType();
        baseType.TypeName = "Test Card";
        baseType.BaseSignature = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        baseType.MatchRadius = 1.0f; // Match anything for testing
        baseType.ArtOptions = new[] { CreateMockTexture() };
        baseType.SymbolOptions = new[] { CreateMockTexture() };

        return new[] { baseType };
    }

    private static GemVisual[] CreateGemVisuals()
    {
        var gemVisuals = new GemVisual[8];

        for (var i = 0; i < 8; i++)
        {
            var gem = new GemVisual();
            gem.Element = (Element)i;
            gem.SocketTexture = CreateMockTexture();
            gem.PositiveGemTexture = CreateMockTexture();
            gem.NegativeGemTexture = CreateMockTexture();
            gem.PositiveEmissionColor = new Color(1.0f, 0.0f, 0.0f); // Red
            gem.NegativeEmissionColor = new Color(0.0f, 0.0f, 1.0f); // Blue
            gem.PositiveEmissionStrength = 1.0f;
            gem.NegativeEmissionStrength = 0.8f;

            gemVisuals[i] = gem;
        }

        return gemVisuals;
    }

    private static Texture2D CreateMockTexture()
    {
        // Create a minimal 1x1 texture for testing
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
        image.Fill(Color.Color8(255, 255, 255));
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }
}
