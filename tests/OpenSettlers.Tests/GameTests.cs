using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The map of the map seed 6359,A0D2,B4FA at size 1 has castle sites at 17,0 and 18,0 (emulator: site:1:6359,A0D2,B4FA).
[TestClass]
public sealed class GameTests
{
    const int Ticks = 40;

    static Command Place(long tick, int x, int y, int player = 0) =>
        new(tick, player, CommandKind.PlaceCastle, x, y);

    static Game Run(params Command[] commands)
    {
        var game = new Game(MapGenerator.Generate(new MapSeed(0x6359, 0xA0D2, 0xB4FA), MapSize.Offered[0]));
        foreach (var command in commands)
        {
            game.Enqueue(command);
        }

        for (var i = 0; i < Ticks; i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void TheSameSeedAndCommandsGiveTheSameDigest()
    {
        var commands = new[] { Place(3, 17, 0), Place(10, 18, 0) };

        Assert.AreEqual(Run(commands).Digest(), Run(commands).Digest());
    }

    [TestMethod]
    public void ACommandAtAnotherTickGivesAnotherDigest()
    {
        Assert.AreNotEqual(Run(Place(3, 17, 0)).Digest(), Run(Place(4, 17, 0)).Digest());
    }

    [TestMethod]
    public void TheOrderOfEnqueueingCommandsOfOneTickDoesNotChangeTheDigest()
    {
        var a = Place(5, 17, 0);
        var b = Place(5, 18, 0);

        Assert.AreEqual(Run(a, b).Digest(), Run(b, a).Digest());
    }

    [TestMethod]
    public void EachTickAdvancesTheTickCountByOne()
    {
        Assert.AreEqual(Ticks, Run().TickCount);
    }

    [TestMethod]
    public void ACommandForAPastTickIsRefused()
    {
        var game = Run();

        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => game.Enqueue(Place(0, 17, 0)));
    }

    [TestMethod]
    public void ACommandBeyondTheRunIsNotApplied()
    {
        Assert.AreEqual(Run().Digest(), Run(Place(Ticks, 17, 0)).Digest());
    }
}
