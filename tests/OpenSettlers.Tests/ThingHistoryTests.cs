using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Thing IDs, the event store, causes, and the list file's ids (ADR 0011), on the map of the seed 1234,5678,9ABC with the
// castle at 15,5 (its flag at 16,6).
[TestClass]
public sealed class ThingHistoryTests
{
    private static ProgramTables Made()
    {
        var classes = new byte[ProgramTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new ProgramTables(
            classes,
            Enumerable
                .Range(0, ProgramTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, ProgramTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, ProgramTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, ProgramTables.ScanWindowCount).ToArray()
        );
    }

    private static Game NewGame()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var objects = map.Bytes(MapLayer.Object);
        for (var index = 0; index < objects.Length; index++)
        {
            if ((objects[index] & 0x7f) is >= 8 and < 0x18)
            {
                objects[index] &= 0x80;
            }
        }

        return new Game(Made(), map);
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    // The world of GameThingTests: flags at 18,6 (slot 1), 19,4 (2), 17,3 (3), roads between them, a hut at 16,2.
    private static Game World()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            Do(game, CommandKind.PlaceFlag, x, y);
        }

        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);
        Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        for (var i = 0; i < 60; i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void TheDigestOfAFixedGameIsTheOneTakenBeforeThingHistory()
    {
        Assert.AreEqual(15684556580687199582UL,World().Digest());
    }

    private static Game Castle()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);

        return game;
    }

    [TestMethod]
    public void AFlagSlotFilledAgainGivesTheNewFlagTheNextFillCount()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.PlaceFlag, 19, 4);

        Assert.AreEqual("flag#2.1", game.FlagId(2).ToString());

        // The last slot: the list is trimmed at its end, and the count stays.
        Do(game, CommandKind.RemoveFlag, 19, 4);
        Assert.HasCount(2, game.Flags);
        Do(game, CommandKind.PlaceFlag, 19, 4);
        Assert.AreEqual("flag#2.2", game.FlagId(2).ToString());

        // A slot in the middle of the list.
        Do(game, CommandKind.RemoveFlag, 18, 6);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Assert.AreEqual("flag#1.2", game.FlagId(1).ToString());
        Assert.AreEqual("flag#2.2", game.FlagId(2).ToString());
    }

    [TestMethod]
    public void ABuildingSlotFilledAgainAfterTheListWasTrimmedGivesTheNextFillCount()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        Assert.AreEqual("building#0.1", game.BuildingId(0).ToString());
        Assert.AreEqual("building#1.1", game.BuildingId(1).ToString());

        Do(game, CommandKind.RemoveBuilding, 16, 2);
        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            game.Tick();
        }

        Assert.HasCount(1, game.Buildings);
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        Assert.AreEqual("building#1.2", game.BuildingId(1).ToString());
    }

    [TestMethod]
    public void TheCastleSettlersTakeTheFirstFillOfTheirSlots()
    {
        var game = Castle();

        Assert.AreEqual("settler#0.1", game.SettlerId(0).ToString());
        Assert.AreEqual("settler#19.1", game.SettlerId(19).ToString());
    }

    [TestMethod]
    public void PlacingTheCastleRecordsTheEventsOfTheCastleItsFlagAndItsSettlers()
    {
        var game = Castle();
        var castle = game.BuildingId(0);

        var events = game.EventsOf(castle);

        Assert.HasCount(1, events);
        Assert.AreEqual(EventType.PlaceCastle, events[0].Type);
        Assert.AreEqual(0L, events[0].Tick);
        Assert.AreEqual(Cause.Placed(castle), events[0].Cause);
        Assert.AreEqual(game.FlagId(0).ToString(), events[0].Other);
        Assert.AreEqual(15, events[0].State.Get("x"));
        Assert.AreEqual(Cause.Placed(castle), game.EventsOf(game.FlagId(0)).Single().Cause);
        Assert.AreEqual(Cause.Placed(castle), game.EventsOf(game.SettlerId(19)).Single().Cause);
        Assert.AreEqual(22, game.Events.Count);
    }

    [TestMethod]
    public void PlacingAFlagAndABuildingRecordsOneEventEachCausePlacedWithTheThingCommanded()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Do(game, CommandKind.PlaceBuilding, 16, 2);

        var flag = game.EventsOf(game.FlagId(1)).Single();
        var hut = game.EventsOf(game.BuildingId(1)).Single();

        Assert.AreEqual(EventType.PlaceFlag, flag.Type);
        Assert.AreEqual(1L, flag.Tick);
        Assert.AreEqual(Cause.Placed(game.FlagId(1)), flag.Cause);
        Assert.AreEqual(EventType.PlaceBuilding, hut.Type);
        Assert.AreEqual(2L, hut.Tick);
        Assert.AreEqual(Cause.Placed(game.BuildingId(1)), hut.Cause);
        Assert.AreEqual("flag#1.1", hut.Other, "the hut names its flag by the ID the flag has now");
        Assert.AreEqual(2, hut.State.Get("type"));
    }
}
