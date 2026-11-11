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
        "buildings": [{"type": "woodcutter", "x": 13, "y": 10}],
        """;

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
    internal static Game NewGame(Layout layout) => GameFactory.Create(MadeTables(), layout.BuildMap());

    // The object class of the building's code (2) is 0 unless `buildingClass` names one: the removal click (0x44b6e6)
    // takes a building for class 4.
    internal static RuleTables MadeTables(
        byte buildingClass = 0,
        byte[]? transportPriorities = null,
        byte[]? buildingSizes = null,
        byte[]? buildingNeeds = null,
        GoodClass?[]? goodClasses = null,
        DistributionStep[][]? distribution = null,
        byte plankRankBase = 0,
        WorkerRequest?[]? workers = null,
        WorkerRequest? builderRequest = null,
        ushort[]? materialOrder = null,
        ushort[]? blowSteps = null,
        ushort stepTime = 100
    )
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
            Enumerable.Repeat(stepTime, RuleTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray(),
            walkRows: rows,
            transportPriorities: transportPriorities,
            buildingSizes: buildingSizes,
            buildingNeeds: buildingNeeds,
            goodClasses: goodClasses,
            distribution: distribution,
            plankRankBase: plankRankBase,
            workers: workers,
            builderRequest: builderRequest,
            materialOrder: materialOrder,
            blowSteps: blowSteps
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
    public void AFinishedWoodcutterEntryStandsHisWorkerInTheDoorAndTheCastleHoldsNoOtherWoodcutter()
    {
        var game = Applied(Built);

        var worker = game.Settlers.Single(settler => settler.Job == Job.Woodcutter);
        Assert.IsInstanceOfType<Door>(worker.Activity);
        Assert.AreEqual((13, 10), (worker.X, worker.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.AreEqual(game.Settlers.ToList().IndexOf(worker), game.Buildings[1].Worker);
        Assert.AreEqual(0, game.Roster.Waiting(Job.Woodcutter));
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadIsTheSettlerTheCastlesWaitingListNamesWhereItNamesOne()
    {
        var jobless = Job.Jobless;
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

        var waiting = game.Roster.Waiting(jobless);
        var lowest = game
            .Settlers.ToList()
            .FindIndex(settler => settler.Job == jobless && settler.Activity is WaitsInCastle);
        Assert.IsTrue(
            waiting > lowest && lowest > 0,
            $"the list names {waiting}, the lowest waiting settler is {lowest}"
        );

        Assert.IsNull(game.Carriers.PlaceCarrierOnRoad(11, 11, 0));

        Assert.IsInstanceOfType<WaitsAtFlag>(game.Settlers[waiting].Activity);
        Assert.IsInstanceOfType<WaitsInCastle>(game.Settlers[lowest].Activity);
    }

    [TestMethod]
    public void APlacedWoodcutterComesOutAndFellsATreeAndNoSecondWoodcutterLeavesForTheHut()
    {
        var game = Applied(Built, Tree);
        var leftTheCastle = false;

        for (var tick = 0; tick < 4000; tick++)
        {
            game.Tick();
            // A settler sent to a building; the road's own carrier leaves for its road.
            leftTheCastle |= game.Settlers.Any(settler => settler.Activity is Leaving { Errand: ToBuilding });
        }

        Assert.IsFalse(leftTheCastle);
        Assert.IsTrue(Trees.Any(tree => game.Map.At(tree.X, tree.Y).Object.Code != 8));
        Assert.IsFalse(game.Buildings[1].WorkerOnTheWay);
        Assert.AreEqual(1, game.Settlers.Count(settler => settler.Job == Job.Woodcutter));
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadWaitsNextToItsFarFlagAndTheRoadCountsItsCarrier()
    {
        var game = Applied(Built + $"\"settlers\": [{Carrier}]");

        var carrier = game.Settlers.Single(settler => settler.Job == Job.Carrier);
        var waiting = (WaitsAtFlag)carrier.Activity;
        Assert.AreEqual((13, 11), (carrier.X, carrier.Y));
        Assert.AreEqual((1, new Direction(3)), (waiting.Flag, waiting.Direction));
        Assert.AreEqual(1, game.Flags[0].Side(0).Carriers);
        Assert.AreEqual(1, game.Flags[1].Side(3).Carriers);
        Assert.IsFalse(game.Flags[0].Side(0).CarrierOnTheWay);
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadCarriesAGoodFromTheFarFlagIntoTheCastle()
    {
        var game = Applied(
            Built + $"\"settlers\": [{Carrier}], \"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": 7}}]"
        );
        var before = game.CastleStock.Logs;

        Run(game, 20000);

        Assert.AreEqual(before + 1, game.CastleStock.Logs);
    }

    [TestMethod]
    public void AGoodIsPutOnThePlaceOfTheFlagWithNoDestination()
    {
        var game = Applied(Built + "\"goods\": [{\"x\": 14, \"y\": 11, \"place\": 2, \"good\": 7}]");

        Assert.AreEqual(new Good(7), game.Flags[1].Places[2].Good);
        Assert.IsTrue(game.Flags[1].Places[0].IsEmpty);
        Assert.IsTrue(game.Flags[1].NeedsGoodsTurn);
    }

    static string CarrierAt(int x, int direction, int y = 11) =>
        $$"""{"job": "carrier", "state": "waiting-on-road", "x": {{x}}, "y": {{y}}, "direction": {{direction}}}""";

    [TestMethod]
    public void AWoodcutterEntryWhenTheCastleHoldsOnlyOneWoodcutterFailsTheLoadAtTheSecondHut()
    {
        var problem = Refused(
            Built.Replace(
                "\"buildings\": [{\"type\": \"woodcutter\", \"x\": 13, \"y\": 10}]",
                "\"buildings\": [{\"type\": \"woodcutter\", \"x\": 13, \"y\": 10}, {\"type\": \"woodcutter\", \"x\": 17, \"y\": 10}]"
            )
        );

        Assert.AreEqual("test.jsonc: buildings[1]: the castle holds no woodcutter", problem);
    }

    [TestMethod]
    [DataRow(13, 10, DisplayName = "the door")]
    [DataRow(14, 11, DisplayName = "the flag")]
    public void AWoodcutterEntryForAHutWithASettlerOnItsDoorOrFlagFailsTheLoad(int x, int y)
    {
        var game = Applied(Built.Replace("\"buildings\": [{\"type\": \"woodcutter\", \"x\": 13, \"y\": 10}],", ""));
        game.Movement.Occupy(game.Map.Index(x, y), 1);

        var problem = Read("\"buildings\": [{\"type\": \"woodcutter\", \"x\": 13, \"y\": 10}]").Apply(game);

        Assert.AreEqual("test.jsonc: buildings[0]: a settler stands on the hut's door or on its flag", problem);
    }

    [TestMethod]
    [DataRow("stonecutter")]
    [DataRow("forester")]
    [DataRow("sawmill")]
    public void AFinishedEntryOfAnotherTypeStandsWithNoWorker(string type)
    {
        var game = Applied(
            Built.Replace(
                "\"type\": \"woodcutter\", \"x\": 13, \"y\": 10",
                $"\"type\": \"{type}\", \"x\": 13, \"y\": 10"
            )
        );

        Assert.IsFalse(game.Buildings[1].WorkerInside || game.Buildings[1].WorkerOnTheWay);
        Assert.AreEqual(1, game.Settlers.Count(s => s.Job == Job.Woodcutter && s.Activity is WaitsInCastle));
    }

    [TestMethod]
    public void AWorkerForAWoodcuttersSiteIsRefused()
    {
        var game = Applied(Built.Replace("\"buildings\": [{\"type\": \"woodcutter\", \"x\": 13, \"y\": 10}],", ""));
        game.Enqueue(
            new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 13, 10, Building: BuildingType.Woodcutter)
        );
        game.Tick();
        Assert.IsTrue(game.Buildings[1].Constructing);

        Assert.AreEqual("no woodcutter's hut stands on the node", game.Walks.StandWorkerInHut(13, 10));
    }

    [TestMethod]
    public void ACarrierForARoadTheLayoutDoesNotBuildFailsTheLoadNamingTheReason()
    {
        var roadless = Built.Replace("\"roads\": [{\"x\": 11, \"y\": 11, \"directions\": [0, 0, 0]}],", "");

        Assert.AreEqual(
            "test.jsonc: settlers[0]: no land road leaves a flag there in that direction",
            Refused(roadless + $"\"settlers\": [{Carrier}]")
        );
    }

    [TestMethod]
    [DataRow(12, 0, DisplayName = "no flag at the node")]
    [DataRow(11, 1, DisplayName = "no road in that direction")]
    public void ACarrierForAFlagAndDirectionWithNoRoadFailsTheLoad(int x, int direction)
    {
        var problem = Refused(Built + $"\"settlers\": [{CarrierAt(x, direction)}]");

        Assert.AreEqual("test.jsonc: settlers[0]: no land road leaves a flag there in that direction", problem);
    }

    [TestMethod]
    [DataRow(11, 0, DisplayName = "from the castle's end")]
    [DataRow(14, 3, DisplayName = "from the far end")]
    public void ASecondCarrierForTheSameRoadFailsTheLoad(int x, int direction)
    {
        var problem = Refused(Built + $"\"settlers\": [{Carrier}, {CarrierAt(x, direction)}]");

        Assert.AreEqual("test.jsonc: settlers[1]: the road has its carrier already", problem);
    }

    [TestMethod]
    public void ACarrierForARoadNoLandRoadJoinsToTheCastleFailsTheLoad()
    {
        var apart = Built
            .Replace(
                "\"flags\": [{\"x\": 14, \"y\": 11}]",
                "\"flags\": [{\"x\": 14, \"y\": 11}, {\"x\": 13, \"y\": 15}, {\"x\": 15, \"y\": 15}]"
            )
            .Replace(
                "\"roads\": [{\"x\": 11, \"y\": 11, \"directions\": [0, 0, 0]}]",
                "\"roads\": [{\"x\": 11, \"y\": 11, \"directions\": [0, 0, 0]}, {\"x\": 13, \"y\": 15, \"directions\": [0, 0]}]"
            );

        var problem = Refused(apart + $"\"settlers\": [{CarrierAt(13, 0, 15)}]");

        Assert.AreEqual("test.jsonc: settlers[0]: no land road leads from the road to the castle", problem);
    }

    [TestMethod]
    public void ACarrierWhenTheCastleHoldsNoCarrierAndNoJoblessSettlerFailsTheLoad()
    {
        // Six roads joined to the castle's flag, one more than the castle's five jobless settlers: three east along row 11,
        // two south from the castle's flag, and one east along row 15.
        const string sixRoads = """
            "castle": {"x": 10, "y": 10},
            "flags": [{"x": 13, "y": 11}, {"x": 15, "y": 11}, {"x": 17, "y": 11}, {"x": 11, "y": 13}, {"x": 11, "y": 15}, {"x": 13, "y": 15}],
            "roads": [
              {"x": 11, "y": 11, "directions": [0, 0]}, {"x": 13, "y": 11, "directions": [0, 0]}, {"x": 15, "y": 11, "directions": [0, 0]},
              {"x": 11, "y": 11, "directions": [2, 2]}, {"x": 11, "y": 13, "directions": [2, 2]}, {"x": 11, "y": 15, "directions": [0, 0]}
            ],
            """;
        var carriers = string.Join(
            ", ",
            CarrierAt(11, 0),
            CarrierAt(13, 0),
            CarrierAt(15, 0),
            CarrierAt(11, 2),
            CarrierAt(11, 2, 13),
            CarrierAt(11, 0, 15)
        );

        var problem = Refused(sixRoads + $"\"settlers\": [{carriers}]");

        Assert.AreEqual("test.jsonc: settlers[5]: the castle holds no carrier", problem);
    }

    [TestMethod]
    public void ACarrierWhenTheCastleHoldsNoCarrierIsAJoblessSettlerMadeTheRoadsCarrier()
    {
        var before = Applied(Built);
        var jobless = before.Settlers.Count(s => s.Job == Job.Jobless && s.Activity is WaitsInCastle);
        Assert.AreEqual(0, before.Settlers.Count(settler => settler.Job == Job.Carrier));

        var game = Applied(Built + $"\"settlers\": [{Carrier}]");

        var carrier = game.Settlers.ToList().FindIndex(settler => settler.Job == Job.Carrier);
        Assert.IsInstanceOfType<WaitsAtFlag>(game.Settlers[carrier].Activity);
        Assert.AreEqual(jobless - 1, game.Settlers.Count(s => s.Job == Job.Jobless && s.Activity is WaitsInCastle));
        Assert.AreEqual(before.Settlers.Count, game.Settlers.Count);
    }

    [TestMethod]
    public void ASettlerInAStateThatIsNoEntryStateOrOfAnotherJobFailsTheRead()
    {
        foreach (
            var entry in new[]
            {
                """{"job": "woodcutter", "state": "felling", "x": 13, "y": 10}""",
                """{"job": "woodcutter", "state": "in-hut", "x": 13, "y": 10}""",
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
            Built + $"\"settlers\": [{Carrier}], \"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": 7}}]";
        var first = Applied(text, Tree);
        var second = Applied(text, Tree);
        Run(first, 500);
        Run(second, 500);

        Assert.AreEqual(first.Digest(), second.Digest());
    }
}
