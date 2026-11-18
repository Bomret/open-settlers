using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle contents a layout's castle entry names (docs/rules/layouts.md): the goods it holds under "stock" and the settlers
// that wait in it under "settlers". The map is flat grass and the castle stands at 10,10, as in LayoutApplyTests. The made
// tables' woodcutter takes a fish as his tool (TestProgram.MadeWorkers).
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

    static Job[] Jobs(Game game) => [.. game.Settlers.Select(settler => settler.Job)];

    [TestMethod]
    public void ACastleThatNamesItsSettlersHoldsItsCastleSettlerAndThoseInFileOrder()
    {
        var game = Applied(
            """ "castle": {"x": 10, "y": 10, "settlers": {"woodcutter": 1, "jobless": 2, "carrier": 1}} """
        );

        CollectionAssert.AreEqual(
            new Job[] { new CastleSettler(), new Woodcutter(), new Jobless(), new Jobless(), new Carrier() },
            Jobs(game)
        );
        Assert.IsInstanceOfType<BuildsCastle>(game.Settlers[0].Activity);
        Assert.IsTrue(game.Settlers.Skip(1).All(settler => settler.Activity is WaitsInCastle));
    }

    [TestMethod]
    public void AnEmptySettlersLeavesTheCastleSettlerAloneAndTheCastleIsStillBuilt()
    {
        var game = Applied(""" "castle": {"x": 10, "y": 10, "settlers": {}} """);

        CollectionAssert.AreEqual(new Job[] { new CastleSettler() }, Jobs(game));
        for (var tick = 0; tick < 20000 && game.Buildings[0].Constructing; tick++)
        {
            game.Tick();
        }

        Assert.IsFalse(game.Buildings[0].Constructing, "the castle settler builds the castle");
    }

    [TestMethod]
    public void ACastleThatNamesItsSettlersButNotItsGoodsKeepsTheStartStock()
    {
        var named = Applied(""" "castle": {"x": 10, "y": 10, "settlers": {"jobless": 1}} """, 25);
        var plain = Applied(""" "castle": {"x": 10, "y": 10} """, 25);

        CollectionAssert.AreEqual(plain.CastleStock.Stock.ToArray(), named.CastleStock.Stock.ToArray());
    }

    const string Woodcutter = """ "buildings": [{"type": "woodcutter", "x": 13, "y": 10}] """;

    [TestMethod]
    public void ABuildingTakesANamedSettlerOfItsJobBeforeOneWithoutAJob()
    {
        var game = Applied(
            """ "castle": {"x": 10, "y": 10, "settlers": {"jobless": 1, "woodcutter": 1}, "stock": {"fish": 1}}, """
                + Woodcutter
        );

        Assert.AreEqual(1, game.CastleStock.Count(Good.Fish), "no tool was taken");
        Assert.IsInstanceOfType<WaitsInCastle>(game.Settlers[1].Activity, "the settler without a job still waits");
        Assert.IsNotInstanceOfType<WaitsInCastle>(game.Settlers[2].Activity, "the woodcutter went to his hut");
    }

    [TestMethod]
    public void ABuildingWithNoNamedSettlerOfItsJobMakesOneWithoutAJobWithTheTool()
    {
        var game = Applied(
            """ "castle": {"x": 10, "y": 10, "settlers": {"jobless": 1}, "stock": {"fish": 1}}, """ + Woodcutter
        );

        Assert.AreEqual(0, game.CastleStock.Count(Good.Fish), "the castle gave up the tool");
        Assert.AreEqual(new Woodcutter(), game.Settlers[1].Job);
        Assert.IsNotInstanceOfType<WaitsInCastle>(game.Settlers[1].Activity);
    }

    [TestMethod]
    [DataRow(""" "settlers": {}, "stock": {"fish": 1} """)]
    [DataRow(""" "settlers": {"jobless": 1}, "stock": {} """)]
    [DataRow(""" "settlers": {"carrier": 1}, "stock": {"fish": 1} """)]
    public void AFinishedBuildingWhoseWorkerTheCastleCannotGiveFailsTheLoad(string contents)
    {
        var layout = Read(""" "castle": {"x": 10, "y": 10, """ + contents + "}, " + Woodcutter);

        Assert.AreEqual(
            "t.jsonc: buildings[0]: the castle holds no woodcutter for the building",
            layout.Apply(NewGame(layout))
        );
    }

    const string RoadWithItsCarrier = """
         "flags": [{"x": 14, "y": 11}],
         "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
         "settlers": [{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}]
        """;

    [TestMethod]
    public void ARoadTakesANamedCarrierBeforeOneWithoutAJob()
    {
        var game = Applied(
            """ "castle": {"x": 10, "y": 10, "settlers": {"jobless": 1, "carrier": 1}}, """ + RoadWithItsCarrier
        );

        Assert.IsInstanceOfType<WaitsInCastle>(game.Settlers[1].Activity, "the settler without a job still waits");
        Assert.IsNotInstanceOfType<WaitsInCastle>(game.Settlers[2].Activity, "the carrier is on the road");
    }

    [TestMethod]
    public void ARoadWithNoNamedCarrierTakesOneWithoutAJob()
    {
        var game = Applied(""" "castle": {"x": 10, "y": 10, "settlers": {"jobless": 1}}, """ + RoadWithItsCarrier);

        Assert.AreEqual(new Carrier(), game.Settlers[1].Job);
        Assert.IsNotInstanceOfType<WaitsInCastle>(game.Settlers[1].Activity);
    }

    [TestMethod]
    public void ASettlersKeyOnAFlagEntryIsAnUnknownKey()
    {
        CollectionAssert.AreEqual(
            new[] { "t.jsonc: flags[0]: unknown key settlers, an entry is {\"x\", \"y\"}" },
            Layout
                .Read(LayoutTests.Text(extra: "\n\"flags\": [{\"x\": 1, \"y\": 1, \"settlers\": {}}]"), "t.jsonc")
                .Problems.ToArray()
        );
    }

    const string NoJob =
        " is no job's name: a castle names carrier, digger, builder, woodcutter, sawmiller, stonecutter, forester, "
        + "fisher, pig-farmer, butcher, farmer, miller, baker, or jobless";
    const string BadCount = ": the count must be a whole number from 0";

    [TestMethod]
    [DataRow("{\"lumberjack\": 1}", "castle: settlers: lumberjack" + NoJob)]
    [DataRow("{\"mill\": 1}", "castle: settlers: mill" + NoJob)]
    [DataRow("{\"knight\": 1}", "castle: settlers: knight" + NoJob)]
    [DataRow("{\"grain\": 1}", "castle: settlers: grain" + NoJob)]
    [DataRow(
        "{\"castle-settler\": 1}",
        "castle: settlers: castle-settler is the castle's own settler, whom the castle always holds and no layout names"
    )]
    [DataRow("{\"jobless\": 1, \"jobless\": 2}", "castle: settlers: jobless is given twice")]
    [DataRow("[\"jobless\"]", "castle: settlers must be an object of job names and counts, such as {\"jobless\": 1}")]
    [DataRow("3", "castle: settlers must be an object of job names and counts, such as {\"jobless\": 1}")]
    [DataRow("{\"jobless\": -1}", "castle: settlers: jobless" + BadCount)]
    [DataRow("{\"jobless\": 1.5}", "castle: settlers: jobless" + BadCount)]
    [DataRow("{\"jobless\": \"1\"}", "castle: settlers: jobless" + BadCount)]
    public void BadSettlersAreOneProblem(string settlers, string problem)
    {
        CollectionAssert.AreEqual(
            new[] { "t.jsonc: " + problem },
            Problems("{\"x\": 10, \"y\": 10, \"settlers\": " + settlers + "}")
        );
    }

    [TestMethod]
    public void SettlersBeyondTheSettlerTableAreOneProblem()
    {
        // The map of size 1 holds a table of 2000 settlers, the game one less (SettlerTable.IsFull); the castle settler is one.
        var most = SettlerTable.SizeOf(Layout.Read(LayoutTests.Text()).Layout!.Size) - 2;

        Assert.IsEmpty(
            Problems("{\"x\": 10, \"y\": 10, \"settlers\": {\"jobless\": " + (most - 1) + ", \"carrier\": 1}}")
        );
        CollectionAssert.AreEqual(
            new[]
            {
                $"t.jsonc: castle: settlers: {most + 1} settlers and the castle settler are more than the {most + 1} the settler table holds",
            },
            Problems("{\"x\": 10, \"y\": 10, \"settlers\": {\"jobless\": " + most + ", \"carrier\": 1}}")
        );
    }

    [TestMethod]
    public void ALayoutWithNoSizeFailsTheLoadWhateverCountItsCastleNames()
    {
        var read = Layout.Read(
            "{\"castle\": {\"x\": 10, \"y\": 10, \"settlers\": {\"jobless\": 2147483647}}}",
            "t.jsonc"
        );

        Assert.IsNotEmpty(read.Problems);
    }
}
