using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events of the woodcutter's work (ADR 0011) on layouts/woodcutter.jsonc: the woodcutter in his hut at 13,10, trees to the
// east, the hut's flag at 14,11.
[TestClass]
public sealed class WoodcuttingHistoryTests
{
    private static Game Laid()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
            folder = folder.Parent;

        var path = Path.Combine(folder!.FullName, "layouts", "woodcutter.jsonc");
        var read = Layout.Read(File.ReadAllText(path), "woodcutter.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
        var game = LayoutSettlersTests.NewGame(read.Layout!);
        Assert.IsNull(read.Layout!.Apply(game));

        return game;
    }

    private static Settler Woodcutter(Game game) => game.Settlers.Single(s => s.Job == Settler.WoodcutterJob);

    private static void RunTrip(Game game, Action? each = null)
    {
        var laid = false;
        for (var i = 0; i < 4000 && !(laid && Woodcutter(game).State == 4); i++)
        {
            game.Tick();
            each?.Invoke();
            laid |= game.History.Events.All.Any(e => e.Type == EventType.PutGood);
        }
    }

    [TestMethod]
    public void ACutRecordsCutTreeNamingTheTreeByItsNodeWithTheCauseWoodcutting()
    {
        var game = Laid();
        RunTrip(game);

        var cuts = game.History.Events.All.Where(e => e.Type == EventType.CutTree).ToList();
        Assert.IsTrue(cuts.Count > 0);
        Assert.IsTrue(cuts.All(e => e.Cause == Cause.Woodcutting));
        var cut = cuts.First(e => e.State.Get("state") == 0x11 && e.State.Get("heading") == 1);
        Assert.AreEqual(
            ThingId.OfObject("tree", cut.State.Get("x")!.Value, cut.State.Get("y")!.Value).ToString(),
            cut.Other
        );
        Assert.IsTrue(cut.Other!.StartsWith("tree@", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PuttingTheLogDownRecordsAnEventNamingTheHutsFlagById()
    {
        var game = Laid();
        RunTrip(game);

        var flag = game.History.FlagId(game.Flags.ToList().FindIndex(f => (f.X, f.Y) == (14, 11)));
        var put = game.History.Events.All.Where(e => e.Type == EventType.PutGood).ToList();
        Assert.IsFalse(put.Any(e => e.Thing.Kind == ThingId.SettlerKind));
        var onFlag = put.Single(e => e.Thing == flag);
        Assert.AreEqual(GameGoodLog, onFlag.State.Get("good0") & 0x1f);
    }

    private const int GameGoodLog = 7;

    [TestMethod]
    public void ATripRecordsNoMovedEventAndEveryWoodcuttingEventCarriesTheCauseWoodcutting()
    {
        var game = Laid();
        var id = game.History.SettlerId(game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob));
        RunTrip(game);

        // The layout's own setup (the settler entering its hut) comes before the first door turn.
        var events = game.History.EventsOf(id).SkipWhile(e => e.Type != EventType.Door).ToList();
        Assert.IsFalse(events.Any(e => e.Type == "moved"));
        string[] own =
        [
            EventType.Door,
            EventType.SearchTree,
            EventType.StepOut,
            EventType.FreeWalk,
            EventType.Arrive,
            EventType.CutTree,
            EventType.EnterHut,
        ];
        Assert.IsTrue(events.Where(e => own.Contains(e.Type)).All(e => e.Cause == Cause.Woodcutting));
        Assert.IsTrue(events.Any(e => e.Type == EventType.StepOut));
        Assert.IsTrue(events.Any(e => e.Type == EventType.EnterHut));
    }

    [TestMethod]
    public void ATurnRecordsEachRuleOnceForTheWoodcutter()
    {
        var game = Laid();
        var id = game.History.SettlerId(game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob));
        RunTrip(game);

        var twice = game
            .History.EventsOf(id)
            .GroupBy(e => (e.Tick, e.Type))
            .Where(g => g.Count() > 1)
            .Select(g => $"tick {g.Key.Tick} {g.Key.Type} x{g.Count()}")
            .ToList();
        Assert.AreEqual("", string.Join("\n", twice.Take(5)));
    }

    [TestMethod]
    public void ADoorTurnOfAnotherJobRecordsTheSettlersCauseNotWoodcutting()
    {
        var game = Laid();
        var slot = game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob);
        var id = game.History.SettlerId(slot);
        for (var i = 0; i < 4000 && Woodcutter(game).State != 4; i++)
            game.Tick();
        Assert.AreEqual(4, Woodcutter(game).State);
        Woodcutter(game).Job = Settler.CarrierJob;
        var before = game.History.EventsOf(id).Count;
        for (var i = 0; i < 50 && !game.History.EventsOf(id).Skip(before).Any(e => e.Type == EventType.Door); i++)
            game.Tick();

        var door = game.History.EventsOf(id).Skip(before).First(e => e.Type == EventType.Door);
        Assert.AreNotEqual(Cause.Woodcutting, door.Cause);
    }

    [TestMethod]
    public void TheWoodcuttersStateAtEveryTickOfATripEqualsTheEnginesState()
    {
        var game = Laid();
        var id = game.History.SettlerId(game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob));
        var seen = new System.Collections.Generic.HashSet<int>();
        var wrong = new System.Collections.Generic.List<string>();
        RunTrip(
            game,
            () =>
            {
                var s = Woodcutter(game);
                seen.Add(s.State);
                var (state, refusal) = game.History.StateAt(id, game.TickCount - 1);
                var now =
                    $"state={s.State} mode={s.Mode} target={s.Target} next={s.Next} heading={s.Heading} frame={s.Frame} "
                    + $"timer={s.Timer} stamp={s.Stamp} x={s.X} y={s.Y}";
                var then = state is null
                    ? refusal!
                    : string.Join(
                        " ",
                        new[]
                        {
                            "state",
                            "mode",
                            "target",
                            "next",
                            "heading",
                            "frame",
                            "timer",
                            "stamp",
                            "x",
                            "y",
                        }.Select(f => $"{f}={state.Get(f)}")
                    );
                if (now != then)
                {
                    wrong.Add($"tick {game.TickCount - 1}: engine {now} / history {then}");
                }
            }
        );

        Assert.AreEqual("", string.Join("\n", wrong.Take(5)));
        CollectionAssert.IsSubsetOf(new[] { 4, 5, 0x10, 0x11, 0x12 }, seen.ToArray());
    }

    [TestMethod]
    public void AWoodcutterOnTheWalkHomeRecordsItsWorkEventsWithTheCauseWalkHome()
    {
        var game = Laid();
        var slot = game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob);
        var id = game.History.SettlerId(slot);
        for (var i = 0; i < 4000 && Woodcutter(game).State != 0x12; i++)
            game.Tick();

        Assert.AreEqual(0x12, Woodcutter(game).State);
        // No command or removal rule sends a woodcutter home without a carried cause, so the walk-home set is filled directly.
        var walkingHome = (System.Collections.Generic.HashSet<int>)
            typeof(Game)
                .GetField(
                    "_walkingHome",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                )!
                .GetValue(game)!;
        walkingHome.Add(slot);
        var before = game.History.EventsOf(id).Count;
        for (var i = 0; i < 30; i++)
            game.Tick();

        var recorded = game.History.EventsOf(id).Skip(before).ToList();
        Assert.IsNotEmpty(recorded);
        Assert.IsTrue(
            recorded.All(e => e.Cause == Cause.WalkHome),
            string.Join(", ", recorded.Select(e => $"{e.Type}/{e.Cause}"))
        );
    }

    [TestMethod]
    public void AWoodcutterCarryingACommandCauseRecordsItsWorkEventsWithIt()
    {
        var game = Laid();
        var slot = game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob);
        var id = game.History.SettlerId(slot);
        var command = Cause.Placed(game.History.BuildingId(1));
        game.History.CarryCause(slot, command);
        RunTrip(game);

        var work = game
            .History.EventsOf(id)
            .Where(e => e.Type is EventType.SearchTree or EventType.StepOut or EventType.CutTree)
            .ToList();
        Assert.IsNotEmpty(work);
        Assert.IsTrue(work.All(e => e.Cause == command));
    }
}
