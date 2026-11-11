using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The stand-in readout of the castle's log, plank, and stone counts in the play view (the original's stock window is a later slice's).
[TestClass]
public sealed class CastleReadoutTests
{
    [TestMethod]
    [DataRow(0, 0, 0, "Logs 0  Planks 0  Stones 0")]
    [DataRow(1, 53, 28, "Logs 1  Planks 53  Stones 28")]
    [DataRow(50000, 7, 50000, "Logs 50000  Planks 7  Stones 50000")]
    public void TheReadoutNamesTheLogPlankAndStoneCounts(int logs, int planks, int stones, string expected)
    {
        Assert.AreEqual(expected, CastleReadout.Text(logs, planks, stones));
    }
}
