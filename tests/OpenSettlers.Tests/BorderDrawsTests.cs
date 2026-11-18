using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The posts after a castle placement, which is internal to the application library; the rest of this class is in
// src/MapView.Tests.
[TestClass]
public sealed class BorderDrawsTests
{
    static Map Blank() => new(MapSize.Offered[0]);

    // The edges that carry a post, as the node each is drawn from and its direction.
    static List<(int Column, int Row, RoadDirection Direction)> Edges(Map map) =>
        [.. EdgeTerms.Posts(map).Select(post => (post.Column, post.Row, post.Direction))];

    [TestMethod]
    public void AfterTheCastlePlacementThePostsLieOnThePairsBetweenTheTerritoryAndTheRest()
    {
        var map = Blank();
        CastlePlacement.Apply(map, 20, 20, 5, 0);

        var expected = new List<(int, int, RoadDirection)>();
        (int Dx, int Dy)[] steps = [(1, 0), (1, 1), (0, 1)];
        for (var row = 0; row < map.Size.Height; row++)
        {
            for (var column = 0; column < map.Size.Width; column++)
            {
                for (var direction = 0; direction < 3; direction++)
                {
                    var (dx, dy) = steps[direction];
                    if (map.At(column, row).Owner != map.At(column + dx, row + dy).Owner)
                    {
                        expected.Add((column, row, (RoadDirection)direction));
                    }
                }
            }
        }

        var edges = Edges(map);

        Assert.IsNotEmpty(expected);
        CollectionAssert.AreEquivalent(expected, edges);
        // The castle-to-flag road lies inside the territory.
        Assert.DoesNotContain((20, 20, RoadDirection.SouthEast), edges);
    }
}
