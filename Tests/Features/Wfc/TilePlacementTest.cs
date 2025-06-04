using GdUnit4;
using Godot;
using Godot.Collections;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class TilePlacementTest
{
    [TestCase]
    public void TestTilePlacementCreation()
    {
        var placement = new TilePlacement
        {
            SourceId = 1,
            AtlasCoords = new Vector2I(5, 3),
            BlocksMovement = true,
            AnimationSpeed = 2.0f
        };

        Assertions.AssertThat(placement.SourceId).IsEqual(1);
        Assertions.AssertThat(placement.AtlasCoords).IsEqual(new Vector2I(5, 3));
        Assertions.AssertBool(placement.BlocksMovement).IsTrue();
        Assertions.AssertThat(placement.AnimationSpeed).IsEqual(2.0f);
    }

    [TestCase]
    public void TestAnimationFrames()
    {
        var placement = new TilePlacement
        {
            AnimationFrames = new Array<Vector2I>
            {
                new Vector2I(0, 0),
                new Vector2I(1, 0),
                new Vector2I(2, 0)
            }
        };

        Assertions.AssertThat(placement.AnimationFrames.Count).IsEqual(3);
        Assertions.AssertThat(placement.AnimationFrames[0]).IsEqual(new Vector2I(0, 0));
        Assertions.AssertThat(placement.AnimationFrames[2]).IsEqual(new Vector2I(2, 0));
    }
}