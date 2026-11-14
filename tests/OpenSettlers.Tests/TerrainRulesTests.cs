using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The mask and texture of a terrain triangle (docs/rules/graphics-images.md, "Terrain drawing"; 0x4278be upper, 0x427a25 lower).
// Index = (a + 4 - b) + 9 x (a' + 4 - b'); the mask is 60 + index (upper) or 141 + index (lower); the variant the 81-byte table gives,
// divided by 4, is the texture's step k: codes 0 to 3 give 292, 4 to 7 give 260 + k, 8 to 10 give 284 + k, 11 to 13 give 268 + k, 14
// and 15 give 276 + k. The table values are read off the tables at 0x4279d4 (upper) and 0x427b38 (lower) by row and column.
[TestClass]
public sealed class TerrainRulesTests
{
    static int Texture(int code, int k) =>
        code switch
        {
            <= 3 => 292,
            <= 7 => 260 + k,
            <= 10 => 284 + k,
            <= 13 => 268 + k,
            _ => 276 + k,
        };

    // Upper: apex 10, left 11, right 12 gives the slopes 3 and 2, the index 3 + 9 x 2 = 21, row 2 column 3 of the upper table: 0x0c, k 3.
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(15)]
    public void AnUpperTriangleWithASlopeTakesTheTextureOfItsTerrainAndStep(int code)
    {
        var draw = TerrainRules.Upper(10, 11, 12, new Terrain { Code = code });

        Assert.AreEqual(new TerrainDraw(60 + 21, Texture(code, 3)), draw);
    }

    // Lower: apex 10, left 6, right 8 gives the slopes 0 and 2 (left + 4 - apex, right + 4 - apex), the index 0 + 9 x 2 = 18, row 2
    // column 0 of the lower table: 0x0c, k 3.
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(13)]
    [DataRow(14)]
    [DataRow(15)]
    public void ALowerTriangleWithASlopeTakesTheTextureOfItsTerrainAndStep(int code)
    {
        var draw = TerrainRules.Lower(10, 6, 8, new Terrain { Code = code });

        Assert.AreEqual(new TerrainDraw(141 + 18, Texture(code, 3)), draw);
    }

    // The largest step: upper row 1 column 5 (0x1c, k 7) is the slopes 5 and 1; lower index 36 (0x1c, k 7) the slopes 0 and 4.
    [TestMethod]
    public void TheLargestStepsOfTheTablesAreReached()
    {
        Assert.AreEqual(new TerrainDraw(60 + 14, 260 + 7), TerrainRules.Upper(10, 9, 13, new Terrain { Code = 7 }));
        Assert.AreEqual(new TerrainDraw(141 + 36, 260 + 7), TerrainRules.Lower(10, 6, 10, new Terrain { Code = 5 }));
    }

    // Level ground: the slopes 4 and 4, the index 40, 0x10 in both tables, k 4; masks 100 and 181.
    [TestMethod]
    public void LevelGroundTakesTheMiddleOfBothTables()
    {
        Assert.AreEqual(new TerrainDraw(100, 264), TerrainRules.Upper(7, 7, 7, new Terrain { Code = 5 }));
        Assert.AreEqual(new TerrainDraw(181, 264), TerrainRules.Lower(7, 7, 7, new Terrain { Code = 5 }));
    }

    // The heights are the five low bits: 42 is 10.
    [TestMethod]
    public void OnlyTheFiveLowBitsOfAHeightCount()
    {
        Assert.AreEqual(
            TerrainRules.Upper(10, 11, 12, new Terrain { Code = 5 }),
            TerrainRules.Upper(42, 43, 44, new Terrain { Code = 5 })
        );
        Assert.AreEqual(
            TerrainRules.Lower(10, 6, 8, new Terrain { Code = 5 }),
            TerrainRules.Lower(42, 38, 40, new Terrain { Code = 5 })
        );
    }

    // Upper index 5 (slopes 5 and 0) and lower index 5 (slopes 5 and 0) hold 0xff, no texture: no draw.
    [TestMethod]
    public void ATableValueOf0xffGivesNoDraw()
    {
        Assert.IsNull(TerrainRules.Upper(10, 9, 14, new Terrain { Code = 5 }));
        Assert.IsNull(TerrainRules.Lower(10, 11, 6, new Terrain { Code = 5 }));
    }

    // A slope two nodes beyond the table's end (an index of 81 or more, or below 0) has no entry in the 81 values.
    [TestMethod]
    public void AnIndexBeyondTheTableGivesNoDraw()
    {
        Assert.IsNull(TerrainRules.Upper(10, 14, 5, new Terrain { Code = 5 }));
        Assert.IsNull(TerrainRules.Lower(10, 6, 15, new Terrain { Code = 5 }));
    }

    // The aliasing of a slope of 9 or more with the next row (Upper(10, 5, 14) has the slopes 9 and 0, and the index 9 of the slopes 0
    // and 1) is a defect case with no stated outcome: the rules document gives the index formula and the 81 values only, and
    // TerrainRules.Upper's doc says null while the code draws. A triage record holds it; no test asserts it.
}
