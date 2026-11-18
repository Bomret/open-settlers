using System;
using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Application.Testing;

/// <summary>A woodcutter's hut on a <see cref="BehaviourBench"/>, with the hut work built over it.</summary>
public static class HutBench
{
    /// <summary>The column of the hut.</summary>
    public const int HutX = 4;

    /// <summary>The row of the hut.</summary>
    public const int HutY = 4;

    /// <summary>
    /// The hut at (4, 4) with its flag south-east of it at (5, 5), and a tree at (5, 4): the first entry of the spiral around the
    /// hut (an offset of 1 east) that the draw 0 names.
    /// </summary>
    public static (
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
        bench.Map.At(HutX + 1, HutY).Object = new MapObject { Code = 8 };
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
        Action<int, int, Good> layGood = (flagSlot, place, good) =>
            laid.Add((flagSlot, place, TestCatalog.CodeOf(good)));
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
        var parts = new JobParts
        {
            Movement = bench.Movement,
            Buildings = bench.Buildings,
            Flags = bench.Flags,
            History = bench.History,
            Draws = bench.Draws,
            LayGood = layGood,
            Hut = behaviour,
        };

        return (bench, behaviour, laid, new Causes(bench.History, bench.Buildings, walkHome), parts);
    }
}
