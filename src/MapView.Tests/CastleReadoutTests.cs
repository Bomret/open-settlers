using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The stand-in readout of the castle's log, plank, stone, and food counts in the play view (the original's stock window is a later slice's).
// The tests against the basic set's and the install's start stock are in the pairs of the sources, since the map view cannot reach the
// Open Settlers source.
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

    // A castle of `catalog` holding `count` of each of `held`.
    static CastleStock Holding(ContentCatalog catalog, IEnumerable<string> held, int count)
    {
        var stock = new CastleStock(catalog);
        stock.Place(
            new Castle
            {
                X = 1,
                Y = 1,
                Height = 1,
            },
            held.ToDictionary(name => new Good { Name = name }, _ => count)
        );

        return stock;
    }

    static IEnumerable<IReadOnlyDictionary<Good, byte>> StartGoodsOf(RuleTables tables) => tables.StartStock;

    [TestMethod]
    public void TheBarOfACatalogWithNoCodesAddsNoGoodTheStartStockLists()
    {
        var tables = MadeProgram.Made();
        var stock = Holding(tables.Catalog, TestCatalog.Names, 1);

        Assert.IsEmpty(CastleReadout.AddedGoods(stock, StartGoodsOf(tables)));
    }

    [TestMethod]
    public void AGoodTheStartStockDoesNotListIsNamedByItsName()
    {
        var tables = MadeProgram.Made();
        var stock = Holding(TestCatalog.WithAiToken(), TestCatalog.Names, 1);
        stock.Add(TestCatalog.AiToken);

        CollectionAssert.AreEqual(
            new[] { ("ai-token", 1) },
            CastleReadout.AddedGoods(stock, StartGoodsOf(tables)).ToArray()
        );
    }

    [TestMethod]
    public void AGoodOnlyALaterStartStockRowNamesStaysOutOfTheBar()
    {
        var rows = MadeProgram
            .MadeStartStock()
            .Select(
                (row, at) =>
                    at == 1
                        ? (IReadOnlyDictionary<Good, byte>)row.Append(new(TestCatalog.AiToken, 3)).ToDictionary()
                        : row
            )
            .ToArray();
        var stock = Holding(TestCatalog.WithAiToken(), TestCatalog.Names, 1);
        stock.Add(TestCatalog.AiToken);

        Assert.IsEmpty(CastleReadout.AddedGoods(stock, rows));
    }

    [TestMethod]
    public void TwoAddedGoodsAreNamedInTheOrderOfTheirNames()
    {
        var tables = MadeProgram.Made();
        var zeta = new Good { Name = "zeta" };
        var entry = TestCatalog.WithAiToken().Require(TestCatalog.AiToken);
        var catalog = new ContentCatalog([
            .. TestCatalog.WithAiToken().Goods,
            entry with
            {
                Name = zeta.Name,
                TransportRank = 101,
            },
        ]);
        var stock = Holding(catalog, TestCatalog.Names, 1);
        stock.Add(zeta);
        stock.Add(zeta);
        stock.Add(TestCatalog.AiToken);

        CollectionAssert.AreEqual(
            new[] { ("ai-token", 1), ("zeta", 2) },
            CastleReadout.AddedGoods(stock, StartGoodsOf(tables)).ToArray()
        );
    }
}
