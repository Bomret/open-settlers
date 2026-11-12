using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The settlers' shared ground: who stands on which node, the step onto a node, and the timer that counts the clock down.
// The movement is built from a map, the building table, the tables, and a clock, with no game.
[TestClass]
public sealed class SettlerMovementTests
{
    private static ushort _clock;

    private static SettlerMovement Movement(Map map) =>
        new(map, new BuildingTable(), LayoutSettlersTests.MadeTables(), () => _clock, _ => new Residue());

    private static Settler SettlerAt(int x, int y) => new(new Woodcutter(), 0, x, y, 0, new WaitsInCastle());

    [TestMethod]
    public void AMoveFreesTheOldNodeAndTakesTheNewOne()
    {
        var map = new Map(MapSize.Offered[0]);
        var movement = Movement(map);
        var settler = SettlerAt(3, 4);
        movement.Occupy(map.Index(3, 4), 7);

        movement.Move(7, settler, 4, 5, map.Index(4, 5));

        Assert.AreEqual(
            (0, 7, 4, 5),
            (movement.Occupant(map.Index(3, 4)), movement.Occupant(map.Index(4, 5)), settler.X, settler.Y)
        );
    }

    [TestMethod]
    public void TheNeighbourInADirectionWrapsAtTheEdge()
    {
        var map = new Map(MapSize.Offered[0]);

        var (x, y, index) = Movement(map).Neighbour(SettlerAt(31, 31), 1);

        Assert.AreEqual((0, 0, 0), (x, y, index));
    }

    [TestMethod]
    public void TheTimerRunsOutWhenTheElapsedTimeIsMoreThanTheTimeLeft()
    {
        var map = new Map(MapSize.Offered[0]);
        var settler = SettlerAt(0, 0);
        settler.Timer = 10;
        _clock = 11;

        var ran = Movement(map).TimerRan(settler);

        Assert.AreEqual((true, (ushort)0xffff, (ushort)11), (ran, settler.Timer, settler.Stamp));
    }

    [TestMethod]
    public void TheTimerHasNotRunOutWhileTimeIsLeft()
    {
        var map = new Map(MapSize.Offered[0]);
        var settler = SettlerAt(0, 0);
        settler.Timer = 10;
        _clock = 4;

        var ran = Movement(map).TimerRan(settler);

        Assert.AreEqual((false, (ushort)6), (ran, settler.Timer));
    }
}
