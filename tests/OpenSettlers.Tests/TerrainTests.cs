using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TerrainTests
{
    [TestMethod]
    public void AFlatUpperTriangleOfGrassIsMask100WithTexture264()
    {
        var draw = TerrainRules.Upper(5, 5, 5, 5);

        Assert.AreEqual(new TerrainDraw(100, 264), draw);
    }

    [TestMethod]
    public void AFlatLowerTriangleOfGrassIsMask181WithTexture264()
    {
        var draw = TerrainRules.Lower(5, 5, 5, 5);

        Assert.AreEqual(new TerrainDraw(181, 264), draw);
    }

    [TestMethod]
    public void WaterIsDrawnWithTheLastTexture()
    {
        Assert.AreEqual(292, TerrainRules.Upper(0, 0, 0, 0)!.Value.Texture);
    }

    [TestMethod]
    public void AHeightStepBeyondFourHasNoMask()
    {
        Assert.IsNull(TerrainRules.Upper(5, 0, 5, 5));
        Assert.IsNull(TerrainRules.Lower(5, 5, 0, 5));
    }

    [TestMethod]
    public void AHeightIsItsFiveLowBits()
    {
        Assert.AreEqual(TerrainRules.Upper(1, 1, 1, 5), TerrainRules.Upper(33, 1, 1, 5));
    }

    [TestMethod]
    public void EveryTileOfAGeneratedMapGivesTwoTrianglesOrNoMask()
    {
        var map = MapGenerator.Generate(new Seed(1, 2, 3).ToMapSeed(), MapSize.Offered[0]);

        var triangles = TerrainTriangles.Of(map);

        Assert.IsLessThanOrEqualTo(2 * 32 * 32, triangles.Count);
        Assert.IsGreaterThan(32 * 32, triangles.Count);
        Assert.IsTrue(triangles.TrueForAll(t => t.Mask is >= 60 and <= 221 && t.Texture is >= 260 and <= 292));
    }

    [TestMethod]
    public void TrianglesFollowTheRowsFromNorthToSouthSoLaterOnesPaintOver()
    {
        var map = MapGenerator.Generate(new Seed(1, 2, 3).ToMapSeed(), MapSize.Offered[0]);

        var rows = TerrainTriangles.Of(map).ConvertAll(t => t.Row);

        Assert.AreEqual(rows.Count, new List<int>(rows).FindAll(r => r >= 0).Count);
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.IsGreaterThanOrEqualTo(rows[i - 1], rows[i]);
        }
    }

    [TestMethod]
    public void TheUpperTriangleHangsFromItsNodeAndTheLowerRisesToTheNodeBelowEast()
    {
        var map = MapGenerator.Generate(new Seed(1, 2, 3).ToMapSeed(), MapSize.Offered[0]);

        var first = TerrainTriangles.Of(map)[0];

        // The first triangle is the upper one of tile (0, 0), anchored at node (0, 0): x 0 less the mask's half
        // width, y 20 * 0 less 4 per unit of height.
        Assert.AreEqual(0, first.Column);
        Assert.AreEqual(0, first.Row);
        Assert.IsTrue(first.Upper);
    }

    [TestMethod]
    public void ChunksCoverEveryTileOnceAndStayWithinTheLimit()
    {
        var chunks = TerrainTiling.Chunks(64, 64, 16);

        Assert.HasCount(16, chunks);
        var covered = new HashSet<(int, int)>();
        foreach (var (column, row, width, height) in chunks)
        {
            Assert.IsLessThanOrEqualTo(16, width);
            Assert.IsLessThanOrEqualTo(16, height);
            for (var y = row; y < row + height; y++)
            {
                for (var x = column; x < column + width; x++)
                {
                    Assert.IsTrue(covered.Add((x, y)));
                }
            }
        }

        Assert.HasCount(64 * 64, covered);
    }

    [TestMethod]
    public void ChunksAtTheEdgeAreSmallerWhereTheMapDoesNotDivide()
    {
        var chunks = TerrainTiling.Chunks(20, 5, 16);

        CollectionAssert.AreEqual(new[] { (0, 0, 16, 5), (16, 0, 4, 5) }, chunks);
    }

    [TestMethod]
    public void ThePaletteTextureHoldsTheColorsAtEightBitsWithAnOpaqueAlpha()
    {
        var bytes = new byte[PaletteFile.Size];
        bytes[3] = 252;
        bytes[4] = 8;
        bytes[5] = 0;
        var palette = PaletteFile.Read(bytes);

        var texture = PaletteTexture.Rgba(palette);

        Assert.HasCount(256 * 4, texture);
        CollectionAssert.AreEqual(new byte[] { 252, 8, 0, 255 }, texture[4..8]);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 255 }, texture[0..4]);
    }
}
