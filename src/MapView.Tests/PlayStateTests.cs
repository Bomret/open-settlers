using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayStateTests
{
    static readonly PlayState State = new()
    {
        CameraX = 120,
        CameraY = 40,
        Zoom = 1.25f,
        Seed = "1234567812345678",
        Size = 2,
    };

    static ExpectStep Expect(string field, string op, string value) =>
        new()
        {
            Line = 7,
            Field = field,
            Op = op,
            Value = value,
        };

    [TestMethod]
    public void TheDumpListsEveryFieldOnItsOwnLine()
    {
        var expected = "camera-x=120\ncamera-y=40\nzoom=1.2500\nseed=1234567812345678\nsize=2\n";

        Assert.AreEqual(expected, State.Dump());
    }

    [TestMethod]
    [DataRow("camera-x", "=", "120")]
    [DataRow("camera-x", "<", "121")]
    [DataRow("camera-x", "<=", "120")]
    [DataRow("camera-y", ">=", "40")]
    [DataRow("camera-y", ">", "39")]
    [DataRow("zoom", "=", "1.25")]
    [DataRow("zoom", ">", "1.2499")]
    [DataRow("seed", "=", "1234567812345678")]
    [DataRow("size", "=", "2")]
    public void AnExpectThatHoldsGivesNoProblem(string field, string op, string value)
    {
        Assert.IsNull(State.Check(Expect(field, op, value)));
    }

    [TestMethod]
    public void AFailedExpectStatesTheExpectationAndTheActualValue()
    {
        var problem = State.Check(Expect("camera-x", ">", "200"));

        Assert.AreEqual("expect camera-x > 200 failed: actual 120", problem);
    }

    static readonly PlayState GameState = State with
    {
        Ticks = 300,
        Logs = 4,
        Planks = 9,
        Stones = 2,
        Food = [1, 2, 3, 4, 5, 6],
    };

    [TestMethod]
    public void AGameViewDumpAddsTheTicksAndTheCastlesGoodsAfterTheMapViewFields()
    {
        var expected =
            "camera-x=120\ncamera-y=40\nzoom=1.2500\nseed=1234567812345678\nsize=2\nticks=300\nlogs=4\nplanks=9\nstones=2\n"
            + "fish=1\npigs=2\nmeat=3\ngrain=4\nflour=5\nbread=6\n";

        Assert.AreEqual(expected, GameState.Dump());
    }

    [TestMethod]
    [DataRow("planks", "=", "9", true)]
    [DataRow("planks", "=", "8", false)]
    [DataRow("planks", ">=", "9", true)]
    [DataRow("planks", "<", "9", false)]
    [DataRow("stones", "=", "2", true)]
    [DataRow("stones", ">", "2", false)]
    [DataRow("stones", "<=", "2", true)]
    public void AGameViewExpectOnPlanksOrStonesComparesWithEveryOperator(
        string field,
        string op,
        string value,
        bool holds
    )
    {
        var problem = GameState.Check(Expect(field, op, value));

        Assert.AreEqual(holds, problem is null, problem);
    }

    [TestMethod]
    [DataRow("fish", 1)]
    [DataRow("pigs", 2)]
    [DataRow("meat", 3)]
    [DataRow("grain", 4)]
    [DataRow("flour", 5)]
    [DataRow("bread", 6)]
    public void AGameViewExpectReadsEachFoodGoodAndNoOther(string field, int count)
    {
        Assert.IsTrue(PlayFields.IsKnown(field));
        Assert.IsNull(GameState.Check(Expect(field, "=", count.ToString())));
        Assert.IsNotNull(GameState.Check(Expect(field, "=", (count + 1).ToString())));
        StringAssert.StartsWith(
            State.Check(Expect(field, "=", "0")),
            $"expect {field}: the map view has no {field}, use --game-view"
        );
    }

    [TestMethod]
    public void AFailedPlanksExpectStatesTheActualCount()
    {
        var problem = GameState.Check(Expect("planks", ">", "20"));

        Assert.AreEqual("expect planks > 20 failed: actual 9", problem);
    }

    [TestMethod]
    public void TheCastlesPlanksAndStonesAreTheGoodsEightAndTenOfItsStock()
    {
        var stock = new Dictionary<Good, int>
        {
            [Good.Log] = 3,
            [Good.Plank] = 53,
            [Good.Stone] = 28,
        };
        var castle = new CastleStock(TestCatalog.Default);
        castle.Place(
            new Castle
            {
                X = 1,
                Y = 1,
                Height = 1,
            },
            stock
        );
        var state = State with
        {
            Ticks = 1,
            Logs = castle.Logs,
            Planks = castle.Planks,
            Stones = castle.Stones,
            Food = castle.FoodCounts,
        };

        StringAssert.EndsWith(
            state.Dump(),
            "ticks=1\nlogs=3\nplanks=53\nstones=28\nfish=0\npigs=0\nmeat=0\ngrain=0\nflour=0\nbread=0\n"
        );
        Assert.IsNull(state.Check(Expect("planks", "=", "53")));
        Assert.IsNull(state.Check(Expect("stones", "=", "28")));
    }

    [TestMethod]
    public void AGameViewDumpEndsItsGameFieldsWithTheGamesDigest()
    {
        var game = GameFactory.Create(
            MadeProgram.Made(),
            MapGenerator.Generate(
                MadeProgram.Made(),
                new MapSeed
                {
                    W0 = 0x6359,
                    W1 = 0xA0D2,
                    W2 = 0xB4FA,
                },
                MapSize.Offered[0]
            )
        );
        game.Enqueue(
            new Command
            {
                Tick = 3,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 17,
                Y = 0,
            }
        );
        game.Tick();
        var dump = (GameState with { Game = game }).Dump();

        StringAssert.EndsWith(dump, $"bread=6\ndigest={game.Digest():x16}\n");
    }

    [TestMethod]
    public void A_digest_expect_compares_the_games_digest_as_sixteen_hex_digits()
    {
        var game = GameFactory.Create(MadeProgram.Made(), new Map(MapSize.Offered[0]));
        var state = GameState with { Game = game };
        var digest = game.Digest().ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsNull(state.Check(Expect("digest", "=", digest)));
        Assert.AreEqual(
            $"expect digest = 0000000000000000 failed: actual {digest}",
            state.Check(Expect("digest", "=", "0000000000000000"))
        );
    }

    [TestMethod]
    public void A_digest_expect_in_the_map_view_is_a_problem()
    {
        Assert.AreEqual(
            "expect digest: the map view has no digest, use --game-view",
            State.Check(Expect("digest", "=", "0000000000000000"))
        );
    }

    static readonly PlayState FlagState = GameState with
    {
        FlagGoods = new System.Collections.Generic.Dictionary<(int X, int Y), int> { [(27, 5)] = 2, [(3, 1)] = 1 },
    };

    [TestMethod]
    public void AGameViewDumpListsTheGoodsOfEachFlagThatHoldsSomeByRowThenColumn()
    {
        var expected =
            "camera-x=120\ncamera-y=40\nzoom=1.2500\nseed=1234567812345678\nsize=2\nticks=300\nlogs=4\nplanks=9\nstones=2\n"
            + "fish=1\npigs=2\nmeat=3\ngrain=4\nflour=5\nbread=6\n"
            + "flag-goods:3,1=1\nflag-goods:27,5=2\n";

        Assert.AreEqual(expected, FlagState.Dump());
    }

    [TestMethod]
    [DataRow("flag-goods:27,5", ">=", "2", true)]
    [DataRow("flag-goods:27,5", "=", "1", false)]
    [DataRow("flag-goods:22,5", "=", "0", true)]
    public void AGameViewExpectOnAFlagsGoodsCountsTheGoodsOnThatNode(string field, string op, string value, bool holds)
    {
        var problem = FlagState.Check(Expect(field, op, value));

        Assert.AreEqual(holds, problem is null, problem);
    }

    [TestMethod]
    [DataRow("ticks", "<", "301", true)]
    [DataRow("ticks", "<", "300", false)]
    [DataRow("ticks", "<=", "300", true)]
    [DataRow("ticks", "<=", "299", false)]
    [DataRow("ticks", "=", "300", true)]
    [DataRow("ticks", "=", "301", false)]
    [DataRow("ticks", ">=", "300", true)]
    [DataRow("ticks", ">=", "301", false)]
    [DataRow("ticks", ">", "299", true)]
    [DataRow("ticks", ">", "300", false)]
    [DataRow("logs", "<", "5", true)]
    [DataRow("logs", "<", "4", false)]
    [DataRow("logs", "<=", "4", true)]
    [DataRow("logs", "<=", "3", false)]
    [DataRow("logs", "=", "4", true)]
    [DataRow("logs", "=", "3", false)]
    [DataRow("logs", ">=", "4", true)]
    [DataRow("logs", ">=", "5", false)]
    [DataRow("logs", ">", "3", true)]
    [DataRow("logs", ">", "4", false)]
    public void AGameViewExpectOnTicksOrLogsComparesWithEveryOperator(string field, string op, string value, bool holds)
    {
        var problem = GameState.Check(Expect(field, op, value));

        Assert.AreEqual(holds, problem is null, problem);
    }

    [TestMethod]
    public void AFailedLogsExpectStatesTheActualCount()
    {
        var problem = GameState.Check(Expect("logs", ">", "9"));

        Assert.AreEqual("expect logs > 9 failed: actual 4", problem);
    }

    [TestMethod]
    [DataRow("ticks")]
    [DataRow("logs")]
    [DataRow("planks")]
    [DataRow("stones")]
    [DataRow("flag-goods:27,5")]
    public void AMapViewExpectOnAGameFieldFails(string field)
    {
        var problem = State.Check(Expect(field, ">=", "0"));

        Assert.IsNotNull(problem);
        StringAssert.Contains(problem, field);
        StringAssert.Contains(problem, "map view");
    }

    [TestMethod]
    public void TheSameStepsOnTheSameSeedAndSizeGiveEqualGameViewDumps()
    {
        static string Dump()
        {
            var game = GameFactory.Create(
                MadeProgram.Made(),
                MapGenerator.Generate(
                    MadeProgram.Made(),
                    new MapSeed
                    {
                        W0 = 0x6359,
                        W1 = 0xA0D2,
                        W2 = 0xB4FA,
                    },
                    MapSize.Offered[0]
                )
            );
            game.Enqueue(
                new Command
                {
                    Tick = 3,
                    Player = 0,
                    Kind = CommandKind.PlaceCastle,
                    X = 17,
                    Y = 0,
                }
            );
            for (var i = 0; i < 400; i++)
            {
                game.Tick();
            }

            return (
                State with
                {
                    Ticks = game.TickCount,
                    Logs = game.CastleStock.Logs,
                    Planks = game.CastleStock.Planks,
                    Stones = game.CastleStock.Stones,
                    Food = game.CastleStock.FoodCounts,
                }
            ).Dump();
        }

        Assert.AreEqual(Dump(), Dump());
    }

    [TestMethod]
    public void AGameWithNoCastleHoldsNoLogs()
    {
        var game = GameFactory.Create(
            MadeProgram.Made(),
            MapGenerator.Generate(
                MadeProgram.Made(),
                new MapSeed
                {
                    W0 = 0x6359,
                    W1 = 0xA0D2,
                    W2 = 0xB4FA,
                },
                MapSize.Offered[0]
            )
        );
        game.Tick();
        var state = State with { Ticks = game.TickCount, Logs = game.CastleStock.Logs };

        Assert.IsNull(state.Check(Expect("logs", "=", "0")));
    }

    [TestMethod]
    public void ZoomComparesAtFourDecimals()
    {
        var zoom = new PlayState
        {
            CameraX = 0,
            CameraY = 0,
            Zoom = 1.00001f,
            Seed = "1",
            Size = 1,
        };

        Assert.IsNull(zoom.Check(Expect("zoom", "=", "1")));
        Assert.IsNotNull(zoom.Check(Expect("zoom", "=", "1.0001")));
    }

    [TestMethod]
    public void AFailedSeedExpectStatesTheActualSeed()
    {
        var problem = State.Check(Expect("seed", "=", "1"));

        Assert.AreEqual("expect seed = 1 failed: actual 1234567812345678", problem);
    }
}
