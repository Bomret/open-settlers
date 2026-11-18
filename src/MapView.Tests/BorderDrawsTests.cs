using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The border posts of a map (docs/rules/roads.md, "How a border is drawn"): a post on each edge east, south-east, and
// south of a node whose owner differs from the neighbor's, where the node holds no road bit of that direction. The posts
// are the ones the road pass of the views (TerrainQuads.Edges) hands its source.
[TestClass]
public sealed class BorderDrawsTests
{
    static Map Blank() => new(MapSize.Offered[0]);

    // The edges that carry a post, as the node each is drawn from and its direction.
    static List<(int Column, int Row, RoadDirection Direction)> Edges(Map map) =>
        [.. EdgeTerms.Posts(map).Select(post => (post.Column, post.Row, post.Direction))];

    [TestMethod]
    public void APlayersNodeBesideANodeNobodyOwnsGetsAPostOnEachEdgeBetweenThem()
    {
        var map = Blank();
        map.At(10, 10).Owner = Owner.Player(0);

        // The node draws its east, south-east, and south edges; its other three neighbors draw theirs toward it.
        CollectionAssert.AreEquivalent(
            new[]
            {
                (10, 10, RoadDirection.East),
                (10, 10, RoadDirection.SouthEast),
                (10, 10, RoadDirection.South),
                (9, 10, RoadDirection.East),
                (9, 9, RoadDirection.SouthEast),
                (10, 9, RoadDirection.South),
            },
            Edges(map)
        );
    }

    [TestMethod]
    public void TwoDifferentPlayersGetAPostAndEqualOwnersNone()
    {
        var map = Blank();
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Owner = Owner.Player(0);
        }

        Assert.IsEmpty(EdgeTerms.Posts(map));

        map.At(10, 10).Owner = Owner.Player(1);

        Assert.HasCount(6, EdgeTerms.Posts(map));
    }

    [TestMethod]
    public void ARoadBetweenTwoOwnersKeepsThePostAwayAlsoWhereItsStripHasNoImage()
    {
        var map = Blank();
        map.At(10, 10).Owner = Owner.Player(0);
        map.At(10, 10).Roads = RoadSet.Of(0);
        // A height difference of 5: the archive holds no mask for the strip.
        map.At(10, 10).Height = 5;

        Assert.DoesNotContain((10, 10, RoadDirection.East), Edges(map));
        Assert.HasCount(5, EdgeTerms.Posts(map));
        Assert.AreEqual(5, EdgeTerms.Strips(map).Single().Difference);
    }

    [TestMethod]
    public void ATerritoryAcrossTheWrappingEdgeGetsItsPostsOnTheSeamOnceEach()
    {
        var map = Blank();
        var last = map.Size.Width - 1;
        map.At(last, 10).Owner = Owner.Player(0);
        map.At(0, 10).Owner = Owner.Player(0);

        var edges = Edges(map);

        // The edge between the two owned nodes crosses the seam and carries none; the edges around them carry one each.
        Assert.DoesNotContain((last, 10, RoadDirection.East), edges);
        Assert.HasCount(edges.Distinct().Count(), edges);
        Assert.HasCount(10, edges);
        Assert.Contains((0, 10, RoadDirection.East), edges);
        Assert.Contains((last - 1, 10, RoadDirection.East), edges);
    }

    [TestMethod]
    public void ATerritoryAcrossTheRowSeamGetsNoPostBetweenItsNodes()
    {
        var map = Blank();
        var last = map.Size.Height - 1;
        map.At(10, last).Owner = Owner.Player(0);
        map.At(10, 0).Owner = Owner.Player(0);

        var edges = Edges(map);

        // The south edge of 10,last reaches 10,0 across the seam and carries none; the edges around them carry one each.
        Assert.DoesNotContain((10, last, RoadDirection.South), edges);
        Assert.HasCount(edges.Distinct().Count(), edges);
        Assert.HasCount(10, edges);
    }

    [TestMethod]
    public void TwoOwnersAcrossTheRowSeamGetTheSeamPostOnce()
    {
        var map = Blank();
        var last = map.Size.Height - 1;
        map.At(10, last).Owner = Owner.Player(0);
        map.At(10, 0).Owner = Owner.Player(1);

        var edges = Edges(map);

        // The south edge of 10,last is the only edge between the two nodes; 10,0 draws none toward the north.
        Assert.HasCount(1, edges.Where(edge => edge == (10, last, RoadDirection.South)));
        Assert.HasCount(11, edges);
    }

    [TestMethod]
    public void APostHasTheSlopeAndGroundOfTheRoadStripOfItsEdge()
    {
        var map = Blank();
        map.At(10, 10).Owner = Owner.Player(0);
        map.At(10, 10).Height = 3;
        map.At(11, 10).Height = 1;
        map.At(10, 9).Height = 2;
        map.At(10, 9).Upper = new Terrain { Code = 12 };
        map.At(10, 10).Lower = new Terrain { Code = 6 };

        var post = EdgeTerms.Posts(map).Single(post => post is { Column: 10, Row: 10, Direction: RoadDirection.East });

        // East: three times the east node's height less the node's, plus the north node's, less the south-east node's.
        Assert.AreEqual(3 * (1 - 3) + 2 - 0, post.Slope);
        Assert.AreEqual(12, post.Ground.Code);
        // The anchor is one pixel left of the middle of the edge and 4 above it, at the edge's mean height.
        var (x, y) = Camera.NodePosition(10, 10);
        Assert.AreEqual(x + Camera.NodeSpacing / 2 - 1, post.X);
        Assert.AreEqual(y - 4 - 2 * (3 + 1), post.Y);
    }
}
