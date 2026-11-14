using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The game asks the digest port for its digest: the encoding is the adapter's.
[TestClass]
public sealed class GameDigestPortTests
{
    sealed class FixedDigest(ulong value) : IGameDigest
    {
        public Game? Asked { get; private set; }

        public ulong Of(Game game)
        {
            Asked = game;

            return value;
        }
    }

    [TestMethod]
    public void TheDigestOfTheGameIsWhatTheDigestPortGivesForTheGame()
    {
        var tables = TestProgram.Made();
        var map = MapGenerator.Generate(
            tables,
            new MapSeed
            {
                W0 = 0x6359,
                W1 = 0xA0D2,
                W2 = 0xB4FA,
            },
            MapSize.Offered[0]
        );
        var port = new FixedDigest(42);
        var game = GameFactory.Create(tables, map, digest: port);

        Assert.AreEqual(42UL, game.Digest());
        Assert.AreSame(game, port.Asked);
    }

    [TestMethod]
    public void TheDigestOfTheGameThePortBuildsFollowsTheCommandsApplied()
    {
        var tables = TestProgram.Made();
        Game Make() =>
            GameFactory.Create(
                tables,
                MapGenerator.Generate(
                    tables,
                    new MapSeed
                    {
                        W0 = 0x6359,
                        W1 = 0xA0D2,
                        W2 = 0xB4FA,
                    },
                    MapSize.Offered[0]
                )
            );
        var a = Make();
        var b = Make();
        a.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 17,
                Y = 0,
            }
        );
        b.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 18,
                Y = 0,
            }
        );
        a.Tick();
        b.Tick();

        Assert.AreNotEqual(a.Digest(), b.Digest());
    }
}
