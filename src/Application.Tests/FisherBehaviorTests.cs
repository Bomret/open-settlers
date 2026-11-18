using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The fisher's work cycle (docs/rules/fishing.md) on a bench of made-up tables and a fixed random sequence: where he finds a node by the
// water, what a cast draws and takes from the lake detail, and what he lays on the flag. The emulator's values for the whole cycle are
// FisherTests'; these check our own logic and need no install.
[TestClass]
public sealed class FisherBehaviorTests
{
    const int HutX = 4;
    const int HutY = 4;
    const int EastCast = 0x84;
    const int WestCast = 0x83;

    // The node a draw leads to from the building: the search takes the spiral entries 1 to 64.
    static (int X, int Y) NodeOf(int draw)
    {
        var (dx, dy) = Spiral.Offsets[((draw & 0xfc) + 4) >> 2];

        return (HutX + dx, HutY + dy);
    }

    // The bench of HutWorkTests with every node land and no tree; the building's flag is at (5, 5).
    static (
        BehaviourBench Bench,
        JobParts Parts,
        ActivityRegistry Registry,
        List<(int Flag, int Place, int Good)> Laid
    ) Land(params ushort[] draws)
    {
        var (bench, behaviour, laid, causes, parts) = HutBench.Hut(draws);
        for (var index = 0; index < bench.Map.Count; index++)
        {
            bench.Map.At(index).Upper = Terrain.Lowland;
            bench.Map.At(index).Lower = Terrain.Lowland;
        }

        bench.Map.At(HutX + 1, HutY).Object = MapObject.None;

        return (bench, parts, HutRegistry.Registry(bench, behaviour, parts, causes), laid);
    }

    // The lower triangle of the node is water: with a land triangle above its north-west neighbour the east cast fits the node.
    static void EastShore(BehaviourBench bench, int x, int y) => bench.Map.At(x, y).Lower = new Terrain { Code = 0 };

    // The lower triangle of the node's west neighbour is water: with a land triangle above its north neighbour the west cast fits.
    static void WestShore(BehaviourBench bench, int x, int y) =>
        bench.Map.At(x - 1, y).Lower = new Terrain { Code = 0 };

    static (Settler Settler, int Slot) Place(
        BehaviourBench bench,
        Activity activity,
        int x = HutX,
        int y = HutY,
        int frame = 0
    )
    {
        var settler = new Settler(new Fisher(), 0, x, y, 0, activity) { Frame = frame };

        return (settler, bench.Place(settler));
    }

    static Activity Walk(Purpose purpose) =>
        new WalksFree
        {
            Trip = new Trip { Remaining = new Vector(0, 0), Purpose = purpose },
        };

    static JobRules RulesOf(JobParts parts) =>
        JobModules.Build(parts).Single(module => module.JobType == typeof(Fisher));

    static void TurnsWhile<TActivity>(BehaviourBench bench, ActivityRegistry registry, int slot, Settler settler)
        where TActivity : Activity
    {
        for (var turn = 0; turn < 3000 && settler.Activity is TActivity; turn++)
        {
            bench.Clock += 8;
            registry.Handle(slot, settler);
        }
    }

    [TestMethod]
    public void AFisherTakesTheDrawnNodeThatIsAShoreAndStepsOutWithTheVectorToIt()
    {
        var (bench, _, registry, _) = Land(0);
        var (x, y) = NodeOf(0);
        EastShore(bench, x, y);
        var (fisher, slot) = Place(bench, new LooksForWater());

        TurnsWhile<LooksForWater>(bench, registry, slot, fisher);

        var walk = (WalksFree)((WaitsOutStep)fisher.Activity).Then;
        Assert.AreEqual(
            (new Vector(x - HutX - 1, y - HutY - 1), new Vector(HutX + 1 - x, HutY + 1 - y), 1),
            (walk.Trip.Remaining, ((ToWater)walk.Trip.Purpose).Back, bench.Draws.Taken)
        );
    }

    [TestMethod]
    public void ANodeWithAnObjectIsNoShoreAndTheSearchDrawsAgain()
    {
        var (bench, _, registry, _) = Land(0, 4);
        var (x, y) = NodeOf(0);
        EastShore(bench, x, y);
        bench.Map.At(x, y).Object = new MapObject { Code = 8 };
        var (x2, y2) = NodeOf(4);
        WestShore(bench, x2, y2);
        var (fisher, slot) = Place(bench, new LooksForWater());

        TurnsWhile<LooksForWater>(bench, registry, slot, fisher);

        Assert.AreEqual((2, true), (bench.Draws.Taken, fisher.Activity is WaitsOutStep));
    }

    [TestMethod]
    public void ANodeWithARoadIsNoShore()
    {
        var (bench, _, registry, _) = Land(0, 4);
        var (x, y) = NodeOf(0);
        EastShore(bench, x, y);
        bench.Map.At(x, y).Roads = RoadSet.Of(0);
        var (x2, y2) = NodeOf(4);
        WestShore(bench, x2, y2);
        var (fisher, slot) = Place(bench, new LooksForWater());

        TurnsWhile<LooksForWater>(bench, registry, slot, fisher);

        Assert.AreEqual((2, true), (bench.Draws.Taken, fisher.Activity is WaitsOutStep));
    }

    [TestMethod]
    public void AFisherWithNoShoreInReachDrawsUntilTheTimerWrapsAndKeepsLooking()
    {
        var (bench, _, registry, _) = Land([.. Enumerable.Repeat((ushort)0, 100)]);
        bench.Map.At(HutX + 1, HutY).Object = new MapObject { Code = 8 };
        var (fisher, slot) = Place(bench, new LooksForWater());
        // The timer ran out 1000 units ago: it reads 0xfc18 and takes 100 for each node that is no shore, 10 draws to wrap.
        fisher.Stamp = (ushort)(bench.Clock - 992);
        bench.Clock += 8;

        registry.Handle(slot, fisher);

        Assert.AreEqual((10, 0, true), (bench.Draws.Taken, (int)fisher.Timer, fisher.Activity is LooksForWater));
    }

    // A fisher on the node he found, casting: the water node beside him holds the detail.
    static (BehaviourBench Bench, ActivityRegistry Registry, Settler Fisher, int Slot) Casting(
        int frame,
        int detail,
        params ushort[] draws
    )
    {
        var (bench, _, registry, _) = Land(draws);
        var (x, y) = frame == EastCast ? (HutX + 1, HutY) : (HutX - 1, HutY);
        bench.Map.At(x, y).LakeMark = true;
        bench.Map.At(x, y).LakeDetail = detail;
        var (fisher, slot) = Place(
            bench,
            new Fishes
            {
                Back = new Vector(0, 0),
                Turns = 0,
                Caught = false,
                Casts = 0,
            },
            frame: frame
        );
        fisher.Stamp = bench.Clock;

        return (bench, registry, fisher, slot);
    }

    [TestMethod]
    public void ACastWhoseDrawIsBelowTheDetailPlusFourCatchesAFishAndLowersTheDetailByOne()
    {
        // The draw's low six bits, 6, are below 3 + 4.
        var (bench, registry, fisher, slot) = Casting(EastCast, 3, 6);

        TurnsWhile<Fishes>(bench, registry, slot, fisher);

        Assert.AreEqual(
            (true, 2, 1),
            (
                fisher.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: true } },
                bench.Map.At(HutX + 1, HutY).LakeDetail,
                bench.Draws.Taken
            )
        );
    }

    [TestMethod]
    public void ACastWhoseDrawIsAtLeastTheDetailPlusFourCatchesNothingAndTenCastsEndTheWork()
    {
        // 7 is not below 3 + 4, and the other nine casts draw 63: ten casts, no catch, and he turns back with nothing.
        var (bench, registry, fisher, slot) = Casting(EastCast, 3, [7, .. Enumerable.Repeat((ushort)63, 9)]);

        TurnsWhile<Fishes>(bench, registry, slot, fisher);

        Assert.AreEqual(
            (true, 3, 10),
            (
                fisher.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: false } },
                bench.Map.At(HutX + 1, HutY).LakeDetail,
                bench.Draws.Taken
            )
        );
    }

    [TestMethod]
    public void ACastAtALakeDetailOfZeroDrawsNothingCatchesNothingAndTheDetailStaysZero()
    {
        var (bench, registry, fisher, slot) = Casting(EastCast, 0);

        TurnsWhile<Fishes>(bench, registry, slot, fisher);

        Assert.AreEqual(
            (true, 0, 0),
            (
                fisher.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: false } },
                bench.Map.At(HutX + 1, HutY).LakeDetail,
                bench.Draws.Taken
            )
        );
    }

    [TestMethod]
    public void ADetailOf252OrMoreWrapsInTheByteSumSoTheCastCatchesOnlyBelowTheWrappedChance()
    {
        // 252 + 4 is 0 as a byte: no draw is below it, so the cast catches nothing and the detail stays.
        var (bench, registry, fisher, slot) = Casting(EastCast, 252, [.. Enumerable.Repeat((ushort)0, 10)]);

        TurnsWhile<Fishes>(bench, registry, slot, fisher);

        Assert.AreEqual((252, 10), (bench.Map.At(HutX + 1, HutY).LakeDetail, bench.Draws.Taken));
    }

    [TestMethod]
    public void TheEastCastTakesTheNodeBelowTheEastNeighbourWhereTheEastNeighbourIsNoLakeNode()
    {
        var (bench, registry, fisher, slot) = Casting(EastCast, 0, 0);
        bench.Map.At(HutX + 1, HutY).LakeMark = false;
        bench.Map.At(HutX + 1, HutY + 1).LakeDetail = 5;

        TurnsWhile<Fishes>(bench, registry, slot, fisher);

        Assert.AreEqual(4, bench.Map.At(HutX + 1, HutY + 1).LakeDetail);
    }

    [TestMethod]
    public void TheWestCastTakesTheWestNeighbourAndWhereItIsNoLakeNodeTheNodeBelowTheFisher()
    {
        var (bench, registry, fisher, slot) = Casting(WestCast, 5, 0);
        TurnsWhile<Fishes>(bench, registry, slot, fisher);
        var (bench2, registry2, fisher2, slot2) = Casting(WestCast, 0, 0);
        bench2.Map.At(HutX - 1, HutY).LakeMark = false;
        bench2.Map.At(HutX, HutY + 1).LakeDetail = 5;

        TurnsWhile<Fishes>(bench2, registry2, slot2, fisher2);

        Assert.AreEqual((4, 4), (bench.Map.At(HutX - 1, HutY).LakeDetail, bench2.Map.At(HutX, HutY + 1).LakeDetail));
    }

    [TestMethod]
    public void AFisherBackAtTheFlagWithAFishLaysGoodOneOnTheFlagAndGoesIntoTheBuilding()
    {
        var (bench, parts, _, laid) = Land();
        var (fisher, slot) = Place(bench, Walk(new BackToHut { Carrying = true }), HutX + 1, HutY + 1);

        RulesOf(parts).Arrive(slot, fisher);

        Assert.AreEqual(((0, 0, 1), true), (laid.Single(), fisher.Activity is Door));
    }

    [TestMethod]
    public void AFisherBackAtTheFlagWithNothingLaysNothing()
    {
        var (bench, parts, _, laid) = Land();
        var (fisher, slot) = Place(bench, Walk(new BackToHut { Carrying = false }), HutX + 1, HutY + 1);

        RulesOf(parts).Arrive(slot, fisher);

        Assert.AreEqual((0, true), (laid.Count, fisher.Activity is Door));
    }

    [TestMethod]
    public void AFisherWhoReachesHisNodeStartsCastingInTheFrameOfTheCastThatFitsIt()
    {
        var (bench, parts, _, _) = Land();
        EastShore(bench, 7, 6);
        WestShore(bench, 9, 6);
        var back = new Vector(-2, -1);
        var (east, eastSlot) = Place(bench, Walk(new ToWater { Back = back }), 7, 6);
        var (west, westSlot) = Place(bench, Walk(new ToWater { Back = back }), 9, 6);

        RulesOf(parts).Arrive(eastSlot, east);
        RulesOf(parts).Arrive(westSlot, west);

        Assert.AreEqual(
            (EastCast, WestCast, 0x2ff, back),
            (east.Frame, west.Frame, (int)east.Timer, ((Fishes)east.Activity).Back)
        );
    }

    [TestMethod]
    public void AFisherWhoReachesANodeWithARoadOrNoShoreTurnsBackAtOnceWithNothing()
    {
        var (bench, parts, _, _) = Land();
        EastShore(bench, 7, 6);
        bench.Map.At(7, 6).Roads = RoadSet.Of(0);
        var back = new Vector(-2, -1);
        var (onRoad, roadSlot) = Place(bench, Walk(new ToWater { Back = back }), 7, 6);
        var (inland, inlandSlot) = Place(bench, Walk(new ToWater { Back = back }), 10, 10);

        RulesOf(parts).Arrive(roadSlot, onRoad);
        RulesOf(parts).Arrive(inlandSlot, inland);

        Assert.IsTrue(
            new[] { onRoad, inland }.All(fisher =>
                fisher.Activity is WalksFree { Trip: { Remaining: var way, Purpose: BackToHut { Carrying: false } } }
                && way == back
                && fisher.Timer == 0
            )
        );
    }
}
