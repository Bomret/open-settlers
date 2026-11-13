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

    private static Settler Of(Activity activity, ushort stamp = 0) => new(new Carrier(), 0, 4, 3, stamp, activity);

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
    }

    [TestMethod]
    public void TheCodeOfEachActivityIsTheOriginalsStateCode()
    {
        Assert.AreEqual(0x42, new WaitsAtFlag(0, 0, new Heading(0)).Code);
        Assert.AreEqual(0x12, new LooksForTree().Code);
        Assert.AreEqual(0x35, new KnightWalks(new Trip(new Vector(0, 0), new Home())).Code);
    }

    // The arms below take their expected bytes from docs/rules/ (the file named at each) and hand arithmetic for the words: the word
    // at 12 is byte 12 plus 256 times byte 13, the word at 14 is byte 14 plus 256 times byte 15. An activity with no Residue set
    // leaves the bytes it does not own at 0.

    // construction.md, state 9: byte 11 is 1 while he looks for the next material and 0xff while he blows; byte 14 holds the count of
    // the materials taken in its low nibble and the second figure in bit 7; byte 15 holds the blows left.
    [TestMethod]
    public void TheBuilderHoldsTheBlowModeTheMaterialsAndTheSecondFigureInByte14()
    {
        Assert.AreEqual((9, 0xff, 5, 0x83, 6), Fields(new Builds(5, true, 3, true, 6)));
        Assert.AreEqual((9, 1, 5, 3, 8), Fields(new Builds(5, false, 3, false, 8)));
    }

    // construction.md, "the builder's byte 11 becomes 0xfe. Bytes 14 and 15 become 0 and 2", state 0x4a; digging.md: bytes 12 to 14 are 0.
    [TestMethod]
    public void TheBuilderLeavingTheSiteHoldsTheStorageModeAndTheStateOfTheWalkInNext()
    {
        Assert.AreEqual((0x4a, 0xfe, 0, 0, 2), Fields(new LeavesSite()));
    }

    // digging.md, state 8: byte 11 the pass, byte 12 the site's height, byte 13 the node of the pass, byte 14 the phase. Pass 15, height
    // 7, and node 6 give the word at 12 as 7 + 256 * 6 = 0x0607.
    [TestMethod]
    public void TheDiggerHoldsThePassTheHeightAndNodeWordAndThePhase()
    {
        Assert.AreEqual((8, 15, 0x0607, 1, 0), Fields(new Digs(15, 7, 6, 1)));
    }

    // sawmilling.md: state 0x18 holds byte 11 at 0xff while he saws and at 0 while he waits for a log.
    [TestMethod]
    public void TheSawmillerHoldsMode0xffWhileSawingAndZeroWhileWaitingForALog()
    {
        Assert.AreEqual((0x18, 0xff, 0, 0, 0), Fields(new Saws(true)));
        Assert.AreEqual((0x18, 0, 0, 0, 0), Fields(new Saws(false)));
    }

    // sawmilling.md: with the plank out, "byte 11 is 8 (the plank), the word at 12 is 0, and byte 15 is 0xd" in state 0xb.
    [TestMethod]
    public void TheSawmillerAtTheDoorWithAPlankHoldsTheGoodAndTheStateHeLaysItDownIn()
    {
        Assert.AreEqual((0xb, 8, 0, 0, 0xd), Fields(new StepsOutWithGood(Good.Plank)));
    }

    // sawmilling.md: state 0xd "puts the good of byte 11" on the flag.
    [TestMethod]
    public void TheSettlerLayingAGoodDownHoldsItInByte11()
    {
        var raw = SettlerEncoder.Fields(Of(new PutsDownGood(Good.Plank)));

        Assert.AreEqual((0xd, 8, 0), (raw.State, raw.Mode, raw.Target));
    }

    // goods.md, state 0xe: byte 11 the good, the word at 12 the destination flag's slot plus one, byte 14 the heading, byte 15 0 in the
    // first half and 0xff after it. A good 7 for the flag slot 2 gives the word 3.
    [TestMethod]
    public void ACarrierHandingAGoodOverHoldsTheGoodTheDestinationWordAndTheHalfInNext()
    {
        var load = new Load(new Good(7), 2);

        Assert.AreEqual((0xe, 7, 3, 3, 0), Fields(new HandsOver(load, new Heading(3))));
        Assert.AreEqual((0xe, 7, 3, 3, 0xff), Fields(new HandsOver(load, new Heading(3), Marked: true)));
    }

    // goods.md: a carrier in state 0x43 holds "byte 14 is the heading and byte 15 is 0".
    [TestMethod]
    public void ACarrierThatClaimsItsNodeHoldsTheHeadingInByte14AndNextZero()
    {
        var raw = SettlerEncoder.Fields(Of(new ClaimsNode(new Heading(4), new Heading(1))));

        Assert.AreEqual((0x43, 4, 0), (raw.State, raw.Heading, raw.Next));
    }

    // settlers.md and sawmilling.md: state 6 on the flag of a building; a mode of 0xfe sends the settler into a storage, any other mode
    // (here the residue's 0) does not.
    [TestMethod]
    public void TheSettlerEnteringABuildingHoldsMode0xfeOnlyForAStorage()
    {
        Assert.AreEqual((6, 0xfe, 0, 0, 0), Fields(new EntersBuilding(Storage: true)));
        Assert.AreEqual((6, 0, 0, 0, 0), Fields(new EntersBuilding()));
    }

    // forestry.md: the free walk after the step onto the flag holds its vector in bytes 11 and 12 and the negated vector in bytes 13 and 14,
    // and byte 15 holds the state 0x10 of the free walk. The vector (2, -3) gives byte 11 2 and byte 12 0xfd, the way back (-2, 3) gives
    // byte 13 0xfe and byte 14 3: the word at 12 is 0xfd + 256 * 0xfe = 0xfefd.
    [TestMethod]
    public void TheStepOntoTheFlagHoldsTheStateAndTheWalkItLeadsTo()
    {
        var walk = new WalksFree(new Trip(new Vector(2, -3), new ToTree(new Vector(-2, 3))));

        Assert.AreEqual((7, 2, 0xfefd, 3, 0x10), Fields(new StepsOntoFlag(walk)));
    }

    // stonecutting.md, state 0x17: bytes 11 and 12 the way back, byte 13 the part of the cut, the word at 14 the quarter. The way back
    // (-1, 2) gives byte 11 0xff and byte 12 2; the phase 1 and byte 12 give the word at 12 as 2 + 256 * 1 = 0x0102; the quarter 0x0123
    // gives byte 14 0x23 and byte 15 0x01.
    [TestMethod]
    public void TheStonecutterHoldsTheWayBackThePhaseAndTheQuarter()
    {
        Assert.AreEqual((0x17, 0xff, 0x0102, 0x23, 0x01), Fields(new CutsStone(new Vector(-1, 2), 1, 0x0123)));
    }

    // forestry.md, state 0x14: bytes 11 and 12 the way back, byte 13 the east way back again, byte 14 0xff once the sapling is down and
    // 0 before. The way back (2, -3) gives byte 11 2 and byte 12 0xfd, so the word at 12 is 0xfd + 256 * 2 = 0x02fd.
    [TestMethod]
    public void TheForesterHoldsTheWayBackTwiceAndByte14ForTheSapling()
    {
        Assert.AreEqual((0x14, 2, 0x02fd, 0xff, 8), Fields(new Plants(new Vector(2, -3), true, 8)));
        Assert.AreEqual((0x14, 2, 0x02fd, 0, 8), Fields(new Plants(new Vector(2, -3), false, 8)));
    }

    [TestMethod]
    public void TheBytesAnActivityLeavesUnusedRideAlongInTheResidue()
    {
        var settler = Of(new Walks(new ToBuilding(1), new Heading(4), 2));

        var residue = SettlerEncoder.ResidueOf(settler);

        Assert.AreEqual(new Residue(0xff, 1, 4, 2), residue);
    }
}
