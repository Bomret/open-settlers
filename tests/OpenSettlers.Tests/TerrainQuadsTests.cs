using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TerrainQuadsTests
{
    static List<MaskImage> Masks()
    {
        var masks = new List<MaskImage>();
        for (var i = 0; i < 200; i++)
        {
            masks.Add(
                new MaskImage(30, 10, 3, -2, new byte[TerrainGraphics.LayerWidth * TerrainGraphics.MaskLayerHeight])
            );
        }

        return masks;
    }

    static TerrainTriangle Triangle(int row, int x = 100, int y = 50) => new(0, row, true, 61, 262, x, y);

    [TestMethod]
    public void TheQuadHangsFromItsAnchorCenteredAndMovedByTheHeaderOffset()
    {
        var bands = TerrainQuads.Bands([Triangle(0)], Masks(), 4, 16);

        var quad = bands[0][0];
        Assert.AreEqual(100 - TerrainGraphics.LayerWidth / 2 + 3, quad.X);
        Assert.AreEqual(50 - 2, quad.Y);
        Assert.AreEqual(30, quad.Width);
        Assert.AreEqual(10, quad.Height);
    }

    [TestMethod]
    public void TheQuadNamesTheMaskAndTextureLayers()
    {
        var quad = TerrainQuads.Bands([Triangle(0)], Masks(), 4, 16)[0][0];

        Assert.AreEqual(TerrainGraphics.MaskLayer(61), quad.MaskLayer);
        Assert.AreEqual(TerrainGraphics.TextureLayer(262), quad.TextureLayer);
    }

    [TestMethod]
    public void RowsFallIntoBandsOfTheGivenHeightAndEmptyBandsAreLeftOut()
    {
        var bands = TerrainQuads.Bands([Triangle(0), Triangle(15), Triangle(16), Triangle(40)], Masks(), 48, 16);

        Assert.AreEqual(3, bands.Count);
        Assert.AreEqual(2, bands[0].Count);
        Assert.AreEqual(1, bands[1].Count);
        Assert.AreEqual(1, bands[2].Count);
    }

    [TestMethod]
    public void TrianglesNotSortedByRowAreRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => TerrainQuads.Bands([Triangle(20), Triangle(3)], Masks(), 48, 16));
    }

    [TestMethod]
    public void ATriangleBelowTheLastRowIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => TerrainQuads.Bands([Triangle(0), Triangle(48)], Masks(), 48, 16));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryTriangleOfAMapLandsInExactlyOneBand()
    {
        var map = MapViewState.Open(TestProgram.Tables(), "1234567812345678", 1).Map;
        Assert.IsNotNull(map);
        var triangles = TerrainTriangles.Of(map);

        var bands = TerrainQuads.Bands(triangles, Masks(), map.Size.Height, 16);

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
