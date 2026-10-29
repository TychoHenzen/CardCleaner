using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Composition;

/// <summary>
/// The map generators are [Service] classes the composition root's scan builds; the root no longer news them up.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MapGeneratorsScannedTest
{
    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ComposedRootResolvesBothMapGenerators()
    {
        // Arrange and act - the autoload composes; the scan builds each generator from the registered data
        TestServices.Reinitialize();
        var created = ReinitializedNodes();
        try
        {
            // Assert - each generator resolves through its contract, and the irregular one is the scanned class
            Assertions.AssertBool(TestServices.Resolver.Get<IGridMapGenerator>() is GridMapGenerator).IsTrue();
            Assertions.AssertThat(TestServices.Resolver.Get<IMeshMapGenerator>()).IsInstanceOf<MeshMapGenerator>();
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ComposedRootSharesOneInstancePerGenerator()
    {
        // Arrange and act
        TestServices.Reinitialize();
        var created = ReinitializedNodes();
        try
        {
            // Assert - the scan registers each generator as a singleton, so repeated resolves return one instance
            var grid = TestServices.Resolver.Get<IGridMapGenerator>();
            Assertions.AssertThat(grid).IsSame(TestServices.Resolver.Get<IGridMapGenerator>());
            var mesh = TestServices.Resolver.Get<IMeshMapGenerator>();
            Assertions.AssertThat(mesh).IsSame(TestServices.Resolver.Get<IMeshMapGenerator>());
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    // Reinitialize adds an input service and a player reset service to the autoload; the tests free them.
    private static Node[] ReinitializedNodes() =>
        new[]
        {
            (Node)TestServices.Resolver.Get<IInputService>(), (Node)TestServices.Resolver.Get<IPlayerResetService>()
        };

    private static void QueueFreeAll(Node[] nodes)
    {
        foreach (var node in nodes)
            node.QueueFree();
    }
}
