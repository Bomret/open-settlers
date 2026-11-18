using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Application.Testing;

/// <summary>The castle's list of waiting settlers, made by hand: a slot for a job.</summary>
public sealed class CarrierRoster : ICastleRoster
{
    /// <summary>The slot of the settler that waits for each job.</summary>
    public Dictionary<Job, int> Waiting { get; } = [];

    /// <summary>The number of workers the roster sent out.</summary>
    public int Sent { get; private set; }

    int ICastleRoster.Waiting(Job job) => Waiting.GetValueOrDefault(job);

    int ICastleRoster.CastleSettler(Job job) => Waiting.GetValueOrDefault(job, -1);

    void ICastleRoster.Take(Job job, int slot) => Waiting.Remove(job);

    void ICastleRoster.WorkerSent() => Sent++;
}

/// <summary>
/// The carrier behaviour built on a <see cref="BehaviourBench"/>: two flags joined by a road east, the goods transport, a castle's
/// roster made by hand, and a walk step that moves nobody.
/// </summary>
public sealed class CarrierRig
{
    const int East = 0;
    const int West = 3;

    /// <summary>
    /// The rig, its road holding a carrier where <paramref name="carrier"/>, the road capacities of <paramref name="capacities"/> where
    /// given, the road <paramref name="steps"/> long, and a water road where <paramref name="water"/>.
    /// </summary>
    public CarrierRig(bool carrier = false, byte[]? capacities = null, int steps = 2, bool water = false)
    {
        Bench = capacities is null
            ? new BehaviourBench()
            : new BehaviourBench(MadeProgram.MadeTables(roadCapacities: capacities));
        Bench.Flags.Add(4, 4);
        Bench.Flags.Add(6, 4);
        Road = Bench.Join(0, East, 1, West, steps: steps, carrier: carrier);
        if (water)
        {
            Bench.Roads.Remove(Road);
            Road = Road with { Water = true };
            Bench.Roads.Add(Road);
        }

        Goods = new GoodsTransport(Bench.Movement, Bench.Flags, Bench.Roads, Bench.History);
        Carriers = new CarrierBehavior(
            Bench.Movement,
            Bench.Settlers,
            Bench.Buildings,
            Bench.Flags,
            Bench.Roads,
            Bench.History,
            Goods,
            Roster,
            (_, _, _) => false,
            good => Stocked.Add(good),
            () => false
        );
        Carriers.Register(Registry);
    }

    /// <summary>The bench the rig is built on.</summary>
    public BehaviourBench Bench { get; }

    /// <summary>The road from flag 0 east to flag 1.</summary>
    public Road Road { get; }

    /// <summary>The goods transport.</summary>
    public GoodsTransport Goods { get; }

    /// <summary>The carrier behaviour.</summary>
    public CarrierBehavior Carriers { get; }

    /// <summary>The castle's roster.</summary>
    public CarrierRoster Roster { get; } = new();

    /// <summary>The registry the carrier behaviour registered its activities in.</summary>
    public ActivityRegistry Registry { get; } = new();

    /// <summary>The goods the carriers took into storage.</summary>
    public List<Good> Stocked { get; } = [];
}
