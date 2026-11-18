using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The openness proof (ADR 0020): `ai-token`, a good the original lacks, moves through the game as catalog data only. A carrier
// takes it from a flag over a road into the castle, whose stock counts it by name. The castle is at 15,5, its flag at 16,6, and
// a road leads to a flag at 18,6.
[TestClass]
public sealed class AiTokenTests
{
    static Game NewGame(bool withAiToken)
    {
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;
        var game = GameFactory.Create(
            new RuleTables(
                classes,
                TestProgram.MadeStartStock(),
                [.. Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount)],
                [.. Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount)],
                [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
                goods: withAiToken ? TestCatalog.WithAiToken() : TestCatalog.StandIn()
            ),
            map
        );
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);

        return game;
    }

    static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
            }
        );
        game.Tick();
    }

    // The game with the road built and a token on the far flag, run until the castle holds it. The far flag is flag 1, the castle's flag 0.
    static Game Delivered()
    {
        var game = NewGame(withAiToken: true);
        game.Goods.LayGood(1, 0, TestCatalog.AiToken, destination: 0);
        for (var i = 0; i < 3000 && game.CastleStock.Count(TestCatalog.AiToken) == 0; i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void ATokenLaidOnAFlagEndsInTheCastleStockCountedByItsName()
    {
        var game = Delivered();

        Assert.AreEqual(1, game.CastleStock.Count(new Good { Name = "ai-token" }));
    }

    [TestMethod]
    public void TwoRunsOfTheSameGameGiveTheSameDigest()
    {
        Assert.AreEqual(Delivered().Digest(), Delivered().Digest());
    }

    [TestMethod]
    public void TheDigestDiffersFromTheSameGameWithoutTheGood()
    {
        var with = Delivered();
        var without = NewGame(withAiToken: true);
        while (without.TickCount < with.TickCount)
        {
            without.Tick();
        }

        Assert.AreEqual(1, with.CastleStock.Count(TestCatalog.AiToken), "the token reached the castle");
        Assert.AreNotEqual(without.Digest(), with.Digest());
    }

    // After the delivery no flag and no carrier holds the token, so only the stock's name hash (GameDigest) tells the games apart.
    [TestMethod]
    public void TheDigestCountsTheTokenInTheCastleStock()
    {
        var game = Delivered();
        var before = game.Digest();

        Assert.IsTrue(game.CastleStock.Take(TestCatalog.AiToken), "the token was in the castle");
        Assert.AreNotEqual(before, game.Digest());
    }

    [TestMethod]
    public void TheCastleReadoutNamesTheTokenWithNoRawValue()
    {
        var game = Delivered();

        var text = CastleReadout.Text(
            game.CastleStock.Logs,
            game.CastleStock.Planks,
            game.CastleStock.Stones,
            game.CastleStock.FoodCounts,
            CastleReadout.AddedGoods(game.CastleStock)
        );

        Assert.AreEqual(
            "Logs 6  Planks 0  Stones 7  Fish 0  Pigs 1  Meat 2  Grain 3  Flour 4  Bread 5  ai-token 1",
            text,
            "the name and the count, no raw value"
        );
    }

    [TestMethod]
    public void TheCastleReadoutOfAnOriginalOnlyGameIsUnchanged()
    {
        var game = NewGame(withAiToken: false);

        Assert.IsEmpty(CastleReadout.AddedGoods(game.CastleStock));
    }
}
