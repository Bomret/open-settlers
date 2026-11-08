using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The removal (docs/rules/removal.md) built on a bench: a map, flag table, and road network made by hand, with fake carrier and
// goods seams, so a test sees the removal's rule alone, without a castle or a game.
[TestClass]
public sealed class RemovalBehaviourTests
{
    private const int East = 0;
    private const int West = 3;
    private const int ResourceGood = 14;

    // The carrier behaviour as the removal sees it: it notes what the removal asks of it.
    private sealed class FakeCarriers : IRoadCarriers
    {
        public List<Road> Forgotten { get; } = [];

        public List<(Road Road, Cause Cause)> Caused { get; } = [];

        public void ForgetCause(Road road) => Forgotten.Add(road);

        public void CarryCause(Road road, Cause command) => Caused.Add((road, command));

        public void Claim(int slot, Settler settler) { }
    }

    private sealed class FakePriorities : ITransportPriorities
    {
        public int TransportPriority(Good good) => good.Code;
    }

    private sealed class Rig
    {
        public Rig()
        {
            Bench = new BehaviourBench();
            Bench.RaiseAndOwn();
            // Flag 0 and flag 1 at the ends of a road of four steps east, flag 2 on a node of its own.
            PutFlag(4, 4);
            PutFlag(8, 4);
            PutFlag(4, 8);
            Road = Bench.Join(0, East, 1, West, steps: 4, carrier: false);
            for (var x = 4; x <= 8; x++)
            {
                Bench.Map.At(x, 4).Roads = x switch
                {
                    4 => RoadSet.Of(East),
                    8 => RoadSet.Of(West),
                    _ => RoadSet.Of(East).With(West),
                };
            }

            Removal = new Removal(
                Bench.Movement,
                Bench.Settlers,
                Bench.Buildings,
                Bench.Flags,
                Bench.Roads,
                Bench.History,
                Carriers,
                new FakePriorities(),
                _ => { },
                () => true,
                _ => { }
            );
        }

        public BehaviourBench Bench { get; }

        public FakeCarriers Carriers { get; } = new();

        public Removal Removal { get; }

        public Road Road { get; }

        private void PutFlag(int x, int y)
        {
            var slot = Bench.Flags.Add(x, y);
            ref var node = ref Bench.Map.At(x, y);
            node.Object = MapObject.Flag;
            node.FlagMark = true;
            node.LinkTo(slot);
        }
    }

    [TestMethod]
    public void AFlagThatHoldsGoodsIsTakenDownAndTheGoodsCountLost()
    {
        var rig = new Rig();
        rig.Bench.Map.Resource1Total = 5;
        rig.Bench.Flags[2].Places[3].Good = new Good(ResourceGood);

        Assert.IsTrue(rig.Removal.RemoveFlagAt(4, 8));

        Assert.AreEqual(4, rig.Bench.Map.Resource1Total, "the good counts against the total of resource 1");
        Assert.AreEqual(2, rig.Bench.Flags.Count, "the flag's slot is free and the table's count falls past it");
        Assert.IsFalse(rig.Bench.Map.At(4, 8).FlagMark);
    }

    [TestMethod]
    public void ARemovalOnANodeWithNothingToRemoveIsRefusedAndChangesNothing()
    {
        var rig = new Rig();
        rig.Bench.Map.Resource1Total = 5;
        var events = rig.Bench.History.Events.All.Count();
        var flags = rig.Bench.Flags.Count;
        var node = rig.Bench.Map.At(12, 12);
        var (obj, flagMark) = (node.Object, node.FlagMark);

        Assert.IsFalse(rig.Removal.RemoveFlagAt(12, 12));
        Assert.IsFalse(rig.Removal.RemoveBuildingAt(12, 12));
        Assert.IsFalse(rig.Removal.RemoveRoadAt(12, 12));

        Assert.AreEqual(5, rig.Bench.Map.Resource1Total);
        Assert.AreEqual(events, rig.Bench.History.Events.All.Count());
        Assert.AreEqual(1, rig.Bench.Roads.Roads.Count);
        Assert.AreEqual(flags, rig.Bench.Flags.Count, "no flag slot is freed");
        Assert.AreEqual((obj, flagMark), (rig.Bench.Map.At(12, 12).Object, rig.Bench.Map.At(12, 12).FlagMark));
        Assert.AreEqual(0, rig.Removal.ClickClass(12, 12), "the click is refused");
    }

    [TestMethod]
    public void TheClickOnAFlagWithOneRoadIsRefusedAndTheClickOnAFlagWithNoneOrOnARoadNodeIsNot()
    {
        var rig = new Rig();

        Assert.AreEqual(1, rig.Removal.ClickClass(8, 4), "a flag at the end of a road");
        Assert.AreEqual(2, rig.Removal.ClickClass(4, 8), "a flag that is taken down");
        Assert.AreEqual(4, rig.Removal.ClickClass(6, 4), "a node of a road");
    }

    [TestMethod]
    public void ARoadTakenDownLosesItsBitsAndItsCauseAndEndsAtBothFlags()
    {
        var rig = new Rig();
        rig.Bench.Flags[1].Side(West).HoldsCarrier = true;

        Assert.IsTrue(rig.Removal.RemoveRoadAt(6, 4));

        Assert.AreEqual(0, rig.Bench.Roads.Roads.Count);
        Assert.IsTrue(
            Enumerable.Range(4, 5).All(x => rig.Bench.Map.At(x, 4).Roads.IsEmpty),
            "no node keeps a road bit"
        );
        Assert.IsFalse(rig.Bench.Flags[1].Side(West).HoldsCarrier);
        CollectionAssert.AreEqual(new[] { rig.Road }, rig.Carriers.Forgotten);
    }

    [TestMethod]
    public void AFlagPutOnARoadSplitsItIntoTwoRoadsThatMeetAtTheFlag()
    {
        var rig = new Rig();
        var slot = rig.Bench.Flags.Add(6, 4);
        rig.Bench.Map.At(6, 4).FlagMark = true;
        rig.Bench.Map.At(6, 4).LinkTo(slot);
        var cause = Cause.Razed(rig.Bench.History.FlagId(slot));

        rig.Removal.SplitRoad(rig.Bench.Map.Index(6, 4), slot, cause);

        var halves = rig.Bench.Roads.Roads;
        Assert.AreEqual(2, halves.Count);
        Assert.IsTrue(halves.Any(r => r.FromSlot == 0 && r.ToSlot == slot && r.Directions.Count == 2));
        Assert.IsTrue(halves.Any(r => r.FromSlot == slot && r.ToSlot == 1 && r.Directions.Count == 2));
        CollectionAssert.AreEqual(new[] { rig.Road }, rig.Carriers.Forgotten);
        Assert.AreEqual(
            2,
            rig.Carriers.Caused.Count,
            "each half without a carrier is asked for with the command's cause"
        );
    }
}
