using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Import.ContentSet;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The carrier behaviour (docs/rules/carriers.md) built on a bench: a flag table and roads made by hand, a castle's roster of
// waiting settlers made by hand, and a walk step that moves nobody, so a test sees the carrier's rule alone.
[TestClass]
public sealed class CarrierBehaviorTests
{
    static readonly Good Log = Good.Log;
    const int East = 0;
    const int West = 3;

    // The castle's list of waiting settlers: a slot for a job.
    sealed class Roster : ICastleRoster
    {
        public Dictionary<Job, int> Waiting { get; } = [];

        public int Sent { get; private set; }

        int ICastleRoster.Waiting(Job job) => Waiting.GetValueOrDefault(job);

        int ICastleRoster.CastleSettler(Job job) => Waiting.GetValueOrDefault(job, -1);

        void ICastleRoster.Take(Job job, int slot) => Waiting.Remove(job);

        void ICastleRoster.WorkerSent() => Sent++;
    }

    sealed class Rig
    {
        public Rig(bool carrier = false, byte[]? capacities = null, int steps = 2, bool water = false)
        {
            Bench = capacities is null
                ? new BehaviourBench()
                : new BehaviourBench(LayoutSettlersTests.MadeTables(roadCapacities: capacities));
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

        public BehaviourBench Bench { get; }

        public Road Road { get; }

        public GoodsTransport Goods { get; }

        public CarrierBehavior Carriers { get; }

        public Roster Roster { get; } = new();

        public ActivityRegistry Registry { get; } = new();

        public List<Good> Stocked { get; } = [];
    }

    [TestMethod]
    public void ARoadWithoutACarrierAsksTheCastleAtItsFlagsTurnAndTheSettlerWithoutAJobIsSentToIt()
    {
        var rig = new Rig();
        var slot = rig.Bench.Settlers.Add(new Settler(new Jobless(), 0, 0, 0, 0, new WaitsInCastle()));
        rig.Roster.Waiting[new Jobless()] = slot;

        rig.Carriers.FlagPass(0);

        var carrier = rig.Bench.Settlers[slot];
        Assert.AreEqual(new Carrier(), carrier.Job);
        Assert.AreEqual(new ToRoad { At = 0, Direction = East }, ((Leaving)carrier.Activity).Errand);
        Assert.IsTrue(rig.Bench.Flags[0].Side(East).CarrierOnTheWay, "the road's near end counts it on its way");
        Assert.IsTrue(rig.Bench.Flags[1].Side(West).CarrierOnTheWay, "and its far end");
        Assert.AreEqual(1, rig.Roster.Sent);
    }

    // A road from flag 0 east to flag 1 with carriers on it, and goods on flag 0 heading east: a busy road at three or more.
    static Rig Busy(int carriers, int goods, byte capacity, bool water = false)
    {
        var rig = new Rig(
            carrier: true,
            capacities: [.. Enumerable.Repeat(capacity, RuleTables.RoadCapacityCount)],
            water: water
        );
        rig.Bench.Flags[0].Side(East).Carriers = carriers;
        rig.Bench.Flags[1].Side(West).Carriers = carriers;
        for (var place = 0; place < goods; place++)
        {
            rig.Goods.LayGood(0, place, Log, destination: 1, way: East);
        }

        var slot = rig.Bench.Settlers.Add(new Settler(new Jobless(), 0, 0, 0, 0, new WaitsInCastle()));
        rig.Roster.Waiting[new Jobless()] = slot;

        return rig;
    }

    [TestMethod]
    public void ABusyRoadWithFewerCarriersThanItsLengthClassHoldsAsksForAnother()
    {
        var rig = Busy(carriers: 1, goods: 3, capacity: 2);

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(1, rig.Roster.Sent, "the castle sends a settler to the busy road");
        Assert.IsTrue(rig.Bench.Flags[0].Side(East).CarrierOnTheWay);
        Assert.IsTrue(rig.Bench.Flags[1].Side(West).CarrierOnTheWay);
        Assert.AreEqual(1, rig.Bench.Flags[0].Side(East).Carriers, "the road counts its carrier on it as before");
        var requests = rig.Bench.History.Events.All.Where(e => e.Type == EventType.CarrierRequested).ToList();
        Assert.AreEqual(3, requests.Count, "the carrier and both flags record the extra request");
        Assert.IsTrue(requests.All(e => e.Cause == Cause.Carriers), "with the carriers as the cause");
    }

    [TestMethod]
    public void ABusyRoadWhoseCarriersEqualItsLengthClassAsksForNone()
    {
        var rig = Busy(carriers: 2, goods: 3, capacity: 2);

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(0, rig.Roster.Sent);
        Assert.IsFalse(rig.Bench.Flags[0].Side(East).CarrierOnTheWay);
    }

    [TestMethod]
    public void ABusyRoadWithMoreCarriersThanItsLengthClassHoldsAsksAgain()
    {
        var rig = Busy(carriers: 2, goods: 3, capacity: 1);

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(1, rig.Roster.Sent, "only a count equal to the length class's stops the request (0x465189)");
    }

    [TestMethod]
    public void ABusyRoadWithACarrierOnTheWayAsksForNoOther()
    {
        var rig = Busy(carriers: 1, goods: 3, capacity: 2);
        rig.Bench.Flags[0].Side(East).CarrierOnTheWay = true;

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(0, rig.Roster.Sent);
    }

    [TestMethod]
    public void ABusyRoadAsksForNoneWhileARequestOfItsFlagFoundNoCarrier()
    {
        var rig = Busy(carriers: 1, goods: 3, capacity: 2);
        rig.Bench.Flags[0].CarrierRequestFailed = true;

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(0, rig.Roster.Sent, "the flag waits for the retry scan, as for a road's first carrier");
    }

    [TestMethod]
    public void ARoadWithTwoGoodsHeadingAlongItAsksForNoneAndShowsItsCarrier()
    {
        var rig = Busy(carriers: 1, goods: 2, capacity: 2);
        rig.Bench.Flags[0].Side(East).HoldsCarrier = false;

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(0, rig.Roster.Sent);
        Assert.IsTrue(rig.Bench.Flags[0].Side(East).HoldsCarrier, "a road that is not busy sets its bit in the status");
    }

    [TestMethod]
    public void ABusyRoadSetsNoBitInTheStatus()
    {
        var rig = Busy(carriers: 2, goods: 3, capacity: 2);
        rig.Bench.Flags[0].Side(East).HoldsCarrier = false;

        rig.Carriers.FlagPass(0);

        Assert.IsFalse(rig.Bench.Flags[0].Side(East).HoldsCarrier);
    }

    [TestMethod]
    public void ACrowdedFlagClearsTheBitOfItsBusyRoadAndANewGoodNoLongerRoutesAlongIt()
    {
        // Flag 1 holds seven goods for the castle's flag (0) to the west: its only road is busy, and the flag is crowded.
        var rig = new Rig(carrier: true, capacities: [.. Enumerable.Repeat((byte)1, RuleTables.RoadCapacityCount)]);
        rig.Bench.Flags[0].Side(East).Carriers = 1;
        rig.Bench.Flags[1].Side(West).Carriers = 1;
        for (var place = 0; place < 7; place++)
        {
            rig.Goods.LayGood(1, place, Log, destination: 0, way: West);
        }

        rig.Carriers.FlagPass(0);

        Assert.IsFalse(rig.Bench.Flags[1].Side(West).HoldsCarrier, "seven goods clear the busy road's bit (0x46520b)");
        Assert.IsTrue(rig.Bench.Flags[0].Side(East).HoldsCarrier, "the far end keeps its own bit");

        rig.Goods.LayGood(1, 7, Log);
        rig.Carriers.FlagPass(0);

        Assert.IsNull(
            rig.Bench.Flags[1].Places[7].Destination,
            "the storage search does not go along the cleared road, so the new good waits"
        );
    }

    [TestMethod]
    public void AFlagWithSixGoodsKeepsTheBitOfItsBusyRoad()
    {
        var rig = Busy(carriers: 1, goods: 6, capacity: 1);

        rig.Carriers.FlagPass(0);

        Assert.IsTrue(rig.Bench.Flags[0].Side(East).HoldsCarrier);
    }

    [TestMethod]
    public void ACrowdedFlagKeepsItsBitsOnATurnThatRoutesNothing()
    {
        var rig = Busy(carriers: 1, goods: 7, capacity: 1);
        rig.Bench.Flags[0].NeedsGoodsTurn = false;

        rig.Carriers.FlagPass(0);

        Assert.IsTrue(
            rig.Bench.Flags[0].Side(East).HoldsCarrier,
            "the count of goods is taken only on a turn that routes them"
        );
    }

    [TestMethod]
    public void ABusyWaterRoadAsksForNoCarrier()
    {
        var rig = Busy(carriers: 1, goods: 3, capacity: 2, water: true);

        rig.Carriers.FlagPass(0);

        Assert.AreEqual(0, rig.Roster.Sent, "the boatman of a water road is not ported");
    }

    [TestMethod]
    public void UnderTheBasicSetABusyRoadsCarriersStopAtItsLengthClassInTheSetsTable()
    {
        var basic = new OpenSettlersRuleTables(Path.Combine(RepositoryRoot(), "content", "basic")).Read();
        var capacities = Enumerable.Range(0, RuleTables.RoadCapacityCount).Select(i => basic.RoadCapacity(i)).ToArray();
        foreach (var (steps, carriers) in new[] { (2, (int)basic.RoadCapacity(0)), (6, basic.RoadCapacity(2) - 1) })
        {
            var rig = new Rig(carrier: true, capacities: capacities, steps: steps);
            rig.Bench.Flags[0].Side(East).Carriers = carriers;
            rig.Bench.Flags[1].Side(West).Carriers = carriers;
            for (var place = 0; place < 3; place++)
            {
                rig.Goods.LayGood(0, place, Log, destination: 1, way: East);
            }

            var slot = rig.Bench.Settlers.Add(new Settler(new Jobless(), 0, 0, 0, 0, new WaitsInCastle()));
            rig.Roster.Waiting[new Jobless()] = slot;

            rig.Carriers.FlagPass(0);

            Assert.AreEqual(
                carriers == basic.RoadCapacity(LengthClass.OfSteps(steps).Value) ? 0 : 1,
                rig.Roster.Sent,
                $"a road of {steps} steps with {carriers} carriers"
            );
        }
    }

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "project.godot")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("no project.godot above the test assembly");
    }

    [TestMethod]
    public void ARoadAskedForACarrierWhenTheCastleHoldsNoneMarksTheFlagToAskAgainAfterTheRetryScan()
    {
        var rig = new Rig();

        rig.Carriers.FlagPass(0);

        Assert.IsTrue(rig.Bench.Flags[0].CarrierRequestFailed);
        Assert.IsFalse(rig.Bench.Flags[0].Side(East).CarrierOnTheWay);
    }

    [TestMethod]
    public void ACarrierSentToARoadThatIsGoneStaysWhereItIsAndTheRoadCountsNoCarrier()
    {
        var rig = new Rig();
        var walking = new Walks
        {
            Errand = new ToRoad { At = 0, Direction = East },
            Heading = new Heading { Value = 0 },
        };
        var settler = new Settler(new Carrier(), 0, 4, 4, 0, walking);
        rig.Bench.Roads.Remove(rig.Road);

        rig.Carriers.EnterRoad(rig.Bench.Settlers.Add(settler), settler);

        Assert.AreSame(walking, settler.Activity);
        Assert.AreEqual(0, rig.Bench.Flags[0].Side(East).Carriers);
    }

    [TestMethod]
    public void ACarrierThatWaitsNextToAFlagTakesTheGoodThatWaitsForItsRoad()
    {
        var rig = new Rig(carrier: true);
        rig.Goods.LayGood(1, 2, Log, destination: 0);
        rig.Goods.Turn(1);
        var approach = Heading.Facing(West).Retried;
        var settler = new Settler(
            new Carrier(),
            0,
            5,
            4,
            0,
            new WaitsAtFlag
            {
                Flag = 1,
                Direction = West,
                Approach = approach,
            }
        );
        var slot = rig.Bench.Settlers.Add(settler);

        rig.Registry.Handle(slot, settler);

        Assert.IsInstanceOfType<WalksRoad>(settler.Activity, "the carrier claims its node and goes on in state 3");
        Assert.AreEqual(slot, rig.Bench.Movement.Occupant(rig.Bench.Map.Index(5, 4)), "it holds the node");
    }

    // A carrier on flag 0, at 4,4, that came along the road from flag 1 (heading west), marked to go: by the fourth empty turn at
    // the end of a busy road (Wait) or by the removal of its road (Removal: the mark with its good dropped).
    static (Rig Rig, Settler Carrier, int Slot) MarkedOnFlag(Load? load)
    {
        var rig = new Rig(carrier: true);
        var map = rig.Bench.Map;
        map.At(4, 4).FlagMark = true;
        map.At(4, 4).LinkTo(0);
        var castle = new Building(BuildingType.Castle, 3, 3, 0) { FillLow = 0xff, FillHigh = 0xff };
        rig.Bench.Buildings.Add(castle);
        map.At(3, 3).LinkTo(0);
        var settler = new Settler(
            new Carrier(),
            0,
            4,
            4,
            0,
            new WalksRoad
            {
                Load = load,
                Heading = Heading.Facing(West),
                Marked = true,
            }
        );
        var slot = rig.Bench.Settlers.Add(settler);
        rig.Bench.Clock = 8;

        return (rig, settler, slot);
    }

    [TestMethod]
    public void ACarrierMarkedToGoWalksToAStorageFromTheFlagItReaches()
    {
        var (rig, settler, slot) = MarkedOnFlag(load: null);

        rig.Registry.Handle(slot, settler);

        var walks = (Walks)settler.Activity;
        Assert.IsInstanceOfType<ToStorage>(walks.Errand, "state 2 with the storage mode 0xfe (0x43aea4)");
        Assert.AreEqual(Heading.Facing(West), walks.Heading, "it keeps its heading");
        Assert.AreEqual(0, settler.Timer);
        Assert.IsTrue(
            rig.Bench.History.Events.All.Any(e => e.Type == EventType.CarrierGoesHome && e.Cause == Cause.Carriers),
            "the history records it with the carriers as the cause"
        );
    }

    [TestMethod]
    public void ACarrierMarkedToGoTakesNoGoodThatWaitsAtTheFlag()
    {
        var (rig, settler, slot) = MarkedOnFlag(load: null);
        rig.Goods.LayGood(0, 2, Log, destination: 1, way: East);
        rig.Goods.WaitForRoad(0, East, 2);

        rig.Registry.Handle(slot, settler);

        Assert.IsInstanceOfType<Walks>(settler.Activity);
        Assert.AreEqual(Log, rig.Bench.Flags[0].Places[2].Good, "the good stays on its place");
        Assert.AreEqual(2, rig.Bench.Flags[0].Side(East).WaitingPlace, "and still waits for the road");
    }

    [TestMethod]
    public void ACarrierMarkedToGoWithAGoodForThisFlagsBuildingDeliversNothing()
    {
        var (rig, settler, slot) = MarkedOnFlag(new Load { Good = Log, Destination = 0 });

        rig.Registry.Handle(slot, settler);

        Assert.IsInstanceOfType<Walks>(settler.Activity, "the mark is read before the delivery: no handover begins");
        Assert.AreEqual(0, rig.Stocked.Count);
    }

    [TestMethod]
    public void ACarrierOnTheFlagItCarriesAGoodToHandsItOverAndTheCastleCountsIt()
    {
        var rig = new Rig(carrier: true);
        var map = rig.Bench.Map;
        map.At(4, 4).FlagMark = true;
        map.At(4, 4).LinkTo(0);
        var castle = new Building(BuildingType.Castle, 3, 3, 0) { FillLow = 0xff, FillHigh = 0xff };
        rig.Bench.Buildings.Add(castle);
        map.At(3, 3).LinkTo(0);
        var load = new Load { Good = Log, Destination = 0 };
        var settler = new Settler(
            new Carrier(),
            0,
            4,
            4,
            0,
            new WalksRoad { Load = load, Heading = Heading.Facing(West) }
        );
        var slot = rig.Bench.Settlers.Add(settler);
        rig.Bench.Clock = 8;

        rig.Registry.Handle(slot, settler);

        Assert.IsInstanceOfType<HandsOver>(
            settler.Activity,
            "the carrier turns to the building and waits out the step"
        );
        Assert.AreEqual(0, rig.Stocked.Count, "nothing is handed over before the step's time has run out");

        rig.Bench.Clock += 200;
        rig.Registry.Handle(slot, settler);

        CollectionAssert.AreEqual(new[] { Log }, rig.Stocked, "the castle counts the log once");
    }
}
