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
        tables.Classes[FlagPlacement.FlagCode] = 3;

        return new Game(tables, layout.BuildMap());
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
        Assert.AreEqual((10, 10), (game.Castle!.Value.X, game.Castle.Value.Y));
        Assert.AreEqual(2, game.Flags.Count);
        Assert.AreEqual(1, game.Roads.Count);
        Assert.AreEqual(2, game.Buildings.Count(building => !building.IsFree));
        Assert.AreEqual(byHand.Digest(), game.Digest());
    }

    [TestMethod]
    public void ARoadListedBeforeItsEndFlagFailsAndListedAfterItSucceeds()
    {
        var before = Read(Castle + Road + Flag.TrimEnd(',', ' '));
        var after = Read(Castle + Flag + Road.TrimEnd(',', ' '));

        var problem = before.Apply(NewGame(before));

        Assert.IsTrue(problem!.StartsWith("test.jsonc: roads[0]: "), problem);
        Assert.IsNull(after.Apply(NewGame(after)));
    }

    [TestMethod]
    public void ARefusedEntryFailsTheLoadNamingTheEntryAndItsIndex()
    {
        // The second flag lies on the castle's own flag: the original refuses it.
        var flags = """ "flags": [{"x": 14, "y": 11}, {"x": 11, "y": 11}] """;
        var layout = Read(Castle + flags);

        Assert.IsTrue(layout.Apply(NewGame(layout))!.StartsWith("test.jsonc: flags[1]: "));
    }

    [TestMethod]
    public void ACastleOnAnotherNodeThanASiteFailsTheLoad()
    {
        var layout = Read(""" "castle": {"x": 40, "y": 0} """);
        var game = NewGame(layout);

        Assert.IsTrue(layout.Apply(game)!.StartsWith("test.jsonc: castle: "));
        Assert.IsNull(game.Castle);
    }

    [TestMethod]
    public void ABuildingWithNoFlagStandingFailsTheLoad()
    {
        var layout = Read(Castle + Hut);

        Assert.IsTrue(layout.Apply(NewGame(layout))!.StartsWith("test.jsonc: buildings[0]: "));
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
