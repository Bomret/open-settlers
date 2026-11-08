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
    const string Hut = """ "buildings": [{"x": 13, "y": 10}] """;

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
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!;
    }

    // Made tables give every object class 0; a road starts at an object of the flag class (3), so the flag's code is
    // given that class.
    static Game NewGame(Layout layout)
    {
        var tables = TestProgram.Made();
        tables.Classes[MapObject.Flag.Code] = 3;

        return GameFactory.Create(tables, layout.BuildMap());
    }

    static Command At(int kind, int x, int y, params int[] directions) =>
        new(0, 0, kind, x, y, kind == CommandKind.BuildRoad ? directions : null);

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
                At(CommandKind.PlaceBuilding, 13, 10),
            }
        )
        {
            byHand.Enqueue(command);
            byHand.ApplyCommands();
        }

        Assert.IsNull(problem);
        Assert.AreEqual(0, game.TickCount);
        Assert.AreEqual((10, 10), (game.CastleStock.Castle!.Value.X, game.CastleStock.Castle.Value.Y));
        Assert.AreEqual(2, game.Flags.Count);
        Assert.AreEqual(1, game.Network.Roads.Count);
        Assert.AreEqual(2, game.Buildings.Count(building => !building.IsFree));
        Assert.AreEqual(byHand.Digest(), game.Digest());
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
        var huts = """ "buildings": [{"x": 13, "y": 10}, {"x": 13, "y": 10}] """;

        foreach (var (extra, entry) in new[] { (flags + huts, "flags"), (roads + huts, "roads"), (huts, "buildings") })
        {
            var read = Layout.Read(LayoutTests.Text(extra: "\n" + extra), "test.jsonc");

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
        var read = Layout.Read(LayoutTests.Text(extra: """ "roads": [{"x": 1, "y": 2}] """), "test.jsonc");

        Assert.IsTrue(read.Problems.Any(problem => problem.StartsWith("test.jsonc: roads[0]: ")));
    }
}
