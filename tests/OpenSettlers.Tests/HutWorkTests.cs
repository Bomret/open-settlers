using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The woodcutter's work cycle and the map's object pass, built from hand-made tables and a fixed random sequence: no castle
// is placed and no game ticks.
[TestClass]
public sealed class HutWorkTests
{
    private const int HutX = 4;
    private const int HutY = 4;

    // The hut at (4, 4) with its flag south-east of it at (5, 5), and a tree at (5, 4): the first entry of the spiral around the
    // hut (an offset of 1 east) that the draw 0 names.
    private static (
        BehaviourBench Bench,
        HutWork Behaviour,
        List<(int Flag, int Place, int Good)> Laid,
        Causes Causes,
        JobParts Parts
    ) Hut(params ushort[] draws)
    {
        var bench = new BehaviourBench(draws);
        var flag = bench.Flags.Add(HutX + 1, HutY + 1);
        bench.Buildings.Add(new Building(BuildingType.Woodcutter, HutX, HutY, flag));
        bench.Map.At(HutX, HutY).LinkTo(0);
        bench.Map.At(HutX, HutY).Object = MapObject.Hut;
        bench.Map.At(HutX + 1, HutY + 1).LinkTo(flag);
        bench.Map.At(HutX + 1, HutY).Object = new MapObject(8);
        var laid = new List<(int, int, int)>();
        var walkHome = new WalkHomeBehavior(
            bench.Movement,
            bench.Buildings,
            bench.Flags,
            bench.Roads,
            bench.History,
            bench.Draws,
            (_, _) => -1
        );
        Action<int, int, int> layGood = (flagSlot, place, good) => laid.Add((flagSlot, place, good));
        var behaviour = new HutWork(
            bench.Movement,
            bench.Settlers,
            bench.Flags,
            bench.History,
            bench.Draws,
            walkHome,
            layGood,
            () => false
        );
        var parts = new JobParts(
            bench.Movement,
            bench.Buildings,
            bench.Flags,
            bench.History,
            bench.Draws,
            layGood,
            behaviour.EnterHut,
            behaviour.StepOut,
            behaviour.ArriveHome,
            behaviour
        );

        return (bench, behaviour, laid, new Causes(bench.History, bench.Buildings, walkHome), parts);
    }

    // The registry of the hut work's activities and the modules' (the woodcutter's among them), and the wait of a step that the game's
    // own walk behaviour handles.
    private static ActivityRegistry Registry(BehaviourBench bench, HutWork behaviour, JobParts parts, Causes causes)
    {
        var registry = new ActivityRegistry();
        behaviour.Register(registry);
        foreach (var module in JobModules.Build(parts))
        {
            behaviour.RegisterModule(module);
            module.Register(registry);
        }

        behaviour.RegisterUnruled(new UnruledRules(parts));
        registry.Register<WaitsOutStep>(
            (slot, settler, _) => WalkBehavior.WaitOutStep(bench.Movement, bench.History, causes, slot, settler)
        );

        return registry;
    }

    [TestMethod]
    public void AWoodcutterSearchesCutsAndCarriesALogToTheFlagWithOneDraw()
    {
        var (bench, behaviour, laid, causes, parts) = Hut(0);
        var registry = Registry(bench, behaviour, parts, causes);
        var woodcutter = new Settler(Job.Woodcutter, 0, HutX, HutY, 0, new LooksForTree());
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
        var woodcutter = new Settler(Job.Woodcutter, 0, HutX, HutY, 0, new LooksForTree());
        var slot = bench.Place(woodcutter);

        for (var turn = 0; turn < 500 && woodcutter.Activity is not WalksFree { Trip.Purpose: BackToHut }; turn++)
        {
            bench.Clock += 8;
            registry.Handle(slot, woodcutter);
        }

        Assert.AreEqual(MapObject.Falling(5, secondVariant: true), bench.Map.At(HutX + 1, HutY).Object);
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

    [TestMethod]
    public void TheGameRunsTheObjectPassOncePerPassThroughTheWoodcuttersPassHook()
    {
        var game = StonecuttingTests.World(null, out var map);
        var before = game.Hut.ObjectPassState;
        var tick = game.TickCount;
        map.At(9, 20).Object = MapObject.Falling(5, secondVariant: false);

        for (var pass = 0; pass < 40000 && map.At(9, 20).Object.Kind == MapObjectKind.FallingTree; pass++)
        {
            game.Tick();
        }

        Assert.AreNotEqual(MapObjectKind.FallingTree, map.At(9, 20).Object.Kind, "the pass reached the falling tree");
        Assert.AreNotEqual(before, game.Hut.ObjectPassState);
        Assert.IsTrue(game.TickCount > tick);
    }
}
