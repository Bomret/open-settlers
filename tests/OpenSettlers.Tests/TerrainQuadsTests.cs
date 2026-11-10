using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TerrainQuadsTests
{
    // A source that draws a 30 x 10 image hung 13 left of and 2 above its anchor for each terrain code except 99, and
    // draws a road strip of difference 0 only.
    sealed class FakeSource : IGraphicsSource
    {
        readonly Dictionary<int, ObjectImage> _images = [];

        public ObjectImage? Draw(TerrainTriangle triangle) =>
            triangle.Terrain.Code == 99 ? null
            : _images.TryGetValue(triangle.Terrain.Code, out var image) ? image
            : _images[triangle.Terrain.Code] = new ObjectImage(30, 10, -13, -2, new byte[30 * 10 * 4]);

        public ObjectImage? Draw(RoadStrip strip) =>
            strip.Difference == 0 ? new ObjectImage(8, 5, 1, -3, new byte[8 * 5 * 4]) : null;

        public ObjectDrawing? Draw(MapObjectDraw draw) => null;

        public ObjectDrawing? Draw(BuildingDraw draw) => null;

        public ObjectDrawing? Draw(GoodAtFlag draw) => null;

        public ObjectDrawing? Draw(SettlerDraw draw) => null;

        public RippleImage? Draw(WaterRipple ripple) => null;

        public int ObjectHeadroom => 0;
    }

    static TerrainTriangle Triangle(int row, int code = 5, int x = 100, int y = 50) =>
        new(0, row, true, new Domain.Terrain(code), 5, 5, 5, x, y);

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
        var strips = new[]
        {
            new RoadStrip(100, 200, RoadDirection.East, 0, 0, new Domain.Terrain(5)),
            new RoadStrip(300, 200, RoadDirection.East, 5, 0, new Domain.Terrain(5)),
        };

        var quads = TerrainQuads.Strips(strips, new FakeSource(), layers);

        Assert.HasCount(1, quads);
        Assert.AreEqual(new ObjectQuad(101, 197, 8, 5, 1), quads[0]);
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
