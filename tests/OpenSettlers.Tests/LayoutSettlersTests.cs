using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A layout's settlers and goods entries (docs/rules/layouts.md). The map is flat grass: the castle stands at 10,10 with its
// flag (slot 0) at 11,11; a flag at 14,11 (slot 1) is three steps east of it, and the hut at 13,10 ties to that flag.
[TestClass]
public sealed class LayoutSettlersTests
{
    const string Built = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        "buildings": [{"x": 13, "y": 10}],
        """;

    const string Woodcutter = """{"job": "woodcutter", "state": "in-hut", "x": 13, "y": 10}""";
    const string Carrier = """{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}""";

    // Trees (code 8) on the nodes of the hut's reach that lie east of its flag, on its row or the next two, where the walk goes
    // straight on over open ground.
    static readonly (int X, int Y)[] Trees =
    [
        .. MapGenerator
            .SpiralOffsets.Skip(1)
            .Take(128)
            .Select(o => (13 + o.Dx, 10 + o.Dy))
            .Where(n => n.Item1 >= 16 && n.Item2 is >= 10 and <= 12),
    ];

    static readonly string Tree =
        "[" + string.Join(", ", Trees.Select(n => $"{{\"x\": {n.X}, \"y\": {n.Y}, \"code\": 8}}")) + "]";

    static Layout Read(string extra, string objects = "[]")
    {
        var read = Layout.Read(LayoutTests.Text(objects: objects, extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    static string[] Problems(string extra) =>
        [.. Layout.Read(LayoutTests.Text(extra: "\n" + extra), "test.jsonc").Problems];

    // The direction each heading class (0x437072) takes first, as in WoodcuttingTests: a step that cuts the vector.
    static readonly byte[] FirstDirections = [20, 20, 20, 20, 16, 12, 12, 8, 8, 4, 4, 24];

    // Made-up tables as the hut and carrier tests have them: every step takes 100 clock units, the flag's object class is 3,
    // and the walk's direction rows hold the heading class's own first direction first.
    internal static Game NewGame(Layout layout) => new(MadeTables(), layout.BuildMap());

    // The object class of the building's code (2) is 0 unless `buildingClass` names one: the removal click (0x44b6e6)
    // takes a building for class 4.
    internal static RuleTables MadeTables(byte buildingClass = 0)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        classes[2] = buildingClass;
        var rows = new byte[RuleTables.WalkRowCount * RuleTables.WalkRowSize];
        for (var row = 0; row < RuleTables.WalkRowCount; row++)
        {
            var first = row < 12 ? (byte)4 : FirstDirections[row - 12];
            new[] { first }
                .Concat(new byte[] { 4, 8, 12, 16, 20, 24 }.Where(code => code != first))
                .ToArray()
                .CopyTo(rows, row * RuleTables.WalkRowSize);
        }

        return new RuleTables(
            classes,
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray(),
            walkRows: rows
        );
    }

    static Game Applied(string extra, string objects = "[]")
    {
        var layout = Read(extra, objects);
        var game = NewGame(layout);
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    static string? Refused(string extra)
    {
        var layout = Read(extra);

        return layout.Apply(NewGame(layout));
    }

    static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    [TestMethod]
    public void AWoodcutterPlacedInHisHutIsInTheDoorAndTheCastleHoldsNoOtherWoodcutter()
    {
        var game = Applied(Built + $"\"settlers\": [{Woodcutter}]");

        var worker = game.Settlers.Single(settler => settler.Job == Settler.WoodcutterJob);
        Assert.AreEqual(4, worker.State);
        Assert.AreEqual((13, 10), (worker.X, worker.Y));
        Assert.AreEqual(0x40, game.Buildings[1].Status & 0xc0);
        Assert.AreEqual(game.Settlers.ToList().IndexOf(worker), game.Buildings[1].Worker);
        Assert.AreEqual(0, game.Waiting(Settler.WoodcutterJob));
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadIsTheSettlerTheCastlesWaitingListNamesWhereItNamesOne()
    {
        const int jobless = 21;
        var game = Applied("\"castle\": {\"x\": 10, \"y\": 10},");
        Run(game, 3000);
        foreach (
            var (kind, x, directions) in new[]
            {
                (CommandKind.PlaceFlag, 14, (int[]?)null),
                (CommandKind.BuildRoad, 11, [0, 0, 0]),
            }
        )
        {
            game.Enqueue(new Command(game.TickCount, 0, kind, x, 11, directions));
            game.ApplyCommands();
        }

        var waiting = game.Waiting(jobless);
        var lowest = game.Settlers.ToList().FindIndex(settler => settler.Job == jobless && settler.State == 1);
        Assert.IsTrue(
            waiting > lowest && lowest > 0,
            $"the list names {waiting}, the lowest waiting settler is {lowest}"
        );

        Assert.IsNull(game.PlaceCarrierOnRoad(11, 11, 0));

        Assert.AreEqual(0x42, game.Settlers[waiting].State);
        Assert.AreEqual(1, game.Settlers[lowest].State);
    }

    [TestMethod]
    public void APlacedWoodcutterComesOutAndFellsATreeAndNoSecondWoodcutterLeavesForTheHut()
    {
        var game = Applied(Built + $"\"settlers\": [{Woodcutter}]", Tree);
        var leftTheCastle = false;

        for (var tick = 0; tick < 4000; tick++)
        {
            game.Tick();
            // Mode 255 is a settler sent to a building; the road's own carrier leaves with the mode of its road.
            leftTheCastle |= game.Settlers.Any(settler => settler.State == 0xf && settler.Mode == 0xff);
        }

        Assert.IsFalse(leftTheCastle);
        Assert.IsTrue(Trees.Any(tree => game.Map.At(tree.X, tree.Y).Object.Code != 8));
        Assert.AreEqual(0, game.Buildings[1].Status & 0x80);
        Assert.AreEqual(1, game.Settlers.Count(settler => settler.Job == Settler.WoodcutterJob));
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadWaitsNextToItsFarFlagAndTheRoadCountsItsCarrier()
    {
        var game = Applied(Built + $"\"settlers\": [{Carrier}]");

        var carrier = game.Settlers.Single(settler => settler.Job == Settler.CarrierJob);
        Assert.AreEqual(0x42, carrier.State);
        Assert.AreEqual((13, 11), (carrier.X, carrier.Y));
        Assert.AreEqual((1, 3), (carrier.Target, carrier.Mode));
        Assert.AreEqual(1, game.RoadByte(0, 0) & 0xf);
        Assert.AreEqual(1, game.RoadByte(1, 3) & 0xf);
        Assert.AreEqual(0, game.RoadByte(0, 0) & 0x80);
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadCarriesAGoodFromTheFarFlagIntoTheCastle()
    {
        var game = Applied(
            Built + $"\"settlers\": [{Carrier}], \"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": 7}}]"
        );
        var before = game.CastleLogs;

        Run(game, 20000);

        Assert.AreEqual(before + 1, game.CastleLogs);
    }

    [TestMethod]
    public void AGoodIsPutOnThePlaceOfTheFlagWithNoDestination()
    {
        var game = Applied(Built + "\"goods\": [{\"x\": 14, \"y\": 11, \"place\": 2, \"good\": 7}]");

        Assert.AreEqual(7, game.FlagGoods(1)[2]);
        Assert.AreEqual(0, game.FlagGoods(1)[0]);
        Assert.IsTrue(game.FlagGoodsBit(1));
    }

    [TestMethod]
    public void ASecondWoodcutterTheCastleDoesNotHoldFailsTheLoadNamingTheEntry()
    {
        var problem = Refused(Built + $"\"settlers\": [{Woodcutter}, {Woodcutter}]");

        Assert.IsTrue(problem!.StartsWith("test.jsonc: settlers[1]: "), problem);
    }

    [TestMethod]
    public void ASettlerForAHutOrRoadTheLayoutDoesNotBuildFailsTheLoad()
    {
        var hutless = Built.Replace("\"buildings\": [{\"x\": 13, \"y\": 10}],", "");
        var roadless = Built.Replace("\"roads\": [{\"x\": 11, \"y\": 11, \"directions\": [0, 0, 0]}],", "");

        Assert.IsTrue(Refused(hutless + $"\"settlers\": [{Woodcutter}]")!.StartsWith("test.jsonc: settlers[0]: "));
        Assert.IsTrue(Refused(roadless + $"\"settlers\": [{Carrier}]")!.StartsWith("test.jsonc: settlers[0]: "));
    }

    [TestMethod]
    public void ASecondCarrierForTheSameRoadFailsTheLoad()
    {
        Assert.IsTrue(
            Refused(Built + $"\"settlers\": [{Carrier}, {Carrier}]")!.StartsWith("test.jsonc: settlers[1]: ")
        );
    }

    [TestMethod]
    public void ASettlerInAStateThatIsNoEntryStateOrOfAnotherJobFailsTheRead()
    {
        foreach (
            var entry in new[]
            {
                """{"job": "woodcutter", "state": "felling", "x": 13, "y": 10}""",
                """{"job": "carrier", "state": "in-hut", "x": 11, "y": 11, "direction": 0}""",
                """{"job": "builder", "state": "in-hut", "x": 13, "y": 10}""",
                """{"job": "woodcutter", "state": "in-hut", "x": 13, "y": 10, "direction": 0}""",
                """{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11}""",
                """{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 6}""",
                """{"state": "in-hut", "x": 13, "y": 10}""",
            }
        )
        {
            Assert.IsTrue(
                Problems(Built + $"\"settlers\": [{entry}]")
                    .Any(problem => problem.StartsWith("test.jsonc: settlers[0]: ")),
                entry
            );
        }
    }

    [TestMethod]
    public void AGoodOnAPlaceOutsideTheFlagOnATakenPlaceOrOnNoFlagFailsTheLoad()
    {
        foreach (var place in new[] { 8, -1 })
        {
            Assert.IsTrue(
                Problems(Built + $"\"goods\": [{{\"x\": 14, \"y\": 11, \"place\": {place}, \"good\": 7}}]")
                    .Any(problem => problem.StartsWith("test.jsonc: goods[0]: "))
            );
        }

        var taken =
            Built
            + "\"goods\": [{\"x\": 14, \"y\": 11, \"place\": 3, \"good\": 7}, {\"x\": 14, \"y\": 11, \"place\": 3, \"good\": 9}]";
        var none = Built + "\"goods\": [{\"x\": 20, \"y\": 20, \"place\": 0, \"good\": 7}]";

        Assert.IsTrue(Refused(taken)!.StartsWith("test.jsonc: goods[1]: "));
        Assert.IsTrue(Refused(none)!.StartsWith("test.jsonc: goods[0]: "));
    }

    [TestMethod]
    public void AGoodThatIsNoGoodFailsTheRead()
    {
        foreach (var good in new[] { "0", "26", "1.5", "\"log\"" })
        {
            Assert.IsTrue(
                Problems(Built + $"\"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": {good}}}]")
                    .Any(problem => problem.StartsWith("test.jsonc: goods[0]: "))
            );
        }
    }

    [TestMethod]
    public void TheSameLayoutGivesTheSameDigestOnEveryRun()
    {
        var text =
            Built
            + $"\"settlers\": [{Woodcutter}, {Carrier}], \"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": 7}}]";
        var first = Applied(text, Tree);
        var second = Applied(text, Tree);
        Run(first, 500);
        Run(second, 500);

        Assert.AreEqual(first.Digest(), second.Digest());
    }
}
