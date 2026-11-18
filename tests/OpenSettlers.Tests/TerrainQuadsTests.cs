using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TerrainQuadsTests
{
    // A source that draws a 30 x 10 image hung 13 left of and 2 above its anchor for each terrain code except 99, draws a
    // road strip of difference 0 only, and draws every border post.
    sealed class FakeSource : IGraphicsSource
    {
        readonly Dictionary<int, ObjectImage> _images = [];

        public ObjectImage? Draw(TerrainTriangle triangle) =>
            triangle.Terrain.Code == 99 ? null
            : _images.TryGetValue(triangle.Terrain.Code, out var image) ? image
            : _images[triangle.Terrain.Code] = new ObjectImage
            {
                Width = 30,
                Height = 10,
                OffsetX = -13,
                OffsetY = -2,
                Rgba = new byte[30 * 10 * 4],
            };

        public ObjectImage? Draw(BuildMarker marker) => null;

        public IReadOnlyList<Flame> Flames(BuildingType type, bool earlySite) => [];

        public ObjectImage? Draw(FlameDraw flame) => null;
        public ObjectImage? Draw(RoadMarker marker) => null;

        public ObjectImage? Draw(RoadStrip strip) =>
            strip.Difference == 0
                ? new ObjectImage
                {
                    Width = 8,
                    Height = 5,
                    OffsetX = 1,
                    OffsetY = -3,
                    Rgba = new byte[8 * 5 * 4],
                }
                : null;

        // Every border post draws one 6 x 7 image hung 2 left of and 7 above its anchor.
        readonly ObjectImage _post = new()
        {
            Width = 6,
            Height = 7,
            OffsetX = -2,
            OffsetY = -7,
            Rgba = new byte[6 * 7 * 4],
        };

        public ObjectImage? Draw(BorderPost post) => _post;

        public ObjectDrawing? Draw(MapObjectDraw draw) => null;

        public ObjectDrawing? Draw(BuildingDraw draw) => null;

        public ObjectDrawing? Draw(GoodAtFlag draw) => null;

        public ObjectDrawing? Draw(SettlerDraw draw) => null;

        public RippleImage? Draw(WaterRipple ripple) => null;

        public int ObjectHeadroom => 0;
    }

    static TerrainTriangle Triangle(int row, int code = 5, int x = 100, int y = 50) =>
        new()
        {
            Column = 0,
            Row = row,
            Upper = true,
            Terrain = new Domain.Terrain { Code = code },
            Apex = 5,
            Left = 5,
            Right = 5,
            X = x,
            Y = y,
        };

    static List<List<ObjectQuad>> Bands(
        IReadOnlyList<TerrainTriangle> triangles,
        int rows,
        ImageLayers? layers = null
    ) => TerrainQuads.Bands(triangles, new FakeSource(), layers ?? new ImageLayers(), rows, 16);

    [TestMethod]
    public void TheQuadHangsFromItsAnchorByTheImagesOffset()
    {
        var quad = Bands([Triangle(0)], 4)[0][0];

        Assert.AreEqual(100 - 13, quad.X);
        Assert.AreEqual(50 - 2, quad.Y);
        Assert.AreEqual(30, quad.Width);
        Assert.AreEqual(10, quad.Height);
    }

    [TestMethod]
    public void TrianglesWithTheSameImageShareALayerInTheOrderTheImagesAreFirstSeen()
    {
        var layers = new ImageLayers();

        var bands = Bands([Triangle(0, 6), Triangle(0, 7), Triangle(1, 6)], 4, layers);

        CollectionAssert.AreEqual(new[] { 0, 1, 0 }, bands[0].Select(q => q.Layer).ToArray());
        Assert.HasCount(2, layers.Images);
    }

    [TestMethod]
    public void ATriangleTheSourceDrawsNothingForLeavesNoQuad()
    {
        var layers = new ImageLayers();

        var bands = Bands([Triangle(0, 99), Triangle(0, 6)], 4, layers);

        Assert.HasCount(1, bands[0]);
        Assert.HasCount(1, layers.Images);
    }

    [TestMethod]
    public void NoTriangleGivesNoBandAndNoError()
    {
        Assert.IsEmpty(Bands([], 4));
    }

    [TestMethod]
    public void RowsFallIntoBandsOfTheGivenHeightAndEmptyBandsAreLeftOut()
    {
        var bands = Bands([Triangle(0), Triangle(15), Triangle(16), Triangle(40)], 48);

        Assert.AreEqual(3, bands.Count);
        Assert.AreEqual(2, bands[0].Count);
        Assert.AreEqual(1, bands[1].Count);
        Assert.AreEqual(1, bands[2].Count);
    }

    [TestMethod]
    public void TrianglesNotSortedByRowAreRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Bands([Triangle(20), Triangle(3)], 48));
    }

    [TestMethod]
    public void ATriangleBelowTheLastRowIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Bands([Triangle(0), Triangle(48)], 48));
    }

    [TestMethod]
    public void AStripHangsFromItsAnchorByTheImagesOffsetAndSharesTheLayersOfTheTerrain()
    {
        var layers = new ImageLayers();
        Bands([Triangle(0)], 4, layers);
        var map = new Map(MapSize.Offered[0]);
        map.At(10, 10).Roads = RoadSet.Of(0);
        // A road the archive has no strip for: a height difference of 5.
        map.At(20, 10).Roads = RoadSet.Of(0);
        map.At(20, 10).Height = 5;

        var quads = TerrainQuads.Edges(map, new FakeSource(), layers);

        var strip = RoadDraws.Strip(map, 10, 10, RoadDirection.East);
        Assert.HasCount(1, quads);
        Assert.AreEqual(
            new ObjectQuad
            {
                X = strip.X + 1,
                Y = strip.Y - 3,
                Width = 8,
                Height = 5,
                Layer = 1,
            },
            quads[0]
        );
    }

    [TestMethod]
    public void TheRoadPassWalksTheHalfColumnsWestToEastEachZigzagBeforeItsEastEdges()
    {
        var map = new Map(MapSize.Offered[0]);
        map.At(10, 10).Owner = Owner.Player(0);

        var posts = EdgeTerms.Posts(map).Select(post => (post.Column, post.Row, post.Direction));

        // The node 10,10 lies on the half-column 2 * 10 - 10 = 10. The east edge of 9,10 (half-column 8); the zigzag
        // east of half-column 9, down the rows: south-east of 9,9, south of 10,10; the zigzag east of half-column 10:
        // south of 10,9, south-east of 10,10; the east edge of 10,10.
        CollectionAssert.AreEqual(
            new[]
            {
                (9, 10, RoadDirection.East),
                (9, 9, RoadDirection.SouthEast),
                (10, 10, RoadDirection.South),
                (10, 9, RoadDirection.South),
                (10, 10, RoadDirection.SouthEast),
                (10, 10, RoadDirection.East),
            },
            posts.ToArray()
        );
    }

    [TestMethod]
    public void OnTheLargestMapTheRoadPassHandsEachEdgeOnce()
    {
        var map = OwnersDifferOnEveryEdge();
        var (width, height) = (map.Size.Width, map.Size.Height);

        var edges = EdgeTerms.Posts(map).Select(post => (post.Column, post.Row, post.Direction)).ToList();

        Assert.HasCount(3 * width * height, edges.Distinct());
        Assert.HasCount(3 * width * height, edges);
    }

    [TestMethod]
    public void OnTheLargestMapFullOfRoadsTheRoadPassHandsAStripForEveryEdge()
    {
        // Every edge would carry a post; the road bits take each one's place.
        var map = OwnersDifferOnEveryEdge();
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Roads = RoadSet.Of(0).With(1).With(2);
        }

        var terms = EdgeTerms.Of(map);

        Assert.HasCount(3 * map.Size.Width * map.Size.Height, terms.OfType<RoadStrip>());
        Assert.HasCount(terms.Count, terms.OfType<RoadStrip>());
    }

    // The largest offered map with owners 0 to 3 by column plus twice the row: the east, south-east, and south neighbors
    // differ by 1, 3, and 2, and the pattern repeats across both seams, so the owners differ on every edge.
    static Map OwnersDifferOnEveryEdge()
    {
        var map = new Map(MapSize.Offered[^1]);
        for (var row = 0; row < map.Size.Height; row++)
        {
            for (var column = 0; column < map.Size.Width; column++)
            {
                map.At(column, row).Owner = Owner.Player((column + 2 * row) % 4);
            }
        }

        return map;
    }

    [TestMethod]
    public void TheRoadPassDrawsEachEdgeAsAStripOrAPostInTheOneWalk()
    {
        var map = new Map(MapSize.Offered[0]);
        map.At(10, 10).Roads = RoadSet.Of(0);
        // The south neighbor is a player's: the node's east edge is a road, its south edge a border.
        map.At(10, 11).Owner = Owner.Player(0);

        var quads = TerrainQuads.Edges(map, new FakeSource(), new ImageLayers());

        // The east edge of 9,11; the zigzag east of half-column 8: 9,10 south-east, 10,11 south; the zigzag east of
        // half-column 9: 10,10 south, 10,11 south-east; the east edge of 10,11; the east strip of 10,10 (half-column 10).
        CollectionAssert.AreEqual(new[] { 6, 6, 6, 6, 6, 6, 8 }, quads.Select(quad => quad.Width).ToArray());
        var post = BorderDraws.Post(map, 10, 10, RoadDirection.South)!.Value;
        Assert.AreEqual(post.X - 2, quads[3].X);
        Assert.AreEqual(post.Y - 7, quads[3].Y);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryTriangleOfAMapLandsInExactlyOneBand()
    {
        var map = MapViewState.Open(TestProgram.Tables(), "1234567812345678", 1).Map;
        Assert.IsNotNull(map);
        var triangles = TerrainTriangles.Of(map);

        var bands = Bands(triangles, map.Size.Height);

        Assert.AreEqual(triangles.Count, bands.Sum(band => band.Count));
        Assert.IsLessThanOrEqualTo((map.Size.Height + 15) / 16, bands.Count);
        var chunks = TerrainTiling.Chunks(1, map.Size.Height, 16);
        Assert.HasCount(chunks.Count, bands);
        for (var i = 0; i < bands.Count; i++)
        {
            var (_, row, _, height) = chunks[i];
            Assert.HasCount(triangles.Count(t => t.Row >= row && t.Row < row + height), bands[i]);
        }
    }
}
