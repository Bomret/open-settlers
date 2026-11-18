using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events line of a laid game (LaidGame needs the adapters internals); the rest of this class is in src/MapView.Tests.
[TestClass]
public sealed class PlayNamedThingTests
{
    readonly string _out = Path.Combine(Path.GetTempPath(), "play-named-" + Guid.NewGuid().ToString("N"), "stem");

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(Path.GetDirectoryName(_out)))
        {
            Directory.Delete(Path.GetDirectoryName(_out)!, true);
        }
    }

    static PlayState StateOf(Game game) =>
        new()
        {
            CameraX = 0,
            CameraY = 0,
            Zoom = 1f,
            Seed = "1",
            Size = 1,
            Ticks = game.TickCount,
            Logs = 0,
            FlagGoods = new Dictionary<(int X, int Y), int>(),
            Things = Thing.Of(game, null),
            Game = game,
        };

    static PlayScript Parse(string text) => PlayScript.Parse(text, "play/t.play");

    PlayRun Run(string text, Game game)
    {
        var run = new PlayRun(Parse(text), () => StateOf(game), _out, _ => null);
        run.Advance(1.0 / 30);

        return run;
    }

    [TestMethod]
    public void AnEventsLineOfACarrierNamesTheFlagItWaitsAtAsOther()
    {
        var game = LaidGame.Laid();
        var waiting = game.History.Events.All.Single(e =>
            e.Type == EventType.LayoutWaitingOnRoad
            && e.Thing.Kind == ThingId.SettlerKind
            && e.Other is { } other
            && other.StartsWith("flag#", StringComparison.Ordinal)
        );

        var settler = game.Settlers[waiting.Thing.Slot];

        var run = Run($"name c {settler.X} {settler.Y} settler\nevents c carrier", game);
        Assert.AreEqual("", string.Join("\n", run.Problems));
        var text = File.ReadAllText(Path.Combine(_out, "carrier.events.txt"));

        StringAssert.Contains(text, $" other={waiting.Other} ");
    }
}
