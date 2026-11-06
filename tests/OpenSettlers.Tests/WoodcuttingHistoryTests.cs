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
    private static readonly int[] RuleStates = [4, 6, 7, 0x10, 0x11, 0x12];

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
            laid |= game.Events.All.Any(e => e.Type == EventType.PutGood);
        }
    }

    [TestMethod]
    public void ACutRecordsCutTreeNamingTheTreeByItsNodeWithTheCauseWoodcutting()
    {
        var game = Laid();
        RunTrip(game);

        var cuts = game.Events.All.Where(e => e.Type == EventType.CutTree).ToList();
        Assert.IsTrue(cuts.Count > 0);
        Assert.IsTrue(cuts.All(e => e.Cause == Cause.Woodcutting));
        var cut = cuts.First(e => e.State.Get("state") == 0x11 && e.State.Get("heading") == 1);
        Assert.AreEqual(ThingId.OfObject("tree", cut.State.Get("x")!.Value, cut.State.Get("y")!.Value).ToString(), cut.Other);
        Assert.IsTrue(cut.Other!.StartsWith("tree@", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PuttingTheLogDownRecordsAnEventNamingTheHutsFlagById()
    {
        var game = Laid();
        RunTrip(game);

        var flag = game.FlagId(game.Flags.ToList().FindIndex(f => (f.X, f.Y) == (14, 11)));
        var put = game.Events.All.Where(e => e.Type == EventType.PutGood).ToList();
        Assert.IsTrue(put.Any(e => e.Thing.Kind == ThingId.SettlerKind && e.Other == flag.ToString()));
        var onFlag = put.Single(e => e.Thing == flag);
        Assert.AreEqual(GameGoodLog, onFlag.State.Get("good0") & 0x1f);
    }

    private const int GameGoodLog = 7;

    [TestMethod]
    public void TheWoodcuttersStateAtEveryTickOfATripEqualsTheEnginesState()
    {
        var game = Laid();
        var id = game.SettlerId(game.Settlers.ToList().FindIndex(s => s.Job == Settler.WoodcutterJob));
        var seen = new System.Collections.Generic.HashSet<int>();
        var wrong = new System.Collections.Generic.List<string>();
        var stale = false;
        var recorded = game.EventsOf(id).Count;
        RunTrip(
            game,
            () =>
            {
                var s = Woodcutter(game);
                // The generic settler step (state 5, a wait, and its end) records in Game.Settlers.cs; the check starts where
                // a rule of Game.Woodcutting.cs has recorded since the settler left such a state.
                var count = game.EventsOf(id).Count;
                var checkedNow = RuleStates.Contains(s.State) && !(stale && count == recorded);
                stale = !RuleStates.Contains(s.State) || (stale && count == recorded);
                recorded = count;
                if (!checkedNow)
                {
                    return;
                }

                seen.Add(s.State);
                var (state, refusal) = game.StateAt(id, game.TickCount - 1);
                var now = $"state={s.State} mode={s.Mode} target={s.Target} next={s.Next} heading={s.Heading} frame={s.Frame} "
                    + $"timer={s.Timer} stamp={s.Stamp} x={s.X} y={s.Y}";
                var then = state is null ? refusal! : string.Join(" ", new[] { "state", "mode", "target", "next", "heading", "frame", "timer", "stamp", "x", "y" }.Select(f => $"{f}={state.Get(f)}"));
                if (now != then)
                {
                    wrong.Add($"tick {game.TickCount - 1}: engine {now} / history {then}");
                }
            }
        );

        Assert.AreEqual("", string.Join("\n", wrong.Take(5)));
        CollectionAssert.IsSubsetOf(new[] { 4, 0x10, 0x11, 0x12 }, seen.ToArray());
    }
}
