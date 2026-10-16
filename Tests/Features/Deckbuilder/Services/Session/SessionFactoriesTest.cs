using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.Session;

[TestSuite]
[RequireGodotRuntime]
public class SessionFactoriesTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void SessionWorldsCreateReturnsAnEmptyWorld()
    {
        ISessionWorld world = SessionWorlds.Create();

        AssertBool(world is SessionWorld).IsTrue();
        AssertThat(world.MapData).IsNull();
        AssertThat(world.GeneratedMap).IsNull();
        AssertInt(world.EnemyCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExplorationSessionsCreateStartsOnlyWhenTheWorldHasAMap()
    {
        var world = SessionWorlds.Create();
        var session = ExplorationSessions.Create(world);

        AssertBool(session is ExplorationSession).IsTrue();
        AssertBool(session.Start()).IsFalse();

        var (_, gridData) = CardCleaner.Tests.TestUtilities.Fixtures.OpenFloorMap.Create(5, 5);
        world.MapData = gridData;
        AssertBool(session.Start()).IsTrue();
        AssertInt(session.MoveCount).IsEqual(0);
    }
}
