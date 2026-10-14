using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services;

[TestSuite]
[RequireGodotRuntime]
public class CardSpawnerTest
{
    private CardSpawner _spawner = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _spawner = new CardSpawner
        {
            OffsetRange = new Vector3(1.0f, 0.5f, 1.0f)
        };
        Assertions.AddNode(_spawner);
    }

    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardSpawner_CanBeInstantiated()
    {
        // Assert
        Assertions.AssertThat(_spawner).IsNotNull();
        Assertions.AssertThat(_spawner).IsInstanceOf<CardSpawner>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OffsetRange_CanBeModified()
    {
        // Act
        var newOffset = new Vector3(2.0f, 1.0f, 2.0f);
        _spawner.OffsetRange = newOffset;

        // Assert
        Assertions.AssertThat(_spawner.OffsetRange).IsEqual(newOffset);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GetNode_ReturnsSpawnerInstance()
    {
        // Act
        var node = _spawner.GetNode();

        // Assert
        Assertions.AssertThat(node).IsEqual(_spawner);
        Assertions.AssertThat(node).IsInstanceOf<Node3D>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SpawnSpecificCard_ValidSignature_WithoutServices_ReturnsNull()
    {
        // Arrange
        var signature = new Scripts.Features.Card.Models.CardSignature();

        // Act - Without registered spawning service, should return null
        var result = _spawner.SpawnSpecificCard(signature);

        // Assert
        Assertions.AssertThat(result).IsNull();
    }
}
