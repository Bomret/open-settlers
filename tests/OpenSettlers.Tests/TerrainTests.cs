using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TerrainTests
{
    [TestMethod]
    public void AHeightStepBeyondFourStillGivesATermForBothTriangles()
    {
        var map = new Map(MapSize.Offered[0]);
        map.At(3, 3).Height = 10;

        var triangles = TerrainTriangles.Of(map);

        Assert.HasCount(2 * 32 * 32, triangles);
        var upper = triangles.Single(t => t is { Column: 3, Row: 3, Upper: true });
        Assert.AreEqual(10, upper.Apex);
        Assert.AreEqual(0, upper.Left);
        Assert.AreEqual(0, upper.Right);
    }

    [TestMethod]
    public void ATermNamesTheTerrainAndTheHeightsOfItsThreeNodes()
    {
        var map = new Map(MapSize.Offered[0]);
        map.At(4, 4).Height = 1;
        map.At(4, 5).Height = 2;
        map.At(5, 5).Height = 3;
        map.At(5, 4).Height = 4;
        map.At(4, 4).Upper = new Terrain { Code = 6 };
        map.At(4, 4).Lower = new Terrain { Code = 7 };

        var triangles = TerrainTriangles.Of(map);

        var upper = triangles.Single(t => t is { Column: 4, Row: 4, Upper: true });
        Assert.AreEqual((new Terrain { Code = 6 }, 1, 2, 3), (upper.Terrain, upper.Apex, upper.Left, upper.Right));
        var lower = triangles.Single(t => t is { Column: 4, Row: 4, Upper: false });
        Assert.AreEqual((new Terrain { Code = 7 }, 3, 1, 4), (lower.Terrain, lower.Apex, lower.Left, lower.Right));
    }

    [TestMethod]
    public void EveryTileOfAGeneratedMapGivesTwoTriangles()
    {
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new Seed
            {
                W0 = 1,
                W1 = 2,
                W2 = 3,
            }.ToMapSeed(),
            MapSize.Offered[0]
        );

        var triangles = TerrainTriangles.Of(map);

        Assert.HasCount(2 * 32 * 32, triangles);
    }

    [TestMethod]
    public void TrianglesFollowTheRowsFromNorthToSouthSoLaterOnesPaintOver()
    {
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new Seed
            {
                W0 = 1,
                W1 = 2,
                W2 = 3,
            }.ToMapSeed(),
            MapSize.Offered[0]
        );

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
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new Seed
            {
                W0 = 1,
                W1 = 2,
                W2 = 3,
            }.ToMapSeed(),
            MapSize.Offered[0]
        );

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
    [DataRow(0)]
    [DataRow(-1)]
    public void ChunksWithASideOfZeroOrLessAreRefused(int side) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TerrainTiling.Chunks(4, 4, side));

    [TestMethod]
    [DataRow(0, 5)]
    [DataRow(5, 0)]
    public void ChunksOfNoRowsOrNoColumnsAreNone(int columns, int rows) =>
        Assert.AreEqual(0, TerrainTiling.Chunks(columns, rows, 16).Count);

    [TestMethod]
    public void ChunksAtTheEdgeAreSmallerWhereTheMapDoesNotDivide()
    {
        var chunks = TerrainTiling.Chunks(20, 5, 16);

        CollectionAssert.AreEqual(new[] { (0, 0, 16, 5), (16, 0, 4, 5) }, chunks);
    }
}
