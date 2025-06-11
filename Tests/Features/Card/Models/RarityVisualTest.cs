using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Models;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class RarityVisualTest
{
    private RarityVisual _rarityVisual = null!;

    [BeforeTest]
    public void Setup()
    {
        _rarityVisual = new RarityVisual
        {
            Rarity = CardRarity.Epic,
            BaseOptions = new[] { CreateMockTexture(), CreateMockTexture() },
            BorderOptions = new[] { CreateMockTexture() },
            CornerOptions = new[] { CreateMockTexture(), CreateMockTexture(), CreateMockTexture() },
            BannerOptions = new[] { CreateMockTexture() },
            ImageBackgroundOptions = new[] { CreateMockTexture(), CreateMockTexture() },
            DescriptionBoxOptions = new[] { CreateMockTexture() },
            EnergyContainerOptions = new[] { CreateMockTexture() }
        };
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultValues_AreValid()
    {
        // Arrange
        var defaultRarity = new RarityVisual();

        // Assert
        Assertions.AssertThat(defaultRarity.Rarity).IsEqual(CardRarity.Common);
        Assertions.AssertThat(defaultRarity.BaseOptions).IsNotNull();
        Assertions.AssertThat(defaultRarity.BorderOptions).IsNotNull();
        Assertions.AssertThat(defaultRarity.CornerOptions).IsNotNull();
        Assertions.AssertThat(defaultRarity.BannerOptions).IsNotNull();
        Assertions.AssertThat(defaultRarity.ImageBackgroundOptions).IsNotNull();
        Assertions.AssertThat(defaultRarity.DescriptionBoxOptions).IsNotNull();
        Assertions.AssertThat(defaultRarity.EnergyContainerOptions).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Properties_StoreCorrectValues()
    {
        // Assert
        Assertions.AssertThat(_rarityVisual.Rarity).IsEqual(CardRarity.Epic);
        Assertions.AssertThat(_rarityVisual.BaseOptions.Length).IsEqual(2);
        Assertions.AssertThat(_rarityVisual.BorderOptions.Length).IsEqual(1);
        Assertions.AssertThat(_rarityVisual.CornerOptions.Length).IsEqual(3);
        Assertions.AssertThat(_rarityVisual.BannerOptions.Length).IsEqual(1);
        Assertions.AssertThat(_rarityVisual.ImageBackgroundOptions.Length).IsEqual(2);
        Assertions.AssertThat(_rarityVisual.DescriptionBoxOptions.Length).IsEqual(1);
        Assertions.AssertThat(_rarityVisual.EnergyContainerOptions.Length).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TextureArrays_CanBeEmpty()
    {
        // Act
        _rarityVisual.BaseOptions = new Texture2D[0];
        _rarityVisual.BorderOptions = new Texture2D[0];

        // Assert - Should handle empty arrays gracefully
        Assertions.AssertThat(_rarityVisual.BaseOptions.Length).IsEqual(0);
        Assertions.AssertThat(_rarityVisual.BorderOptions.Length).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AllRarityTypes_CanBeAssigned()
    {
        // Act & Assert - Test each rarity type can be assigned
        foreach (var rarity in System.Enum.GetValues<CardRarity>())
        {
            _rarityVisual.Rarity = rarity;
            Assertions.AssertThat(_rarityVisual.Rarity).IsEqual(rarity);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TextureOptions_AcceptNullTextures()
    {
        // Act - Arrays can contain null textures for missing variants
        _rarityVisual.BaseOptions = new Texture2D[] { CreateMockTexture(), null!, CreateMockTexture() };

        // Assert
        Assertions.AssertThat(_rarityVisual.BaseOptions.Length).IsEqual(3);
        Assertions.AssertThat(_rarityVisual.BaseOptions[0]).IsNotNull();
        Assertions.AssertThat(_rarityVisual.BaseOptions[1]).IsNull();
        Assertions.AssertThat(_rarityVisual.BaseOptions[2]).IsNotNull();
    }

    private static Texture2D CreateMockTexture()
    {
        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgb8);
        image.Fill(new Color(System.Random.Shared.NextSingle(), System.Random.Shared.NextSingle(), System.Random.Shared.NextSingle()));
        return ImageTexture.CreateFromImage(image);
    }
}
