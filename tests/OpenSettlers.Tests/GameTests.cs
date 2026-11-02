using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class GameTests
{
    const string Seed = "1234567812345678";
    const int Ticks = 40;

    static Game Run(params Command[] commands)
    {
        var game = new Game(MapViewState.Open(Seed, 1).Map!);
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
        var commands = new[] { new Command(3, 0, 1, 4, 5), new Command(10, 0, 2, 6, 7) };

        Assert.AreEqual(Run(commands).Digest(), Run(commands).Digest());
    }

    [TestMethod]
    public void ACommandAtAnotherTickGivesAnotherDigest()
    {
        Assert.AreNotEqual(Run(new Command(3, 0, 1, 4, 5)).Digest(), Run(new Command(4, 0, 1, 4, 5)).Digest());
    }

    [TestMethod]
    public void TheOrderOfEnqueueingCommandsOfOneTickDoesNotChangeTheDigest()
    {
        var a = new Command(5, 0, 1, 1, 1);
        var b = new Command(5, 1, 1, 2, 2);

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

        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => game.Enqueue(new Command(0, 0, 1, 1, 1)));
    }

    [TestMethod]
    public void ACommandBeyondTheRunIsNotApplied()
    {
        Assert.AreEqual(Run().Digest(), Run(new Command(Ticks, 0, 1, 1, 1)).Digest());
    }
}
