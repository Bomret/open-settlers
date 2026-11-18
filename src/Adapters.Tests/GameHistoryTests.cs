using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The exporter that writes the original's raw values into each event's state (INV-0006). The history tests of the same
// class are in the application's test project.
[TestClass]
public sealed class GameHistoryTests
{
    [TestMethod]
    public void TheExporterWritesTheSettlersRawValuesAsNamedFieldsInOrder()
    {
        var settler = new Settler(
            new Woodcutter(),
            0,
            7,
            9,
            0,
            new WaitsAtFlag
            {
                Flag = 300,
                Direction = new Direction { Value = 254 },
                Approach = new Heading { Value = 11 },
                Residue = new Residue { Heading = 3, Next = 15 },
            }
        )
        {
            Frame = 6,
            Timer = 65535,
        };
        var settlers = new SettlerTable { settler };
        var exporter = new SnapshotExporter(settlers, [], [], TestCatalog.Default);

        var state = exporter.Settler(0);

        Assert.AreEqual(
            "job=5 state=66 player=0 mode=254 target=300 next=15 heading=3 frame=6 timer=65535 stamp=11 x=7 y=9",
            state.ToString()
        );
    }

    [TestMethod]
    public void TheExporterWritesAFlagsRoadBytesAndGoodsAfterItsNodeAndStatus()
    {
        var flags = new FlagTable { { 4, 5 } };
        flags[0].RecordCarrierRequest(failed: true);
        flags[0].Side(0).HoldCarrier();
        for (var direction = 0; direction < 6; direction++)
        {
            flags[0].Side(direction).KeepCarriers(direction + 1);
        }

        flags[0].Places[0].Lay(TestCatalog.GoodOf(9), null, null);
        flags[0].Places[7].Lay(TestCatalog.GoodOf(1), null, 0);

        var exporter = new SnapshotExporter([], [], flags, TestCatalog.Default);

        var state = exporter.Flag(0);

        Assert.AreEqual(
            "x=4 y=5 status=129 road0=1 road1=2 road2=3 road3=4 road4=5 road5=6 good0=9 good1=0 good2=0 good3=0 good4=0 good5=0 good6=0 good7=33",
            state.ToString()
        );
    }

    [TestMethod]
    public void TheGameRecordsThroughItsHistory()
    {
        var game = HistoryWorld.NewGame();
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();

        Assert.IsNotEmpty(game.History.EventsOf(game.History.BuildingId(0)));
    }
}
