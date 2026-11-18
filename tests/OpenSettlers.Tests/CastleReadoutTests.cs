using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

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

    // The basic content set's shape: no good has an original code, and the start stock holds goods beside the nine of the bar.
    static CastleStock PlacedWithNoCodes(params Good[] extra)
    {
        var entries = TestCatalog
            .WithAiToken()
            .Goods.Select(entry => entry with { OriginalCode = null })
            .Where(entry => entry.Name != TestCatalog.AiToken.Name || extra.Contains(TestCatalog.AiToken));
        var stock = new CastleStock(new ContentCatalog(entries));
        stock.Place(
            new Castle
            {
                X = 1,
                Y = 1,
                Height = 1,
            },
            TestCatalog.Names.ToDictionary(name => new Good { Name = name }, _ => 1)
        );

        return stock;
    }

    [TestMethod]
    public void TheBarOfACatalogWithNoCodesAddsNoGoodTheOriginalHas()
    {
        Assert.IsEmpty(CastleReadout.AddedGoods(PlacedWithNoCodes()));
    }

    [TestMethod]
    public void TheBarOfACatalogWithNoCodesNamesAGoodOutsideTheOriginalsByItsName()
    {
        var stock = PlacedWithNoCodes(TestCatalog.AiToken);
        stock.Add(TestCatalog.AiToken);

        CollectionAssert.AreEqual(new[] { ("ai-token", 1) }, CastleReadout.AddedGoods(stock).ToArray());
    }
}
