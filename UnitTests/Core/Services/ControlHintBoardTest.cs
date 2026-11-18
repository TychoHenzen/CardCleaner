using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
public class ControlHintBoardTest
{
    private static readonly ControlHint Open = new("Q", "Open the pack");
    private static readonly ControlHint Drop = new("RMB", "Put it down");
    private static readonly ControlHint Turn = new("T", "Turn");

    [TestCase]
    public static void HintsKeepTheOrderSourcesFirstPublishedIn()
    {
        var board = new ControlHintBoard();
        var hand = new object();
        var placement = new object();

        board.Publish(hand, [Open]);
        board.Publish(placement, [Turn]);
        board.Publish(hand, [Open, Drop]);

        AssertThat(board.Hints.ToArray()).IsEqual(new[] { Open, Drop, Turn });
    }

    [TestCase]
    public static void RepublishingTheSameHintsRaisesNoChange()
    {
        var board = new ControlHintBoard();
        var source = new object();
        var changes = 0;
        board.Changed += () => changes++;

        board.Publish(source, [Open, Drop]);
        board.Publish(source, [Open, Drop]);

        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    public static void AnEmptyListAndClearBothRemoveTheSourcesHints()
    {
        var board = new ControlHintBoard();
        var first = new object();
        var second = new object();
        board.Publish(first, [Open]);
        board.Publish(second, [Turn]);

        board.Publish(first, []);
        AssertThat(board.Hints.ToArray()).IsEqual(new[] { Turn });

        board.Clear(second);
        AssertThat(board.Hints.Count).IsEqual(0);
    }

    [TestCase]
    public static void ClearingASourceWithNoHintsRaisesNoChange()
    {
        var board = new ControlHintBoard();
        var changes = 0;
        board.Changed += () => changes++;

        board.Clear(new object());

        AssertThat(changes).IsEqual(0);
    }
}
