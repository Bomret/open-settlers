using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayStateTests
{
    static readonly PlayState State = new(CameraX: 120, CameraY: 40, Zoom: 1.25f, Seed: "1234567812345678", Size: 2);

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

    static readonly PlayState GameState = State with { Ticks = 300, Logs = 4 };

    [TestMethod]
    public void AGameViewDumpAddsTheTicksAndLogsAfterTheMapViewFields()
    {
        var expected = "camera-x=120\ncamera-y=40\nzoom=1.2500\nseed=1234567812345678\nsize=2\nticks=300\nlogs=4\n";

        Assert.AreEqual(expected, GameState.Dump());
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
            var game = new Game(
                TestProgram.Made(),
                MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x6359, 0xA0D2, 0xB4FA), MapSize.Offered[0])
            );
            game.Enqueue(new Command(3, 0, CommandKind.PlaceCastle, 17, 0));
            for (var i = 0; i < 400; i++)
            {
                game.Tick();
            }

            return (State with { Ticks = game.TickCount, Logs = game.CastleLogs }).Dump();
        }

        Assert.AreEqual(Dump(), Dump());
    }

    [TestMethod]
    public void AGameWithNoCastleHoldsNoLogs()
    {
        var game = new Game(
            TestProgram.Made(),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x6359, 0xA0D2, 0xB4FA), MapSize.Offered[0])
        );
        game.Tick();
        var state = State with { Ticks = game.TickCount, Logs = game.CastleLogs };

        Assert.IsNull(state.Check(Expect("logs", "=", "0")));
    }

    [TestMethod]
    public void ZoomComparesAtFourDecimals()
    {
        var zoom = new PlayState(0, 0, 1.00001f, "1", 1);

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
