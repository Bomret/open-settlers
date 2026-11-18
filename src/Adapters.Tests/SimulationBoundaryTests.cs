using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The cursor windows of the retry scans of a game built by the adapters (the other boundaries of the simulation's application
// layer are in the tests of the application): each pass runs the scans of the buildings and of the flags. The expected values are
// hand arithmetic from the rules the routines cite: the 0x428728 scan in docs/rules/.
[TestClass]
public sealed class SimulationBoundaryTests
{
    // ----- Cursor windows (F31): 0x428728 clears a window of entries from a cursor, stops after the tenth flagged one -----

    static Game WindowGame(int window)
    {
        var windows = new ushort[RuleTables.ScanWindowCount];
        Array.Fill(windows, (ushort)16);
        windows[MapSize.Offered[0].Value] = (ushort)window;
        var tables = MadeProgram.MadeTables(scanWindows: windows);

        return GameFactory.Create(
            tables,
            MapGenerator.Generate(
                MadeProgram.Made(),
                new MapSeed
                {
                    W0 = 0x1234,
                    W1 = 0x5678,
                    W2 = 0x9ABC,
                },
                MapSize.Offered[0]
            )
        );
    }

    static BuildingTable WithBuildings(Game game, int count, bool failed = false)
    {
        var table = (BuildingTable)game.Buildings;
        for (var i = 0; i < count; i++)
        {
            table.Add(new Building(BuildingType.Woodcutter, 2 + i, 2, 0) { RequestFailed = failed });
        }

        return table;
    }

    [TestMethod]
    public void TheBuildingScanWindowIsClampedAtTheTableEndAndTheNextPassStartsOver()
    {
        var game = WindowGame(4);
        WithBuildings(game, 6);
        var cursors = new int[3];

        for (var pass = 0; pass < cursors.Length; pass++)
        {
            game.Tick();
            cursors[pass] = game.PassScanCursor;
        }

        // 6 entries, window 4: 0 to 4, then min(6 - 4, 4) = 2 entries to 6 (not 8), then the cursor at the count resets.
        CollectionAssert.AreEqual(new[] { 4, 6, 4 }, cursors);
    }

    [TestMethod]
    public void TheBuildingScanCursorPastTheCountResetsToTheTableStart()
    {
        var game = WindowGame(4);
        var table = WithBuildings(game, 6);
        game.Tick();
        game.Tick();
        table.Free(5);
        table.Free(4);

        game.Tick();

        // The cursor, 6, lies past the 4 entries left: the scan starts at 0 and takes the 4 (a cursor not reset would take none).
        Assert.AreEqual(4, game.PassScanCursor);
    }

    [TestMethod]
    public void TheBuildingScanStopsAfterTheTenthFlaggedEntryAndTheNextPassResumesThere()
    {
        var game = WindowGame(16);
        var table = WithBuildings(game, 15, failed: true);

        game.Tick();

        var afterFirst = (game.PassScanCursor, table.Count(building => building.RequestFailed));
        game.Tick();

        // Ten flagged entries (0 to 9) are cleared and the cursor stays on the tenth; the pass after clears the other 5.
        Assert.AreEqual(
            ((9, 5), (15, 0)),
            (afterFirst, (game.PassScanCursor, table.Count(building => building.RequestFailed)))
        );
    }

    static void WithFlags(Game game, int count, bool failed = false)
    {
        for (var i = 0; i < count; i++)
        {
            game.Flags.Add(2 + i, 2);
            game.Flags[i].RecordCarrierRequest(failed);
        }
    }

    [TestMethod]
    public void TheFlagScanWindowIsClampedAtTheTableEndAndTheNextScanStartsOver()
    {
        var game = WindowGame(4);
        WithFlags(game, 6);
        var cursors = new int[3];

        for (var scan = 0; scan < cursors.Length; scan++)
        {
            game.Tick();
            cursors[scan] = game.CarrierFlagCursor;
        }

        CollectionAssert.AreEqual(new[] { 4, 6, 4 }, cursors);
    }

    [TestMethod]
    public void TheFlagScanCursorPastTheCountResetsToTheTableStart()
    {
        var game = WindowGame(4);
        WithFlags(game, 6);
        game.Tick();
        game.Tick();
        game.Flags.Free(5);
        game.Flags.Free(4);

        game.Tick();

        Assert.AreEqual(4, game.CarrierFlagCursor);
    }

    [TestMethod]
    public void TheFlagScanStopsAfterTheTenthFlaggedEntryAndTheNextScanResumesThere()
    {
        var game = WindowGame(16);
        WithFlags(game, 15, failed: true);

        game.Tick();

        var afterFirst = (game.CarrierFlagCursor, game.Flags.Count(flag => flag.CarrierRequestFailed));
        game.Tick();

        Assert.AreEqual(
            ((9, 5), (15, 0)),
            (afterFirst, (game.CarrierFlagCursor, game.Flags.Count(flag => flag.CarrierRequestFailed)))
        );
    }
}
