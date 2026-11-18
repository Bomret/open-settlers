using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle readout's bar against the basic set's and the install's start stock, which the map view's test project cannot reach;
// the rest of this class is in src/MapView.Tests.
[TestClass]
public sealed class CastleReadoutTests
{
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

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output");
    }

    static RuleTables Basic() => new OpenSettlersRuleTables(Path.Combine(RepositoryRoot(), "content", "basic")).Read();

    // The rule of the bar rests on this: each content set lists every good it has in each row of its start stock, zeros included.
    [TestMethod]
    public void EveryStartStockRowOfTheBasicSetAndTheMadeTablesListsEveryGoodOfItsCatalog()
    {
        foreach (var tables in new[] { Basic(), MadeProgram.Made() })
        {
            AssertEveryRowListsEveryGood(tables);
        }
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryStartStockRowOfTheInstallListsEveryGoodOfItsCatalog()
    {
        AssertEveryRowListsEveryGood(TestProgram.Tables());
    }

    static void AssertEveryRowListsEveryGood(RuleTables tables)
    {
        var names = tables.Catalog.Goods.Select(entry => entry.Name).Order().ToArray();
        foreach (var row in tables.StartStock)
        {
            CollectionAssert.AreEqual(names, row.Keys.Select(good => good.Name).Order().ToArray());
        }
    }

    [TestMethod]
    public void TheBasicSetAddsNoGoodHoldingOneOfEachItHas()
    {
        var tables = Basic();
        var stock = Holding(tables.Catalog, tables.Catalog.Goods.Select(entry => entry.Name), 1);

        Assert.IsEmpty(CastleReadout.AddedGoods(stock, StartGoodsOf(tables)));
    }

    [TestMethod]
    public void TheBasicSetAddsNoGoodAtGameStart()
    {
        var tables = Basic();
        var stock = new CastleStock(tables.Catalog);
        stock.Place(
            new Castle
            {
                X = 1,
                Y = 1,
                Height = 1,
            },
            StartStock.At(tables, 0)
        );

        Assert.IsEmpty(CastleReadout.AddedGoods(stock, StartGoodsOf(tables)));
    }
}
