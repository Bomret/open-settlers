using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;
using static OpenSettlers.Application.Testing.HutBench;
using static OpenSettlers.Tests.HutRegistry;

namespace OpenSettlers.Tests;

// The woodcutter's work cycle and the map's object pass, built from hand-made tables and a fixed random sequence: the bench tests
// place no castle and tick no game (the tests that tick a game are in the root tests' class of the same name).
[TestClass]
public sealed class HutWorkTests
{
    [TestMethod]
    public void AWoodcutterSearchesCutsAndCarriesALogToTheFlagWithOneDraw()
    {
        var (bench, behaviour, laid, causes, parts) = Hut(0);
        var registry = Registry(bench, behaviour, parts, causes);
        var woodcutter = new Settler(new Woodcutter(), 0, HutX, HutY, 0, new LooksForTree());
        var slot = bench.Place(woodcutter);

        for (var turn = 0; turn < 500 && !(laid.Count > 0 && woodcutter.Activity is Door); turn++)
        {
            bench.Clock += 8;
            registry.Handle(slot, woodcutter);
        }

        Assert.AreEqual(
            ((0, 0, WoodcutterRules.GoodLog), 1, (HutX, HutY), true),
            (laid.Single(), bench.Draws.Taken, (woodcutter.X, woodcutter.Y), woodcutter.Activity is Door)
        );
    }

    [TestMethod]
    public void TheCutLeavesAFallingTreeOfTheSecondVariantOnTheTreesNode()
    {
        var (bench, behaviour, _, causes, parts) = Hut(0);
        var registry = Registry(bench, behaviour, parts, causes);
        var woodcutter = new Settler(new Woodcutter(), 0, HutX, HutY, 0, new LooksForTree());
        var slot = bench.Place(woodcutter);

        for (var turn = 0; turn < 500 && woodcutter.Activity is not WalksFree { Trip.Purpose: BackToHut }; turn++)
        {
            bench.Clock += 8;
            registry.Handle(slot, woodcutter);
        }

        Assert.AreEqual(MapObject.Falling(5, secondVariant: true), bench.Map.At(HutX + 1, HutY).Object);
    }

    [TestMethod]
    public void AModuleRefusesTheArrivalOfASettlerThatIsNotOnAFreeWalkWithAMessage()
    {
        var (_, _, _, _, parts) = Hut();
        var rules = JobModules.Build(parts).Single(module => module.JobType == typeof(Digger));
        var settler = new Settler(new Digger(), 0, HutX, HutY, 0, new SearchesHome());

        var refusal = Assert.ThrowsExactly<InvalidOperationException>(() => rules.Arrive(1, settler));

        StringAssert.Contains(refusal.Message, "free walk");
    }

    [TestMethod]
    public void TheObjectPassTurnsAFallingTreeIntoAStumpWhenItsCursorReachesIt()
    {
        var (bench, behaviour, _, _, _) = Hut();
        // The cursor starts at column 0 of row 0 and moves 23 columns on at each of its nodes.
        bench.Map.At(23, 0).Object = MapObject.Falling(5, secondVariant: false);
        bench.Clock = 8 * 40;

        behaviour.ObjectPass();

        Assert.AreEqual(MapObject.Stump, bench.Map.At(23, 0).Object);
    }
}
