using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>The rarity, base type and gem data the generator suites build cards from; not a suite.</summary>
public static class SignatureCardTestData
{
    /// <summary>A generator built from the test data, as the container builds it from the provider's arrays.</summary>
    internal static SignatureCardGenerator CreateGenerator()
    {
        return new SignatureCardGenerator(CreateRarityVisuals(), CreateBaseTypes(), CreateGemVisuals());
    }

    /// <summary>
    ///     A generator with <paramref name="baseTypeCount" /> base types that no all-zero signature matches, so a card
    ///     without magical potential picks its art from them by its look seed alone. <paramref name="arts" /> holds
    ///     each base type's art texture, in base type order.
    /// </summary>
    internal static SignatureCardGenerator CreateGeneratorWithBaseTypes(int baseTypeCount, out Texture2D[] arts)
    {
        var baseTypes = new BaseCardType[baseTypeCount];
        arts = new Texture2D[baseTypeCount];
        for (var i = 0; i < baseTypeCount; i++)
        {
            var art = CreateMockTexture();
            arts[i] = art;

            var baseType = new BaseCardType();
            baseType.TypeName = $"Test Card {i}";
            baseType.BaseSignature = new CardSignatureResource { Solidum = 0.9f };
            baseType.MatchRadius = 0.01f;
            baseType.ArtOptions = new[] { art };
            baseType.SymbolOptions = new[] { CreateMockTexture() };
            baseType.EnergyFill1Options = new[] { CreateMockTexture() };
            baseType.EnergyFill2Options = new[] { CreateMockTexture() };
            baseTypes[i] = baseType;
        }

        var commonVisual = new RarityVisual();
        commonVisual.Rarity = CardRarity.Common;
        commonVisual.BaseOptions = new[] { CreateMockTexture(), CreateMockTexture(), CreateMockTexture() };
        commonVisual.BorderOptions = new[] { CreateMockTexture() };
        commonVisual.EnergyContainerOptions = new[] { CreateMockTexture(), CreateMockTexture() };

        return new SignatureCardGenerator(new[] { commonVisual }, baseTypes, CreateGemVisuals());
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
        baseType.BaseSignature = new CardSignatureResource { Solidum = 0.5f };
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
