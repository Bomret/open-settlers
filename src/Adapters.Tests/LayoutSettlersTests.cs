using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.LayoutGames;
using static OpenSettlers.Domain.Testing.MadeProgram;

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
        "buildings": [{"type": "woodcutter-hut", "x": 13, "y": 10}],
        """;

    const string Carrier = """{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}""";

    // Trees (code 8) on the nodes of the hut's reach that lie east of its flag, on its row or the next two, where the walk goes
    // straight on over open ground.
    static readonly (int X, int Y)[] Trees =
    [
        .. Spiral
            .Offsets.Skip(1)
            .Take(128)
            .Select(o => (13 + o.Dx, 10 + o.Dy))
            .Where(n => n.Item1 >= 16 && n.Item2 is >= 10 and <= 12),
    ];

    static readonly string Tree =
        "[" + string.Join(", ", Trees.Select(n => $"{{\"x\": {n.X}, \"y\": {n.Y}, \"code\": 8}}")) + "]";

    static Layout Read(string extra, string objects = "[]")
    {
        var read = Layout.Read(LayoutText.Text(objects: objects, extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    static string[] Problems(string extra) =>
        [.. Layout.Read(LayoutText.Text(extra: "\n" + extra), "test.jsonc").Problems];

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

    // Whether a layout stands a worker in a finished building of the type: the game's public setup asks for one on a node with no
    // building (0,0), which it refuses where the type's worker stands in the hut and passes over with no answer where he does not.
    static bool StandsWorker(Game game, BuildingType type) => game.StandWorkerInFinishedHut(type, 0, 0) is not null;

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

        var worker = game.Settlers.Single(settler => settler.Job == new Woodcutter());
        Assert.IsInstanceOfType<Door>(worker.Activity);
        Assert.AreEqual((13, 10), (worker.X, worker.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.AreEqual(game.Settlers.ToList().IndexOf(worker), game.Buildings[1].Worker);
        Assert.AreEqual(0, game.CastleWaitingSlot(new Woodcutter()));
    }

    [TestMethod]
    public void AFinishedMillEntryStandsAMillerMadeOfASettlerWithoutAJobInItsDoorAndHeAsksForGrainAtHisNextTurn()
    {
        var layout = Read(Built.Replace("\"woodcutter-hut\"", "\"mill\""));
        var game = GameFactory.Create(
            MadeTables(
                workers: MadeProgram.MadeWorkers(mill: true),
                goodClasses: ConstructionTables.Classes(),
                millRankBase: ConstructionTables.MillRankBase
            ),
            layout.BuildMap()
        );

        Assert.IsNull(layout.Apply(game));

        var miller = game.Settlers.Single(settler => settler.Job == new Miller());
        Assert.IsInstanceOfType<Door>(miller.Activity);
        Assert.AreEqual((13, 10), (miller.X, miller.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.IsTrue(StandsWorker(game, BuildingType.Mill));
        Run(game, 200);
        Assert.AreEqual(new Grinds { Phase = 0 }.Code, miller.Activity.Code, "he waits for grain in the mill");
        Assert.AreEqual(ConstructionTables.GrainClass, game.Flags[1].RequestOf(RequestWord.First).Asks);
        Assert.AreEqual(ConstructionTables.MillRankBase, game.Flags[1].RequestOf(RequestWord.First).Rank);
    }

    [TestMethod]
    public void AFinishedFisherEntryStandsHisWorkerInTheDoorAndHeGoesOnToLookForAShore()
    {
        var game = Applied(Built.Replace("woodcutter-hut", "fisher-hut"));

        var worker = game.Settlers.Single(settler => settler.Job == new Fisher() && settler.Activity is Door);
        Assert.AreEqual((13, 10), (worker.X, worker.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.AreEqual(game.Settlers.ToList().IndexOf(worker), game.Buildings[1].Worker);

        Run(game, 200);

        Assert.IsInstanceOfType<LooksForWater>(worker.Activity);
    }

    [TestMethod]
    public void AFinishedBakerEntryStandsABakerMadeOfASettlerWithoutAJobInItsDoorAndHeAsksForFlourAtHisNextTurn()
    {
        var layout = Read(Built.Replace("\"woodcutter-hut\"", "\"baker\""));
        var game = GameFactory.Create(
            MadeTables(workers: MadeProgram.MadeWorkers(baker: true), goodClasses: ConstructionTables.Classes()),
            layout.BuildMap()
        );

        Assert.IsNull(layout.Apply(game));

        var baker = game.Settlers.Single(settler => settler.Job == new Baker());
        Assert.IsInstanceOfType<Door>(baker.Activity);
        Assert.AreEqual((13, 10), (baker.X, baker.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.IsTrue(StandsWorker(game, BuildingType.Baker));
        Run(game, 200);
        Assert.AreEqual(new Bakes { Phase = 0 }.Code, baker.Activity.Code, "he waits for flour in the bakery");
        Assert.AreEqual(ConstructionTables.FlourClass, game.Flags[1].RequestOf(RequestWord.First).Asks);
        Assert.AreEqual(0xff, game.Flags[1].RequestOf(RequestWord.First).Rank);
    }

    [TestMethod]
    public void AFinishedButcherEntryStandsAButcherMadeOfASettlerWithoutAJobAndTheKnifeFromTheCastlesStock()
    {
        var knife = TestCatalog.GoodOf(19);
        var workers = MadeProgram.MadeWorkers();
        workers[TestCatalog.CodeOf(BuildingType.Butcher)] = new WorkerRequest { Job = new Butcher(), Tool = knife };
        var layout = Read(Built.Replace("\"woodcutter-hut\"", "\"butcher\""));
        var game = GameFactory.Create(
            MadeTables(workers: workers, goodClasses: ConstructionTables.Classes()),
            layout.BuildMap()
        );

        var control = GameFactory.Create(
            MadeTables(workers: MadeProgram.MadeWorkers(), goodClasses: ConstructionTables.Classes()),
            layout.BuildMap()
        );
        Assert.IsNull(layout.Apply(control));

        Assert.IsNull(layout.Apply(game));
        Assert.AreEqual(
            control.CastleStock.Count(knife) - 1,
            game.CastleStock.Count(knife),
            "the castle gave up one knife for the butcher"
        );

        var butcher = game.Settlers.Single(settler => settler.Job == new Butcher());
        Assert.IsInstanceOfType<Door>(butcher.Activity);
        Assert.AreEqual((13, 10), (butcher.X, butcher.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.IsTrue(StandsWorker(game, BuildingType.Butcher));
        Run(game, 200);
        Assert.AreEqual(new Butchers { Working = false }.Code, butcher.Activity.Code, "he waits for a pig in the shop");
        Assert.AreEqual(ConstructionTables.PigClass, game.Flags[1].RequestOf(RequestWord.First).Asks);
        Assert.AreEqual(0xff, game.Flags[1].RequestOf(RequestWord.First).Rank);
    }

    [TestMethod]
    public void AFinishedPigFarmEntryStandsAPigFarmerMadeOfASettlerWithoutAJobInItsDoorAndHeAsksForGrainAtHisNextTurn()
    {
        var layout = Read(Built.Replace("\"woodcutter-hut\"", "\"pig-farm\""));
        var game = GameFactory.Create(
            MadeTables(
                workers: MadeProgram.MadeWorkers(pigFarm: true),
                goodClasses: ConstructionTables.Classes(),
                jobData: ConstructionTables.PigData()
            ),
            layout.BuildMap()
        );

        Assert.IsNull(layout.Apply(game));

        var farmer = game.Settlers.Single(settler => settler.Job == new PigFarmer());
        Assert.IsInstanceOfType<Door>(farmer.Activity);
        Assert.AreEqual((13, 10), (farmer.X, farmer.Y));
        Assert.IsTrue(game.Buildings[1].WorkerInside && !game.Buildings[1].WorkerOnTheWay);
        Assert.IsTrue(StandsWorker(game, BuildingType.PigFarm));
        Run(game, 200);
        Assert.AreEqual(new TendsPigs { Phase = 0 }.Code, farmer.Activity.Code, "he waits for grain in the pig farm");
        Assert.AreEqual(1, game.Buildings[1].FillHigh, "the farm holds one pig");
        Assert.AreEqual(ConstructionTables.GrainClass, game.Flags[1].RequestOf(RequestWord.First).Asks);
        Assert.AreEqual(ConstructionTables.PigRankBase, game.Flags[1].RequestOf(RequestWord.First).Rank);
    }

    [TestMethod]
    public void ALayoutStandsAWorkerOnlyInTheHutOfAJobWhoseRulesLetItsSettlerStandInTheHut()
    {
        var game = Applied(Built);

        CollectionAssert.AreEqual(
            new[] { true, true, true, true, false, false, false },
            new[]
            {
                BuildingType.Woodcutter,
                BuildingType.Stonecutter,
                BuildingType.Forester,
                BuildingType.Fisher,
                BuildingType.Sawmill,
                BuildingType.Castle,
                BuildingType.Storehouse,
            }
                .Select(type => StandsWorker(game, type))
                .ToArray()
        );
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadIsTheSettlerTheCastlesWaitingListNamesWhereItNamesOne()
    {
        var jobless = new Jobless();
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
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = kind,
                    X = x,
                    Y = 11,
                    Directions = directions,
                }
            );
            game.ApplyCommands();
        }

        var waiting = game.CastleWaitingSlot(jobless);
        var lowest = game
            .Settlers.ToList()
            .FindIndex(settler => settler.Job == jobless && settler.Activity is WaitsInCastle);
        Assert.IsTrue(
            waiting > lowest && lowest > 0,
            $"the list names {waiting}, the lowest waiting settler is {lowest}"
        );

        Assert.IsNull(game.PlaceCarrierOnRoad(11, 11, 0));

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
        Assert.AreEqual(1, game.Settlers.Count(settler => settler.Job == new Woodcutter()));
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadWaitsNextToItsFarFlagAndTheRoadCountsItsCarrier()
    {
        var game = Applied(Built + $"\"settlers\": [{Carrier}]");

        var carrier = game.Settlers.Single(settler => settler.Job == new Carrier());
        var waiting = (WaitsAtFlag)carrier.Activity;
        Assert.AreEqual((13, 11), (carrier.X, carrier.Y));
        Assert.AreEqual((1, new Direction { Value = 3 }), (waiting.Flag, waiting.Direction));
        Assert.AreEqual(1, game.Flags[0].Side(0).Carriers);
        Assert.AreEqual(1, game.Flags[1].Side(3).Carriers);
        Assert.IsFalse(game.Flags[0].Side(0).CarrierOnTheWay);
    }

    [TestMethod]
    public void ACarrierPlacedOnARoadCarriesAGoodFromTheFarFlagIntoTheCastle()
    {
        var game = Applied(
            Built + $"\"settlers\": [{Carrier}], \"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": \"log\"}}]"
        );
        var before = game.CastleStock.Logs;

        Run(game, 20000);

        Assert.AreEqual(before + 1, game.CastleStock.Logs);
    }

    [TestMethod]
    public void AGoodIsPutOnThePlaceOfTheFlagWithNoDestination()
    {
        var game = Applied(Built + "\"goods\": [{\"x\": 14, \"y\": 11, \"place\": 2, \"good\": \"log\"}]");

        Assert.AreEqual(Good.Log, game.Flags[1].Places[2].Good);
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
                "\"buildings\": [{\"type\": \"woodcutter-hut\", \"x\": 13, \"y\": 10}]",
                "\"buildings\": [{\"type\": \"woodcutter-hut\", \"x\": 13, \"y\": 10}, {\"type\": \"woodcutter-hut\", \"x\": 17, \"y\": 10}]"
            )
        );

        Assert.AreEqual("test.jsonc: buildings[1]: the castle holds no woodcutter for the building", problem);
    }

    // A woodcutter entry for a hut with a settler on its door or flag is in the class of the same name in src/Application.Tests: it
    // stands the settler there through the settlers' movement, which is internal to the application.

    [TestMethod]
    [DataRow("sawmill")]
    public void AFinishedEntryOfAnotherTypeStandsWithNoWorker(string type)
    {
        var game = Applied(
            Built.Replace(
                "\"type\": \"woodcutter-hut\", \"x\": 13, \"y\": 10",
                $"\"type\": \"{type}\", \"x\": 13, \"y\": 10"
            )
        );

        Assert.IsFalse(game.Buildings[1].WorkerInside || game.Buildings[1].WorkerOnTheWay);
        Assert.AreEqual(1, game.Settlers.Count(s => s.Job == new Woodcutter() && s.Activity is WaitsInCastle));
    }

    [TestMethod]
    [DataRow("sawmill", 13, 10, DisplayName = "a finished sawmill")]
    [DataRow("woodcutter-hut", 10, 10, DisplayName = "the castle")]
    public void AWorkerForAFinishedBuildingOfAJobWhoseSettlerDoesNotStandInTheHutIsRefusedNamingTheJobsThatDo(
        string type,
        int x,
        int y
    )
    {
        var game = Applied(
            Built.Replace(
                "\"type\": \"woodcutter-hut\", \"x\": 13, \"y\": 10",
                $"\"type\": \"{type}\", \"x\": 13, \"y\": 10"
            )
        );

        Assert.AreEqual(
            "no finished woodcutter's, stonecutter's, pig farmer's, miller's, forester's, fisher's, farmer's, butcher's, or baker's building stands on the node",
            game.StandWorkerInFinishedHut(BuildingType.Woodcutter, x, y)
        );
    }

    [TestMethod]
    public void AWorkerForAWoodcuttersSiteIsRefused()
    {
        var game = Applied(Built.Replace("\"buildings\": [{\"type\": \"woodcutter-hut\", \"x\": 13, \"y\": 10}],", ""));
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 13,
                Y = 10,
                Building = BuildingType.Woodcutter,
            }
        );
        game.Tick();
        Assert.IsTrue(game.Buildings[1].Constructing);

        Assert.AreEqual(
            "no finished woodcutter's, stonecutter's, pig farmer's, miller's, forester's, fisher's, farmer's, butcher's, or baker's building stands on the node",
            game.StandWorkerInFinishedHut(BuildingType.Woodcutter, 13, 10)
        );
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
        const string SixRoads = """
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

        var problem = Refused(SixRoads + $"\"settlers\": [{carriers}]");

        Assert.AreEqual("test.jsonc: settlers[5]: the castle holds no carrier", problem);
    }

    [TestMethod]
    public void ACarrierWhenTheCastleHoldsNoCarrierIsAJoblessSettlerMadeTheRoadsCarrier()
    {
        var before = Applied(Built);
        var jobless = before.Settlers.Count(s => s.Job == new Jobless() && s.Activity is WaitsInCastle);
        Assert.AreEqual(0, before.Settlers.Count(settler => settler.Job == new Carrier()));

        var game = Applied(Built + $"\"settlers\": [{Carrier}]");

        var carrier = game.Settlers.ToList().FindIndex(settler => settler.Job == new Carrier());
        Assert.IsInstanceOfType<WaitsAtFlag>(game.Settlers[carrier].Activity);
        Assert.AreEqual(jobless - 1, game.Settlers.Count(s => s.Job == new Jobless() && s.Activity is WaitsInCastle));
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
                Problems(Built + $"\"goods\": [{{\"x\": 14, \"y\": 11, \"place\": {place}, \"good\": \"log\"}}]")
                    .Any(problem => problem.StartsWith("test.jsonc: goods[0]: "))
            );
        }

        var taken =
            Built
            + "\"goods\": [{\"x\": 14, \"y\": 11, \"place\": 3, \"good\": \"log\"}, {\"x\": 14, \"y\": 11, \"place\": 3, \"good\": \"boat\"}]";
        var none = Built + "\"goods\": [{\"x\": 20, \"y\": 20, \"place\": 0, \"good\": \"log\"}]";

        Assert.IsTrue(Refused(taken)!.StartsWith("test.jsonc: goods[1]: "));
        Assert.IsTrue(Refused(none)!.StartsWith("test.jsonc: goods[0]: "));
    }

    [TestMethod]
    public void AGoodGivenByNumberOrByAnUnknownNameIsRefusedWithTheNamesTheContentSetKnows()
    {
        foreach (var good in new[] { "4", "0", "26", "1.5", "\"unobtainium\"", "\"\"", "\"Log\"", "null" })
        {
            var refusal = Refused(Built + $"\"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": {good}}}]");

            Assert.IsNotNull(refusal, good);
            StringAssert.StartsWith(refusal, "test.jsonc: goods[0]: ", good);
            StringAssert.Contains(refusal, $"the good {good} is no stable name", good);
            StringAssert.Contains(refusal, "use boat, bread, butcher-knife, coal,", good);
            StringAssert.EndsWith(refusal, "shield, shovel, stone, sword", good);
        }
    }

    [TestMethod]
    public void EveryOriginalGoodLoadsByItsStableNameTheShieldIncluded()
    {
        foreach (var name in TestCatalog.Names)
        {
            var game = Applied(Built + $"\"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": \"{name}\"}}]");

            Assert.AreEqual(name, game.Flags[1].Places[0].Good.Name, name);
        }
    }

    [TestMethod]
    public void TheSameLayoutGivesTheSameDigestOnEveryRun()
    {
        var text =
            Built
            + $"\"settlers\": [{Carrier}], \"goods\": [{{\"x\": 14, \"y\": 11, \"place\": 0, \"good\": \"log\"}}]";
        var first = Applied(text, Tree);
        var second = Applied(text, Tree);
        Run(first, 500);
        Run(second, 500);

        Assert.AreEqual(first.Digest(), second.Digest());
    }
}
