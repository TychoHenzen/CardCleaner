using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.CardStacks.Models;
using Godot;

namespace CardCleaner.Tests.Features.CardStacks.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardClustersTest
{
    private const float Link = 0.5f;

    // A row of points 0.4 apart links end to end, though its ends lie far further apart than the link distance.
    [TestCase]
    [TestCategory("Unit")]
    public static void PointsChainedWithinTheLinkDistanceAreOneGroup()
    {
        var points = Row(Vector3.Zero, 101, 0.4f);

        var groups = CardClusters.LargerThan(points, Link, 100);

        AssertThat(groups.Count).IsEqual(1);
        AssertThat(groups[0]).IsEqual(Enumerable.Range(0, 101).ToList());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OnlyGroupsLargerThanTheLimitAreReturned()
    {
        var points = Row(Vector3.Zero, 100, 0.01f).Concat(Row(new Vector3(10f, 0f, 0f), 101, 0.01f)).ToList();

        var groups = CardClusters.LargerThan(points, Link, 100);

        AssertThat(groups.Count).IsEqual(1);
        AssertThat(groups[0].First()).IsEqual(100);
        AssertThat(groups[0].Count).IsEqual(101);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PointsFurtherApartThanTheLinkStaySeparate()
    {
        var points = Row(Vector3.Zero, 4, 0.6f);

        AssertThat(CardClusters.LargerThan(points, Link, 1).Count).IsEqual(0);
        AssertThat(CardClusters.LargerThan(points, Link, 0).Count).IsEqual(4);
    }

    // The groups come in the order of their first point, whatever order the points were linked in.
    [TestCase]
    [TestCategory("Unit")]
    public static void InterleavedGroupsKeepTheirOrder()
    {
        var points = new List<Vector3>();
        for (var i = 0; i < 6; i++)
            points.Add(new Vector3(i % 2 == 0 ? 0f : 5f, 0f, i * 0.01f));

        var groups = CardClusters.LargerThan(points, Link, 2);

        AssertThat(groups[0]).IsEqual(new List<int> { 0, 2, 4 });
        AssertThat(groups[1]).IsEqual(new List<int> { 1, 3, 5 });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ALinkDistanceMustBePositive()
    {
        AssertThrown(() => CardClusters.LargerThan([Vector3.Zero], 0f, 0))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    private static List<Vector3> Row(Vector3 start, int count, float step) =>
        Enumerable.Range(0, count).Select(i => start + new Vector3(0f, 0f, i * step)).ToList();
}
