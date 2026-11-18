using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Applying a layout's castle, flags, roads, and buildings to a game. The map is flat grass: the castle stands at 10,10
// with its flag at 11,11; a flag at 14,11 is three steps east of it, and the hut at 13,10 ties to that flag.
[TestClass]
public sealed class LayoutApplyTests
{
    const string Castle = """ "castle": {"x": 10, "y": 10}, """;
    const string Flag = """ "flags": [{"x": 14, "y": 11}], """;
    const string Road = """ "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}], """;
    const string Hut = """ "buildings": [{"type": "woodcutter-hut", "x": 13, "y": 10}] """;

    // The reasons the game gives for a refused entry (Layout.Refusal).
    const string CastleRefusal = "the node is no castle site, the original refuses it there";
    const string FlagRefusal =
        "the original's flag button does not allow a flag on the node: the castle must stand, and the node must hold no flag, "
        + "no building, and no object that blocks it";
    const string RoadRefusal =
        "a step is not allowed where the original's road drawing allows it, or the last step reaches no flag; list the flag "
        + "at the end before the road";

    static Layout Read(string extra)
    {
        var read = Layout.Read(LayoutText.Text(extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    // Made tables give every object class 0; a road starts at an object of the flag class (3), so the flag's code is
    // given that class.
    static Game NewGame(Layout layout)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var tables = new RuleTables(
            classes,
            MadeProgram.MadeStartStock(),
            catalog: TestCatalog.StandIn(TestCatalog.Buildings(workers: MadeProgram.MadeWorkers(stonecutter: true)))
        );

        return GameFactory.Create(tables, layout.BuildMap());
    }

    static Command At(int kind, int x, int y, params int[] directions) =>
        new()
        {
            Tick = 0,
            Player = 0,
            Kind = kind,
            X = x,
            Y = y,
            Directions = kind == CommandKind.BuildRoad ? directions : null,
        };

    [TestMethod]
    public void ACastleTwoFlagsARoadAndAHutStandAtTickZeroAndGiveTheDigestOfTheSameCommandsOneByOne()
    {
        var layout = Read(Castle + Flag + Road + Hut);
        var game = NewGame(layout);

        var problem = layout.Apply(game);

        var byHand = NewGame(layout);
        foreach (
            var command in new[]
            {
                At(CommandKind.PlaceCastle, 10, 10),
                At(CommandKind.PlaceFlag, 14, 11),
                At(CommandKind.BuildRoad, 11, 11, 0, 0, 0),
            }
        )
        {
            byHand.Enqueue(command);
            byHand.ApplyCommands();
        }

        Assert.IsTrue(byHand.PlaceFinishedBuilding(BuildingType.Woodcutter, 13, 10));
        Assert.IsNull(byHand.Walks.StandWorkerInHut(13, 10), "the hut's entry stands the worker in it");

        Assert.IsNull(problem);
        Assert.AreEqual(0, game.TickCount);
        Assert.AreEqual((10, 10), (game.CastleStock.Castle!.Value.X, game.CastleStock.Castle.Value.Y));
        Assert.AreEqual(2, game.Flags.Count);
        Assert.AreEqual(1, game.Network.Roads.Count);
        Assert.AreEqual(2, game.Buildings.Count(building => !building.IsFree));
        Assert.AreEqual(byHand.Digest(), game.Digest());
    }

    [TestMethod]
    public void AStonecuttersBuildingEntryStandsHisWorkerInsideAsTheWoodcuttersDoes()
    {
        var layout = Read(Castle + Flag + Road + Hut.Replace("woodcutter", "stonecutter"));
        var game = NewGame(layout);

        Assert.IsNull(layout.Apply(game));

        var worker = game.Settlers.Single(settler => settler.Job == new Stonecutter() && settler.Activity is Door);
        Assert.AreEqual((13, 10), (worker.X, worker.Y), "he stands in the doorway of his building");
        Assert.IsTrue(game.Buildings.Single(building => building.Type == BuildingType.Stonecutter).WorkerInside);
    }

    [TestMethod]
    public void AForestersBuildingEntryStandsAJoblessSettlerInsideAsTheForester()
    {
        var layout = Read(Castle + Flag + Road + Hut.Replace("woodcutter", "forester"));
        var game = NewGame(layout);
        var without = NewGame(Read(Castle + Flag + Road));
        Assert.IsNull(Read(Castle + Flag + Road).Apply(without));
        var jobless = without.Settlers.Count(settler => settler.Job == new Jobless());

        Assert.IsNull(layout.Apply(game));

        var worker = game.Settlers.Single(settler => settler.Job == new Forester());
        Assert.IsInstanceOfType<Door>(worker.Activity, "he stands in the doorway of his building");
        Assert.AreEqual((13, 10), (worker.X, worker.Y));
        Assert.IsTrue(game.Buildings.Single(building => building.Type == BuildingType.Forester).WorkerInside);
        Assert.AreEqual(jobless - 1, game.Settlers.Count(settler => settler.Job == new Jobless()));
    }

    // The made tables of the layout tests with the sawmill large, as the original's size table makes it.
    static Game NewGameWithALargeSawmill(Layout layout)
    {
        var sizes = Enumerable.Repeat((byte)1, TestCatalog.BuildingSlots).ToArray();
        foreach (
            var large in new[]
            {
                BuildingType.Sawmill,
                BuildingType.Farm,
                BuildingType.Butcher,
                BuildingType.PigFarm,
                BuildingType.Baker,
            }
        )
        {
            sizes[TestCatalog.CodeOf(large)] = 2;
        }

        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;

        return GameFactory.Create(
            new RuleTables(
                classes,
                MadeProgram.MadeStartStock(),
                catalog: TestCatalog.StandIn(
                    TestCatalog.Buildings(sizes: sizes, workers: MadeProgram.MadeWorkers(stonecutter: true))
                )
            ),
            layout.BuildMap()
        );
    }

    [TestMethod]
    [DataRow("woodcutter-hut")]
    [DataRow("stonecutter-hut")]
    [DataRow("forester-hut")]
    [DataRow("sawmill")]
    [DataRow("fisher-hut")]
    [DataRow("farm")]
    [DataRow("butcher")]
    [DataRow("pig-farm")]
    [DataRow("mill")]
    [DataRow("baker")]
    public void AFinishedBuildingEntryStandsFinishedWithItsFlagAndNoSite(string name)
    {
        var layout = Read(Castle + $$""" "buildings": [{"type": "{{name}}", "x": 13, "y": 10}] """);
        var game = NewGameWithALargeSawmill(layout);

        Assert.IsNull(layout.Apply(game));

        var building = game.Buildings.Single(b => b.Type.Name == name);
        Assert.IsFalse(building.Constructing, "a layout's building stands finished");
        Assert.IsNull(building.Site);
        Assert.AreEqual((14, 11), (game.Flags[building.Flag].X, game.Flags[building.Flag].Y));
    }

    [TestMethod]
    public void AFarmEntryStandsAJoblessSettlerInsideAsTheFarmerInTheSearchForAField()
    {
        var layout = Read(Castle + Flag + Road + Hut.Replace("woodcutter-hut", "farm"));
        var sizes = Enumerable.Repeat((byte)1, TestCatalog.BuildingSlots).ToArray();
        sizes[TestCatalog.CodeOf(BuildingType.Farm)] = 2;
        var workers = MadeProgram.MadeWorkers();
        var scythe = TestCatalog.GoodOf(20);
        workers[TestCatalog.CodeOf(BuildingType.Farm)] = new WorkerRequest { Job = new Farmer(), Tool = scythe };
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        Game Made(Layout of) =>
            GameFactory.Create(
                new RuleTables(
                    classes,
                    MadeProgram.MadeStartStock(),
                    jobData: MadeProgram.MadeJobData(),
                    catalog: TestCatalog.StandIn(TestCatalog.Buildings(sizes: sizes, workers: workers))
                ),
                of.BuildMap()
            );
        var game = Made(layout);
        var without = Made(Read(Castle + Flag + Road));
        Assert.IsNull(Read(Castle + Flag + Road).Apply(without));

        Assert.IsNull(layout.Apply(game));

        Assert.AreEqual(
            without.CastleStock.Count(scythe) - 1,
            game.CastleStock.Count(scythe),
            "the castle gives up the scythe the farmer takes"
        );
        var worker = game.Settlers.Single(settler => settler.Job == new Farmer());
        Assert.IsInstanceOfType<Door>(worker.Activity, "he stands in the doorway of his farm");
        Assert.AreEqual((13, 10), (worker.X, worker.Y));
        Assert.IsTrue(game.Buildings.Single(building => building.Type == BuildingType.Farm).WorkerInside);
    }

    [TestMethod]
    public void ALargeFinishedBuildingTakesTheObjectCode3()
    {
        var layout = Read(Castle + """ "buildings": [{"type": "sawmill", "x": 13, "y": 10}] """);
        var game = NewGameWithALargeSawmill(layout);

        Assert.IsNull(layout.Apply(game));

        Assert.AreEqual(3, game.Map.At(13, 10).Object.Code);
    }

    [TestMethod]
    [DataRow("storehouse")]
    [DataRow("castle")]
    [DataRow("well")]
    [DataRow("woodcutter")]
    [DataRow("fisher")]
    [DataRow("stonecutter")]
    [DataRow("forester")]
    public void AnEntryThatNamesNoPlaceableStableNameIsRefusedAtTheBuildNamingTheNames(string name)
    {
        var entry = $$""" "buildings": [{"type": "{{name}}", "x": 13, "y": 10}] """;
        var layout = Read(Castle + entry);
        var game = NewGameWithALargeSawmill(layout);

        var problem = layout.Apply(game);

        Assert.AreEqual(
            $"test.jsonc: buildings[0]: the building type \"{name}\" is no stable name of a building type of this content set, use "
                + "baker, butcher, farm, fisher-hut, forester-hut, mill, pig-farm, sawmill, stonecutter-hut, woodcutter-hut",
            problem
        );
        Assert.AreEqual(0, game.Buildings.Count(building => !building.IsFree && building.Type != BuildingType.Castle));
    }

    [TestMethod]
    public void AnEntryThatGivesTheTypeAsANumberIsRefusedAtTheBuildNamingTheNames()
    {
        var layout = Read(Castle + """ "buildings": [{"type": 2, "x": 13, "y": 10}] """);

        var problem = layout.Apply(NewGameWithALargeSawmill(layout));

        StringAssert.StartsWith(problem, "test.jsonc: buildings[0]: ");
        StringAssert.Contains(problem, "use baker, butcher");
    }

    [TestMethod]
    public void AnEntryWithNoTypeFailsTheReadNamingTheShape()
    {
        var read = Layout.Read(LayoutText.Text(extra: "\n" + """ "buildings": [{"x": 13, "y": 10}] """), "test.jsonc");

        Assert.IsTrue(
            read.Problems.Any(problem =>
                problem.StartsWith("test.jsonc: buildings[0]: ") && problem.Contains("\"type\"")
            ),
            string.Join("; ", read.Problems)
        );
    }

    [TestMethod]
    public void ABuildingOutsideTheCastlesLandFailsTheLoadNamingTheEntry()
    {
        // 30,30 lies outside the castle's land, where no building site fits. The large type on a small site is refused by
        // BuildingSiteTests.ALargeTypeOnASiteOfASmallBuildingChangesNoState, the same placement.
        var layout = Read(Castle + """ "buildings": [{"type": "sawmill", "x": 30, "y": 30}] """);

        var problem = layout.Apply(NewGameWithALargeSawmill(layout));

        Assert.IsTrue(problem?.StartsWith("test.jsonc: buildings[0]: "), problem);
    }

    [TestMethod]
    public void ARoadListedBeforeItsEndFlagFailsAndListedAfterItSucceeds()
    {
        var before = Read(Castle + Road + Flag.TrimEnd(',', ' '));
        var after = Read(Castle + Flag + Road.TrimEnd(',', ' '));

        var problem = before.Apply(NewGame(before));

        Assert.AreEqual("test.jsonc: roads[0]: " + RoadRefusal, problem);
        Assert.IsNull(after.Apply(NewGame(after)));
    }

    [TestMethod]
    public void ARefusedEntryFailsTheLoadNamingTheEntryAndItsIndex()
    {
        // The second flag lies on the castle's own flag: the original refuses it.
        var flags = """ "flags": [{"x": 14, "y": 11}, {"x": 11, "y": 11}] """;
        var layout = Read(Castle + flags);

        Assert.AreEqual("test.jsonc: flags[1]: " + FlagRefusal, layout.Apply(NewGame(layout)));
    }

    [TestMethod]
    public void ACastleOnAnotherNodeThanASiteFailsTheLoad()
    {
        var layout = Read(""" "castle": {"x": 40, "y": 0} """);
        var game = NewGame(layout);

        Assert.AreEqual("test.jsonc: castle: " + CastleRefusal, layout.Apply(game));
        Assert.IsNull(game.CastleStock.Castle);
    }

    [TestMethod]
    public void ABuildingWithNoFlagStandingPutsItsFlagDown()
    {
        var layout = Read(Castle + Hut);
        var game = NewGame(layout);

        Assert.IsNull(layout.Apply(game));
        Assert.IsTrue(game.Flags.Any(flag => (flag.X, flag.Y) == (14, 11)));
    }

    [TestMethod]
    public void ARoadEqualToAnotherReversedPassesTheReadAndTheGameRefusesItNamingTheEntry()
    {
        // From the far flag back west over the same three steps: the read compares starts and directions, so only the
        // game, whose path is taken, can refuse it.
        var reversed =
            """ "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}, {"x": 14, "y": 11, "directions": [3, 3, 3]}] """;
        var layout = Read(Castle + Flag + reversed);

        Assert.AreEqual("test.jsonc: roads[1]: " + RoadRefusal, layout.Apply(NewGame(layout)));
    }

    [TestMethod]
    public void ARepeatedFlagRoadOrBuildingFailsTheReadNamingBothEntries()
    {
        var flags = """ "flags": [{"x": 14, "y": 11}, {"x": 14, "y": 11}], """;
        var roads =
            """ "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}, {"x": 11, "y": 11, "directions": [0, 0, 0]}], """;
        var huts =
            """ "buildings": [{"type": "woodcutter-hut", "x": 13, "y": 10}, {"type": "woodcutter-hut", "x": 13, "y": 10}] """;

        foreach (var (extra, entry) in new[] { (flags + huts, "flags"), (roads + huts, "roads"), (huts, "buildings") })
        {
            var read = Layout.Read(LayoutText.Text(extra: "\n" + extra), "test.jsonc");

            Assert.IsTrue(
                read.Problems.Any(problem =>
                    problem.StartsWith($"test.jsonc: {entry}[1]: ") && problem.Contains($"{entry}[0]")
                ),
                string.Join("; ", read.Problems)
            );
        }
    }

    [TestMethod]
    public void AMalformedEntryFailsTheReadNamingIt()
    {
        var read = Layout.Read(LayoutText.Text(extra: """ "roads": [{"x": 1, "y": 2}] """), "test.jsonc");

        Assert.IsTrue(read.Problems.Any(problem => problem.StartsWith("test.jsonc: roads[0]: ")));
    }
}
