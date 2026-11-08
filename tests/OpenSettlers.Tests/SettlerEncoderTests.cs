using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The exporter of the typed settler: it writes the original's state, mode, target, heading, and next (bytes 10 to 15 of the
// settler's item) and the stamp again, field for field, as the digest and the event snapshots hold them (ADR 0013). The
// values are the ones the engine's rules wrote into the item before the activities were typed.
[TestClass]
public sealed class SettlerEncoderTests
{
    // The movement that turns a walk home reads the residue as the encoder writes it.
    private static SettlerMovement Movement { get; } =
        new(
            new Map(MapSize.Offered[0]),
            new BuildingTable(),
            LayoutSettlersTests.MadeTables(),
            () => 0,
            SettlerEncoder.ResidueOf
        );

    private static Settler Of(Activity activity, ushort stamp = 0) => new(Job.Carrier, 0, 4, 3, stamp, activity);

    private static (int State, int Mode, int Target, int Heading, int Next) Fields(Activity activity)
    {
        var raw = SettlerEncoder.Fields(Of(activity));

        return (raw.State, raw.Mode, raw.Target, raw.Heading, raw.Next);
    }

    [TestMethod]
    public void ASettlerWaitingInTheCastleHoldsItsStorageAsTheHeading()
    {
        Assert.AreEqual((1, 0, 0, 3, 0), Fields(new WaitsInCastle(3)));
    }

    [TestMethod]
    public void ASettlerLeavingTheCastleForABuildingHoldsMode255AndTheFlagAsTheTarget()
    {
        Assert.AreEqual((0xf, 0xff, 4, 0, 0), Fields(new Leaving(new ToBuilding(4))));
    }

    [TestMethod]
    public void ACarrierLeavingForARoadHoldsTheDirectionAsTheModeAndTheFlagAsTheTarget()
    {
        Assert.AreEqual((0xf, 3, 2, 0, 0), Fields(new Leaving(new ToRoad(2, 3))));
    }

    [TestMethod]
    public void TheWalkHomeHoldsMode254AndTargetZero()
    {
        Assert.AreEqual((2, 0xfe, 0, 0, 0), Fields(new Walks(new ToStorage(), new Heading(0))));
    }

    [TestMethod]
    public void ABlockedStepMarksTheHeadingAsTheDirectionLessSixAndCountsTheTriesInNext()
    {
        var walks = new Walks(new ToBuilding(1), new Heading(4).Retried, 7);

        Assert.AreEqual((2, 0xff, 1, -2, 7), Fields(walks));
    }

    [TestMethod]
    public void ASettlerThatWaitsOutAStepHoldsTheStateItTakesNextInNext()
    {
        var wait = new WaitsOutStep(new Walks(new ToBuilding(1), new Heading(0)));

        Assert.AreEqual((5, 0xff, 1, 0, 2), Fields(wait));
    }

    [TestMethod]
    public void ACarrierOnItsRoadHoldsTheGoodAsTheModeAndTheDestinationFlagPlusOneAsTheTarget()
    {
        var walks = new WalksRoad(new Load(new Good(7), 2), new Heading(1), Waits: 3);

        Assert.AreEqual((3, 7, 3, 1, 3), Fields(walks));
    }

    [TestMethod]
    public void ACarrierMarkedToGoHoldsNext255()
    {
        Assert.AreEqual(
            (3, 0, 5, 1, 0xff),
            Fields(new WalksRoad(null, new Heading(1), Marked: true) { Residue = new(Target: 5) })
        );
    }

    [TestMethod]
    public void ACarrierWithNoGoodKeepsTheTargetOfItsLastGood()
    {
        Assert.AreEqual((3, 0, 9, 2, 0), Fields(new WalksRoad(null, new Heading(2)) { Residue = new(Target: 9) }));
    }

    [TestMethod]
    public void ACarrierWaitingNextToAFlagHoldsTheDirectionAndFlagAndTheApproachInTheStampsLowByte()
    {
        var waiting = new WaitsAtFlag(1, 3, new Heading(2).Retried);
        var raw = SettlerEncoder.Fields(Of(waiting, 0x1200));

        Assert.AreEqual((0x42, 3, 1, 0, 0), (raw.State, raw.Mode, raw.Target, raw.Heading, raw.Next));
        Assert.AreEqual((ushort)0x12fc, raw.Stamp);
    }

    [TestMethod]
    public void AFreeWalkHoldsTheVectorTheWayBackMarkAndTheMemory()
    {
        var toTree = new WalksFree(new Trip(new Vector(-2, 3), new ToTree(new Vector(2, -3)), 0x10));

        Assert.AreEqual((0x10, 0xfe, 0x0203, -3, 0x10), Fields(toTree));
    }

    [TestMethod]
    public void TheWalkBackHoldsTheMark0x80AndOneInTheHeadingWhereItCarriesTheLog()
    {
        var back = new WalksFree(new Trip(new Vector(1, 1), new BackToHut(true)));

        Assert.AreEqual((0x10, 1, 0x8001, 1, 0), Fields(back));
    }

    [TestMethod]
    public void TheWalkHomeHoldsTheMark0x80AndMinusOneInTheHeading()
    {
        var home = new WalksFree(new Trip(new Vector(0, 0), new Home(), 8));

        Assert.AreEqual((0x10, 0, 0x8000, -1, 8), Fields(home));
    }

    [TestMethod]
    public void TheCutHoldsTheWayBackTheVariantMarkAndTheCount()
    {
        var cut = new Cuts(new Vector(2, -3), true, 4, 8);

        Assert.AreEqual((0x11, 2, 0xffFd, 4, 8), Fields(cut));
    }

    [TestMethod]
    public void TheDoorHoldsTheTimeItLeavesAsTheTarget()
    {
        Assert.AreEqual((4, 0xfe, 0x1b, 0, 0), Fields(new Door(0x1b, EnteringStorage: true)));
    }

    [TestMethod]
    public void TheSearchForAFlagHomeHoldsMode1WhereItStartsAtTheFarEnd()
    {
        Assert.AreEqual((0x19, 1, 0, 0, 0), Fields(new SearchesHome(true)));
        Assert.AreEqual((0x19, 0, 0, 0, 0), Fields(new SearchesHome()));
    }

    [TestMethod]
    public void TheSearchForAFlagHomeHoldsTheModeByteTheWalkLeftNotTheResidue()
    {
        Assert.AreEqual((0x19, 0xfe, 0, 0, 0), Fields(new SearchesHome(true, 0xfe) { Residue = new(Mode: 9) }));
        Assert.AreEqual((0x19, 1, 0, 0, 0), Fields(new SearchesHome(true) { Residue = new(Mode: 0) }));
        Assert.AreEqual((0x19, 0, 0, 0, 0), Fields(new SearchesHome() { Residue = new(Mode: 7) }));
    }

    [TestMethod]
    public void TheSearchAWalkTurnsIntoKeepsTheModeByteOfTheWalk()
    {
        var search = Movement.HomeSearchFrom(new Walks(new ToBuilding(1), new Heading(4), 2));

        Assert.AreEqual((0x19, 0xff, 1, 4, 2), Fields(search));
    }

    [TestMethod]
    public void TheSearchAfterAStepClearsTheNextByte()
    {
        var search = Movement.HomeSearchAfterStep(new Walks(new ToBuilding(1), new Heading(4), 2));

        Assert.AreEqual((0x19, 0xff, 1, 4, 0), Fields(search));
    }

    [TestMethod]
    public void AStateTheEngineHasNoNameForIsKeptAsItsCode()
    {
        Assert.AreEqual((0x4a, 0, 0, 0, 0), Fields(new RawState(0x4a)));
        Assert.AreEqual(0x49, new RawState(0x49).Code);
    }

    [TestMethod]
    public void ACarrierWaitingAtAFlagKeepsTheRetriedHeadingInTheLowByteOfItsStamp()
    {
        var settler = Of(new WaitsAtFlag(7, new Direction(2), new Heading(-6)), 0x1234);

        Assert.AreEqual((ushort)0x12fa, SettlerEncoder.Fields(settler).Stamp);
    }

    [TestMethod]
    public void AHeadingKeptRetriedTwiceTurnsBackByByteArithmetic()
    {
        Assert.AreEqual(new Heading(0xfa + 6 & 0xff), new Heading(-6).Unmarked);
        Assert.AreEqual(new Heading(3), new Heading(0).Turned);
        Assert.AreEqual(new Heading(0xfd + 3 & 0xff), new Heading(-3).Turned);
    }

    [TestMethod]
    public void TheCodeOfEachActivityIsTheOriginalsStateCode()
    {
        Assert.AreEqual(0x42, new WaitsAtFlag(0, 0, new Heading(0)).Code);
        Assert.AreEqual(0x12, new LooksForTree().Code);
        Assert.AreEqual(0x35, new KnightWalks(new Trip(new Vector(0, 0), new Home())).Code);
    }

    [TestMethod]
    public void TheBytesAnActivityLeavesUnusedRideAlongInTheResidue()
    {
        var settler = Of(new Walks(new ToBuilding(1), new Heading(4), 2));

        var residue = SettlerEncoder.ResidueOf(settler);

        Assert.AreEqual(new Residue(0xff, 1, 4, 2), residue);
    }
}
