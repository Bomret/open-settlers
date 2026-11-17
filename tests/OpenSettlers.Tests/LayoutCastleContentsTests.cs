using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle contents a layout's castle entry names (docs/rules/layouts.md): the goods it holds under "stock". The map is
// flat grass and the castle stands at 10,10, as in LayoutApplyTests.
[TestClass]
public sealed class LayoutCastleContentsTests
{
    static string[] Problems(string castle) =>
        [.. Layout.Read(LayoutTests.Text(extra: "\n\"castle\": " + castle), "t.jsonc").Problems];

    static Layout Read(string extra)
    {
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + extra), "t.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    static Game NewGame(Layout layout, int stockLevel = 0)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var tables = new RuleTables(classes, TestProgram.MadeStartStock(), workers: TestProgram.MadeWorkers());

        return GameFactory.Create(tables, layout.BuildMap(), stockLevel);
    }

    static Game Applied(string extra, int stockLevel = 0)
    {
        var layout = Read(extra);
        var game = NewGame(layout, stockLevel);
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(40)]
    public void ACastleThatNamesItsGoodsHoldsExactlyThoseWhateverItsStartStock(int level)
    {
        var game = Applied(
            """ "castle": {"x": 10, "y": 10, "stock": {"scythe": 1, "butcher-knife": 2, "grain": 3}} """,
            level
        );

        var expected = new int[StartStock.Goods];
        expected[20 - 1] = 1;
        expected[19 - 1] = 2;
        expected[Good.Grain.Code - 1] = 3;
        CollectionAssert.AreEqual(expected, game.CastleStock.Stock.ToArray());
    }

    [TestMethod]
    public void AnEmptyStockLeavesTheCastleWithNoGoods()
    {
        var game = Applied(""" "castle": {"x": 10, "y": 10, "stock": {}} """, 40);

        CollectionAssert.AreEqual(new int[StartStock.Goods], game.CastleStock.Stock.ToArray());
    }

    [TestMethod]
    public void AGoodTheCastleWasNotNamedCountsLaterDeliveries()
    {
        var game = Applied(""" "castle": {"x": 10, "y": 10, "stock": {"grain": 1}} """);

        game.CastleStock.Add(Good.Log);
        game.CastleStock.Add(Good.Log);

        Assert.AreEqual(2, game.CastleStock.Logs);
        Assert.IsTrue(game.CastleStock.Take(Good.Grain));
        Assert.AreEqual(0, game.CastleStock.Count(Good.Grain));
    }

    [TestMethod]
    public void ACastleThatNamesItsGoodsKeepsTheTwentyFirstSettlers()
    {
        var named = Applied(""" "castle": {"x": 10, "y": 10, "stock": {}} """);
        var plain = Applied(""" "castle": {"x": 10, "y": 10} """);

        CollectionAssert.AreEqual(
            plain.Settlers.Select(settler => settler.Job).ToArray(),
            named.Settlers.Select(settler => settler.Job).ToArray()
        );
        Assert.AreEqual(20, named.Settlers.Count);
    }

    [TestMethod]
    public void ALayoutWithNoStockAndACastlePlacedByAClickGiveTheSameGame()
    {
        var layout = Read(""" "castle": {"x": 10, "y": 10} """);
        var byLayout = NewGame(layout, 40);
        Assert.IsNull(layout.Apply(byLayout));
        var byClick = NewGame(layout, 40);
        byClick.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 10,
                Y = 10,
            }
        );

        // The click's command applies at the start of tick 0; the layout applied it before that tick.
        for (var tick = 0; tick < 50; tick++)
        {
            byLayout.Tick();
            byClick.Tick();
            Assert.AreEqual(byClick.Digest(), byLayout.Digest(), $"tick {tick}");
        }

        CollectionAssert.AreEqual(byClick.CastleStock.Stock.ToArray(), byLayout.CastleStock.Stock.ToArray());
    }

    [TestMethod]
    public void AStockKeyOnAFlagEntryIsAnUnknownKey()
    {
        CollectionAssert.AreEqual(
            new[] { "t.jsonc: flags[0]: unknown key stock, an entry is {\"x\", \"y\"}" },
            Layout
                .Read(LayoutTests.Text(extra: "\n\"flags\": [{\"x\": 1, \"y\": 1, \"stock\": {}}]"), "t.jsonc")
                .Problems.ToArray()
        );
    }

    [TestMethod]
    [DataRow("{\"bread-roll\": 1}", "castle: stock: bread-roll is no good's name")]
    [DataRow("{\"tree\": 1}", "castle: stock: tree is no good's name")]
    [DataRow("{\"farmer\": 1}", "castle: stock: farmer is no good's name")]
    [DataRow("{\"grain\": 1, \"grain\": 2}", "castle: stock: grain is given twice")]
    [DataRow("[\"grain\"]", "castle: stock must be an object of good names and counts, such as {\"grain\": 1}")]
    [DataRow("3", "castle: stock must be an object of good names and counts, such as {\"grain\": 1}")]
    [DataRow("{\"grain\": -1}", "castle: stock: grain: the count must be a whole number from 0 to 50000")]
    [DataRow("{\"grain\": 50001}", "castle: stock: grain: the count must be a whole number from 0 to 50000")]
    [DataRow("{\"grain\": 1.5}", "castle: stock: grain: the count must be a whole number from 0 to 50000")]
    [DataRow("{\"grain\": \"1\"}", "castle: stock: grain: the count must be a whole number from 0 to 50000")]
    [DataRow("{\"grain\": true}", "castle: stock: grain: the count must be a whole number from 0 to 50000")]
    [DataRow("{\"grain\": 1e10}", "castle: stock: grain: the count must be a whole number from 0 to 50000")]
    public void ABadStockIsOneProblem(string stock, string problem)
    {
        CollectionAssert.AreEqual(
            new[] { "t.jsonc: " + problem },
            Problems("{\"x\": 10, \"y\": 10, \"stock\": " + stock + "}")
        );
    }

    [TestMethod]
    public void CountsOfZeroAndFiftyThousandAreRead()
    {
        var game = Applied(""" "castle": {"x": 10, "y": 10, "stock": {"log": 0, "plank": 50000}} """);

        Assert.AreEqual(0, game.CastleStock.Logs);
        Assert.AreEqual(50000, game.CastleStock.Planks);
    }
}
