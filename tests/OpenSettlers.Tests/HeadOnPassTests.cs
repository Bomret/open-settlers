using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The pass of two walking settlers that meet head-on (Step in Game.Settlers.cs, 0x43a6c1), reached through commands alone,
// on made-up tables as LayoutSettlersTests has them: every step takes 100 clock units, the flag's object class is 3, and
// the free walk's direction rows hold each heading class's own first direction first. Roads run east from the castle's
// flag at 16,6 to 18,6 and on to 20,6. Taking down the second road sends its carrier home: it walks west along the first
// road. Building the road again at once sends a new carrier east from the castle along the first road. The two meet
// between 18,6 and 19,6: the carrier going west waits on 19,6, and the one going east steps past it.
[TestClass]
public sealed class HeadOnPassTests
{
    private const int East = 0;
    private const int West = 3;

    // The direction each heading class (0x437072) takes first, as in WoodcuttingTests: a step that cuts the vector.
    private static readonly byte[] FirstDirections = [20, 20, 20, 20, 16, 12, 12, 8, 8, 4, 4, 24];

    private static readonly ProgramTables Tables = MadeTables();

    private static ProgramTables MadeTables()
    {
        var classes = new byte[ProgramTables.ClassCount];
        classes[FlagPlacement.FlagCode] = 3;
        var rows = new byte[ProgramTables.WalkRowCount * ProgramTables.WalkRowSize];
        for (var row = 0; row < ProgramTables.WalkRowCount; row++)
        {
            var first = row < 12 ? (byte)4 : FirstDirections[row - 12];
            new[] { first }
                .Concat(new byte[] { 4, 8, 12, 16, 20, 24 }.Where(code => code != first))
                .ToArray()
                .CopyTo(rows, row * ProgramTables.WalkRowSize);
        }

        return new ProgramTables(
            classes,
            Enumerable
                .Range(0, ProgramTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, ProgramTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, ProgramTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, ProgramTables.ScanWindowCount).ToArray(),
            walkRows: rows
        );
    }

    private sealed record Meeting(Game Game, ThingEvent Pass, ThingEvent Wait, int Stepper, int Passer);

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    // The roads run from the castle's flag in `direction` (east unless named): two steps to the second flag, two more
    // to the third. The settler coming home heads the opposite way.
    private static Meeting Meet(int direction = East)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(Tables, map);
        var (dx, dy) = RoadBuilding.Steps[direction];
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 16 + 2 * dx, 6 + 2 * dy);
        Do(game, CommandKind.PlaceFlag, 16 + 4 * dx, 6 + 4 * dy);
        Do(game, CommandKind.BuildRoad, 16, 6, [direction, direction]);
        Do(game, CommandKind.BuildRoad, 16 + 2 * dx, 6 + 2 * dy, [direction, direction]);
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
        }

        Do(game, CommandKind.RemoveRoad, 16 + 3 * dx, 6 + 3 * dy);
        Do(game, CommandKind.BuildRoad, 16 + 2 * dx, 6 + 2 * dy, [direction, direction]);

        ThingEvent? pass = null;
        for (var i = 0; i < 3000 && pass is null; i++)
        {
            game.Tick();
            pass = game.Events.All.FirstOrDefault(e => e.Type == EventType.Stepped && e.Other is not null);
        }

        Assert.IsNotNull(pass, "the two carriers met head-on and passed within 3000 ticks");
        var passer = pass.Thing.Slot;
        var stepper = game.Events.All.First(e => e.Thing.ToString() == pass.Other).Thing.Slot;
        var wait = game.EventsOf(pass.Thing)
            .Last(e => e.Type == EventType.Stepped && e.Tick < pass.Tick && e.State.Get("heading") < 0);

        return new Meeting(game, pass, wait, stepper, passer);
    }

    private static int Height(Game game, int x, int y) =>
        game.Map.Layer(MapLayer.Height)[game.Map.Index(x, y)] & Map.HeightMask;

    private static (int X, int Y) Node(ThingState state) => (state.Get("x")!.Value, state.Get("y")!.Value);

    [TestMethod]
    public void TheTwoSettlersTakeEachOthersNodesWithTheStepsHeadingsShapeAndTime()
    {
        var meeting = Meet();
        var state = meeting.Pass.State;
        var rise = Height(meeting.Game, 19, 6) - Height(meeting.Game, 18, 6);
        var back = 4 + 9 * ((East + 3) % 6) - rise;

        Assert.AreEqual((18, 6), Node(state), "the passer is on the stepper's node");
        Assert.AreEqual(East, state.Get("heading"));
        Assert.AreEqual(back, state.Get("frame"));
        Assert.AreEqual((int)Tables.StepTime(back), state.Get("timer"));

        // The stepper's turn ends with its `walked` event, after the pass.
        var stepper = meeting
            .Game.EventsOf(meeting.Game.SettlerId(meeting.Stepper))
            .Last(e => e.Tick == meeting.Pass.Tick);
        Assert.AreEqual((19, 6), Node(stepper.State), "the stepper is on the passer's node");
        Assert.AreEqual((East + 3) % 6, stepper.State.Get("heading"));
    }

    [TestMethod]
    public void ThePasserGetsOneSteppedEventWithItsOwnCauseNamingTheStepper()
    {
        var meeting = Meet();
        var stepper = meeting.Game.SettlerId(meeting.Stepper);

        var passes = meeting.Game.Events.All.Where(e => e.Type == EventType.Stepped && e.Other is not null).ToList();

        Assert.AreEqual(1, passes.Count);
        Assert.AreEqual(meeting.Game.SettlerId(meeting.Passer), passes[0].Thing);
        Assert.AreEqual(stepper.ToString(), passes[0].Other);
        Assert.AreEqual(meeting.Wait.Cause, passes[0].Cause, "the passer's cause, as on its own wait");
        Assert.AreNotEqual(
            meeting.Game.EventsOf(stepper).Last(e => e.Tick < meeting.Pass.Tick).Cause,
            passes[0].Cause,
            "not the stepper's cause"
        );
    }

    // The stepper is the rebuilt road's new carrier and 19,6 the middle of that road: its next turn takes up its post
    // there (the wait). The passer walks on west towards the castle.
    [TestMethod]
    public void AfterThePassBothSettlersWalkOnWithNeitherBlocked()
    {
        var meeting = Meet();
        var game = meeting.Game;
        var stepper = game.SettlerId(meeting.Stepper);
        var passer = game.Settlers[meeting.Passer];
        bool Posted() => game.EventsOf(stepper).Any(e => e.Tick > meeting.Pass.Tick && e.Type == EventType.Wait);

        for (var i = 0; i < 2000 && (!Posted() || (passer.X, passer.Y) == (18, 6)); i++)
        {
            game.Tick();
        }

        Assert.IsTrue(Posted(), "the stepper took up its post on the passer's old node");
        Assert.AreNotEqual((18, 6), (passer.X, passer.Y), "the passer walked on from the stepper's old node");
        var blocked = game
            .Events.All.Where(e =>
                e.Tick > meeting.Pass.Tick && e.Type == EventType.Stepped && e.State.Get("heading") < 0
            )
            .Where(e => e.Thing.Slot == meeting.Stepper || e.Thing.Slot == meeting.Passer);
        Assert.IsFalse(blocked.Any(), "neither settler was blocked after the pass");
    }

    [TestMethod]
    public void TheSettlerHeadingWestWaitsInsteadOfPassing()
    {
        var meeting = Meet();
        var state = meeting.Wait.State;

        Assert.AreEqual(meeting.Pass.Thing, meeting.Wait.Thing);
        Assert.IsNull(meeting.Wait.Other);
        Assert.AreEqual((19, 6), Node(state));
        Assert.AreEqual(0x7f, state.Get("timer"));
        Assert.AreEqual(0x51 + West, state.Get("frame"));
        Assert.AreEqual(West - 6, state.Get("heading"));
    }

    // The same meeting on a road running south-east (the one coming home heads north-west) or south (it heads north).
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void TheSettlerHeadingNorthWestOrNorthWaitsInsteadOfPassing(int direction)
    {
        var meeting = Meet(direction);
        var (dx, dy) = RoadBuilding.Steps[direction];
        var home = (direction + 3) % 6;
        var state = meeting.Wait.State;

        Assert.AreEqual((16 + 3 * dx, 6 + 3 * dy), Node(state), "the settler heading home waits mid-road");
        Assert.AreEqual(home - 6, state.Get("heading"));
        Assert.AreEqual(direction, meeting.Pass.State.Get("heading"), "the waiting settler passes the other way");
        Assert.AreEqual((16 + 2 * dx, 6 + 2 * dy), Node(meeting.Pass.State));
    }
}
