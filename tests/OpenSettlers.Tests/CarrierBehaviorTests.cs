using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The carrier behaviour (docs/rules/carriers.md) built on a bench: a flag table and roads made by hand, a castle's roster of
// waiting settlers made by hand, and a walk step that moves nobody, so a test sees the carrier's rule alone.
[TestClass]
public sealed class CarrierBehaviorTests
{
    private const int Log = 7;
    private const int East = 0;
    private const int West = 3;

    // The castle's list of waiting settlers: a slot for a job.
    private sealed class Roster : ICastleRoster
    {
        public Dictionary<Job, int> Waiting { get; } = [];

        public int Sent { get; private set; }

        int ICastleRoster.Waiting(Job job) => Waiting.GetValueOrDefault(job);

        int ICastleRoster.CastleSettler(Job job) => Waiting.GetValueOrDefault(job, -1);

        void ICastleRoster.Take(Job job, int slot) => Waiting.Remove(job);

        void ICastleRoster.WorkerSent() => Sent++;
    }

    private sealed class Rig
    {
        public Rig(bool carrier = false)
        {
            Bench = new BehaviourBench();
            Bench.Flags.Add(4, 4);
            Bench.Flags.Add(6, 4);
            Road = Bench.Join(0, East, 1, West, carrier: carrier);
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
        var slot = rig.Bench.Settlers.Add(new Settler(Job.Jobless, 0, 0, 0, 0, new WaitsInCastle()));
        rig.Roster.Waiting[Job.Jobless] = slot;

        rig.Carriers.FlagPass(0);

        var carrier = rig.Bench.Settlers[slot];
        Assert.AreEqual(Job.Carrier, carrier.Job);
        Assert.AreEqual(new ToRoad(0, East), ((Leaving)carrier.Activity).Errand);
        Assert.IsTrue(rig.Bench.Flags[0].Side(East).CarrierOnTheWay, "the road's near end counts it on its way");
        Assert.IsTrue(rig.Bench.Flags[1].Side(West).CarrierOnTheWay, "and its far end");
        Assert.AreEqual(1, rig.Roster.Sent);
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
        var walking = new Walks(new ToRoad(0, East), new Heading(0));
        var settler = new Settler(Job.Carrier, 0, 4, 4, 0, walking);
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
        var settler = new Settler(Job.Carrier, 0, 5, 4, 0, new WaitsAtFlag(1, West, approach));
        var slot = rig.Bench.Settlers.Add(settler);

        rig.Registry.Handle(slot, settler);

        Assert.IsInstanceOfType<WalksRoad>(settler.Activity, "the carrier claims its node and goes on in state 3");
        Assert.AreEqual(slot, rig.Bench.Movement.Occupant(rig.Bench.Map.Index(5, 4)), "it holds the node");
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
        var load = new Load(new Good(Log), 0);
        var settler = new Settler(Job.Carrier, 0, 4, 4, 0, new WalksRoad(load, Heading.Facing(West)));
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

        CollectionAssert.AreEqual(new[] { new Good(Log) }, rig.Stocked, "the castle counts the log once");
    }
}
