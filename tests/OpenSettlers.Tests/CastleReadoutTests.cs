using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The stand-in readout of the castle's log, plank, stone, and food counts in the play view (the original's stock window is a later slice's).
[TestClass]
public sealed class CastleReadoutTests
{
    [TestMethod]
    [DataRow(0, 0, 0, "Logs 0  Planks 0  Stones 0  Fish 0  Pigs 0  Meat 0  Grain 0  Flour 0  Bread 0")]
    [DataRow(1, 53, 28, "Logs 1  Planks 53  Stones 28  Fish 0  Pigs 0  Meat 0  Grain 0  Flour 0  Bread 0")]
    [DataRow(50000, 7, 50000, "Logs 50000  Planks 7  Stones 50000  Fish 0  Pigs 0  Meat 0  Grain 0  Flour 0  Bread 0")]
    public void TheReadoutNamesTheLogPlankAndStoneCounts(int logs, int planks, int stones, string expected)
    {
        Assert.AreEqual(expected, CastleReadout.Text(logs, planks, stones, [0, 0, 0, 0, 0, 0]));
    }

    [TestMethod]
    public void TheReadoutNamesEachFoodGoodBesideTheLogsPlanksAndStones()
    {
        Assert.AreEqual(
            "Logs 4  Planks 9  Stones 2  Fish 11  Pigs 12  Meat 13  Grain 14  Flour 15  Bread 16",
            CastleReadout.Text(4, 9, 2, [11, 12, 13, 14, 15, 16])
        );
    }
}
