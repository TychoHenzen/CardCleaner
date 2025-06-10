using GdUnit4;
using Godot;
using Godot.Collections;

namespace CardCleaner.Tests.Features;

[TestSuite]
[RequireGodotRuntime]
public class TilePlacementTest
{
    [TestCase]
    public void TestTilePlacementCreation()
    {
        var placement = new TilePlacement
        {
            AnimationFrames = new Array<Vector3I> { new(1, 5, 3) },
            BlocksMovement = true,
            AnimationSpeed = 2.0f
        };

        Assertions.AssertThat(placement.AnimationFrames[0].X).IsEqual(1);
        Assertions.AssertThat(placement.AnimationFrames[0].YZ()).IsEqual(new Vector2I(5, 3));
        Assertions.AssertBool(placement.BlocksMovement).IsTrue();
        Assertions.AssertThat(placement.AnimationSpeed).IsEqual(2.0f);
    }

    [TestCase]
    public void TestAnimationFrames()
    {
        var placement = new TilePlacement
        {
            AnimationFrames = new Array<Vector3I>
            {
                new(0, 0, 0),
                new(1, 0, 0),
                new(2, 0, 0)
            }
        };

        Assertions.AssertThat(placement.AnimationFrames.Count).IsEqual(3);
        Assertions.AssertThat(placement.AnimationFrames[0]).IsEqual(new Vector3I(0, 0, 0));
        Assertions.AssertThat(placement.AnimationFrames[2]).IsEqual(new Vector3I(2, 0, 0));
    }
}