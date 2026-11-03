using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The stand-in readout of the castle's log count in the play view (the original's stock window is a later slice's).
[TestClass]
public sealed class CastleReadoutTests
{
    [TestMethod]
    [DataRow(0, "Logs 0")]
    [DataRow(1, "Logs 1")]
    [DataRow(50000, "Logs 50000")]
    public void TheReadoutNamesTheLogCount(int logs, string expected)
    {
        Assert.AreEqual(expected, CastleReadout.Text(logs));
    }
}
