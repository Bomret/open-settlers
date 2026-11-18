using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;
using static OpenSettlers.Domain.Testing.SettlerCases;

namespace OpenSettlers.Tests;

// The placement of a walking settler's terms on made-up walk steps (no install): which settlers are drawn, the step they are
// at, the row, the order, and the position of their terms. The sprites the History Edition's tables pick for the terms are
// checked by the class of the same name in src/Import.HistoryEdition.Tests. The shape of the steps (entry 2 of the graphics
// archive) and the use of the timer follow docs/rules/settlers.md.
[TestClass]
public sealed class SettlerFiguresTests
{
    // The walk steps of the made-up sequences: the frame 7 holds three steps, (1, 2), (-1, 0), and (0, 0), one for each eight
    // clock units of time left; the frame 8 holds the step (0, 0); every other frame has none.
    sealed class MadeSteps : IWalkSteps
    {
        static readonly (int Dx, int Dy)[] Seven = [(1, 2), (-1, 0), (0, 0)];

        public StepOffset? At(int frame, int timeLeft)
        {
            var index = timeLeft >> 3;
            (int Dx, int Dy)? step = frame switch
            {
                7 when index < Seven.Length => Seven[index],
                8 when index == 0 => (0, 0),
                _ => null,
            };

            return step is { } at ? new StepOffset { Dx = at.Dx, Dy = at.Dy } : null;
        }
    }

    static SettlerFigures Figures(sbyte[]? frameRows = null) =>
        new(new MadeSteps(), new FigureTables(frameRows: frameRows));

    // A map whose node of the waiting carriers (4,3) has the roads given, and the lake mark where asked.
    static Map MapWithRoad(RoadSet roads, bool lakeMark = false)
    {
        var map = Map();
        map.At(4, 3).Roads = roads;
        map.At(4, 3).LakeMark = lakeMark;

        return map;
    }

    static Map Map() =>
        MapGenerator.Generate(
            MadeProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );

    static readonly SettlerPart[] ThreeParts = [SettlerPart.Figure, SettlerPart.Overlay, SettlerPart.SecondFigure];

    // The terms of a settler are its figure, its overlay, and its second figure; the source decides which of them it draws.
    // A job draws its figure through its handler (0x43f744 plus 8 for each job); the History Edition's tables pick no sprite for
    // a job without one, but the placement makes the terms of every job.
    [TestMethod]
    [DataRow(4)]
    [DataRow(21)]
    public void ASettlerOfAJobWithoutAFigureHandlerIsNotDrawn(int job)
    {
        var walker = Walker(7, 0);
        var other = new Settler(Job.Of(job), 0, 4, 3, 0, walker.Activity)
        {
            Frame = walker.Frame,
            Timer = walker.Timer,
            Stamp = 0,
        };

        Assert.IsNotEmpty(Figures().Draws(Map(), [walker], 0), "a woodcutter in the same walk is drawn");
        Assert.HasCount(3, Figures().Draws(Map(), [other], 0), "the source answers for the job, not the placement");
    }

    [TestMethod]
    public void AWalkingSettlerIsDrawnAsItsFigureItsColourOverlayAndItsSecondFigure()
    {
        var terms = Figures().Draws(Map(), [Walker(7, 0)], 0);

        CollectionAssert.AreEqual(ThreeParts, terms.Select(term => term.Part).ToArray());
    }

    [TestMethod]
    public void TheSequenceAdvancesOneTripleForEachEightClockUnitsOfTimeLeft()
    {
        var figures = Figures();

        var steps = new[] { 0, 8, 16 }.Select(timer => figures.Draws(Map(), [Walker(7, timer)], 0)[0].Step).ToArray();

        CollectionAssert.AreEqual(new[] { 0, 8, 16 }, steps);
    }

    [TestMethod]
    public void TheOffsetsOfATripleMoveTheFigureAlongTheStep()
    {
        var figures = Figures();

        var first = figures.Draws(Map(), [Walker(7, 0)], 0)[0];
        var second = figures.Draws(Map(), [Walker(7, 8)], 0)[0];

        Assert.AreEqual(-2, second.X - first.X, "the dx of the triples are 1 and -1");
        Assert.AreEqual(-2, second.Y - first.Y);
    }

    [TestMethod]
    public void TheTimeLeftRunsDownWithTheClockBetweenTheSettlersTurns()
    {
        var term = Figures().Draws(Map(), [Walker(7, 16)], 8)[0];

        Assert.AreEqual(8, term.Step, "16 units left at the settler's turn, 8 gone since: 8 left, the second triple");
    }

    [TestMethod]
    public void OnlyASettlerOnTheRoadIsDrawn()
    {
        var figures = Figures();

        Assert.IsEmpty(figures.Draws(Map(), [Walker(7, 0, state: 1), Walker(7, 0, state: 0x12)], 0));
        Assert.HasCount(3, figures.Draws(Map(), [Walker(7, 0, state: 5)], 0));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(0x10)]
    [DataRow(0x11)]
    public void AWoodcutterStepsThroughItsDoorWalksFreeAndCutsDrawnAsAWalkingSettlerIs(int state)
    {
        Assert.HasCount(3, Figures().Draws(Map(), [Walker(7, 0, state: state)], 0));
    }

    // Confirmed (draw runs walk-home, dismissed, dismissed-with-log): a settler that searches for the nearest flag (0x19) on its
    // way home is drawn as a walking settler is.
    [TestMethod]
    public void ASettlerSearchingForAFlagOnItsWayHomeIsDrawnAsAWalkingSettlerIs()
    {
        Assert.HasCount(3, Figures().Draws(Map(), [Walker(7, 0, state: 0x19)], 0));
    }

    // Confirmed (the same runs): a settler that claims its node (0x1c) on its way home, and a settler in the states 0x44 and
    // 0x45, have no record of the settler's draw path. A carrier in 0x44 or 0x45 is drawn by the tile pass instead
    // (AWaitingCarrierIsDrawnByTheTilePassOnItsNode).
    [TestMethod]
    [DataRow(0x1c)]
    [DataRow(0x44)]
    [DataRow(0x45)]
    public void ASettlerThatClaimsItsNodeOrWaitsToGoHomeIsNotDrawn(int state)
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(7, 0, state: state)], 0));
    }

    // Confirmed (the draw runs of the carried goods): the carrier that hands a good over (state 0xe) is drawn as one on its
    // road is, with the good it carries in its terms.
    [TestMethod]
    public void ACarrierHandingAGoodOverIsDrawnWithTheGoodInItsHands()
    {
        var terms = Figures().Draws(Map(), [Carrier(mode: 7, state: 0xe)], 0);

        Assert.HasCount(3, terms);
        Assert.IsTrue(terms.All(term => term.Carried == TestCatalog.GoodOf(7)));
    }

    [TestMethod]
    public void ACarrierOnItsRoadIsDrawnAsAWalkingSettlerIs()
    {
        var figures = Figures();

        Assert.HasCount(3, figures.Draws(Map(), [Walker(7, 0, state: 3)], 0));
        Assert.AreEqual(8, figures.Draws(Map(), [Walker(7, 16, state: 3)], 8)[0].Step);
    }

    // Confirmed by the draw run (settler-draws.txt): the settler's draw path has no record for a settler in state 0x42 (the
    // carrier's handler returns at once). A settler of another job in that state draws nothing.
    [TestMethod]
    public void ASettlerOfAnotherJobInTheWaitingStateIsNotDrawn()
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(7, 8, state: 0x42)], 800));
    }

    // A carrier that waits off its node (states 0x42 to 0x45) leaves a mark on the node (0x43bc52), and the tile pass draws it
    // there (0x44d77e): the term holds the node's facts and the game's clock, at the node lifted by 4 times its height, with no
    // lift of 2.
    [TestMethod]
    [DataRow(0x42)]
    [DataRow(0x43)]
    [DataRow(0x44)]
    [DataRow(0x45)]
    public void AWaitingCarrierIsDrawnByTheTilePassOnItsNode(int state)
    {
        var map = MapWithRoad(RoadSet.Of(0).With(3));
        var height = map.At(4, 3).Height;
        var (nodeX, nodeY) = Camera.NodePosition(4, 3);

        var terms = Figures().Draws(map, [Carrier(mode: 7, state: state)], 40);

        Assert.HasCount(3, terms);
        Assert.IsTrue(terms.All(term => term.Node is not null && term.Step == 40));
        Assert.AreEqual((nodeX, nodeY - 4 * height), (terms[0].X, terms[0].Y));
    }

    // The phase is the node's column's (stand-in for the address of the node's record): the term holds the column 13.
    [TestMethod]
    public void AWaitingCarriersPhaseIsItsColumns()
    {
        var map = Map();
        map.At(13, 3).Roads = RoadSet.Of(0).With(3);
        var carrier = Carrier(mode: 7, state: 0x42);
        carrier.X = 13;

        Assert.AreEqual(13, Figures().Draws(map, [carrier], 40)[0].Node!.Column);
    }

    // A node with the lake mark draws the fixed figure word 0x203 with no offsets (0x44d77e): the term says so and the position
    // is the node's.
    [TestMethod]
    public void AWaitingCarrierOnANodeWithBitSixDrawsTheFixedFigure()
    {
        var map = MapWithRoad(RoadSet.Of(0).With(3), lakeMark: true);
        var height = map.At(4, 3).Height;
        var (nodeX, nodeY) = Camera.NodePosition(4, 3);

        var term = Figures().Draws(map, [Carrier(mode: 7, state: 0x42)], 40)[0];

        Assert.IsTrue(term.Node!.LakeMark);
        Assert.AreEqual((nodeX, nodeY - 4 * height), (term.X, term.Y));
    }

    // 0x44d3ae draws a row's settler records, then the tile pass's records of that row.
    [TestMethod]
    public void AWaitingCarrierIsDrawnAfterTheWalkingSettlersOfItsRow()
    {
        var walker = Walker(7, 0);
        walker.X = 6;

        var terms = Figures().Draws(MapWithRoad(RoadSet.Of(0).With(3)), [Carrier(mode: 7, state: 0x42), walker], 40);

        Assert.HasCount(6, terms);
        Assert.IsTrue(terms.Take(3).All(term => term.Node is null && term.Job is Woodcutter));
        Assert.IsTrue(terms.Skip(3).All(term => term.Node is not null && term.Job is Carrier));
    }

    // The row of a record is the settler's row plus the byte of its frame's row (0x43f537); a list holds its records in the order
    // of the original's scan of the nodes, which is the settlers' row, then column.
    [TestMethod]
    public void ARecordJoinsTheRowItsFramesByteMovesItTo()
    {
        var rows = new sbyte[16];
        rows[8] = -2;
        // The settler of the frame 8 (the felling) stands in row 4, the one of the frame 7 in row 3; the frame's byte
        // puts the first in row 2, so it is drawn first.
        var felling = Walker(8, 0, state: 0x11);
        felling.Y = 4;

        var terms = Figures(rows).Draws(Map(), [Walker(7, 0), felling], 0);

        Assert.AreEqual(8, terms[0].Frame, "the felling");
        Assert.AreEqual(2, terms[0].Row);
        Assert.AreEqual(3, terms[^1].Row);
    }

    [TestMethod]
    public void TheRecordsOfOneRowAreInTheOrderOfTheNodesFromTheWest()
    {
        var east = Walker(7, 0);
        east.X = 6;
        var west = Walker(7, 0);
        west.X = 2;

        var terms = Figures().Draws(Map(), [east, west], 0);

        Assert.IsLessThan(terms[3].X, terms[0].X, "the settler at column 2 first");
    }

    // 0x43f537: the figure is 4 times the node's height and 2 more pixels above the node, plus the triple's offsets.
    [TestMethod]
    public void ASettlerIsLiftedByTheHeightOfItsNodeAndTwoPixels()
    {
        var map = Map();
        var settler = Walker(7, 0);
        var height = map.At(settler.X, settler.Y).Height;
        var (nodeX, nodeY) = Camera.NodePosition(settler.X, settler.Y);

        var term = Figures().Draws(map, [settler], 0)[0];

        Assert.AreEqual(nodeX + 1, term.X, "the triple's dx is 1");
        Assert.AreEqual(nodeY + 2 - 4 * height - 2, term.Y, "the triple's dy is 2");
    }

    [TestMethod]
    public void AWoodcutterOnTheWayBackWithTheLogIsDrawnInTheRowOfTheLog()
    {
        var home = Walker(7, 0);
        home.Activity = WayBack(true);

        var terms = Figures().Draws(Map(), [home], 0);

        Assert.HasCount(3, terms);
        Assert.IsTrue(terms.All(term => term.Activity == home.Activity));
    }

    [TestMethod]
    public void AWoodcutterWhoseTreeIsGoneWalksBackInTheRowOfTheWalk()
    {
        var home = Walker(7, 0);
        home.Activity = WayBack(false);

        Assert.HasCount(3, Figures().Draws(Map(), [home], 0));
    }

    [TestMethod]
    public void AWoodcutterWithTheLogOutsideTheFreeWalkIsDrawnInTheRowOfTheWalk()
    {
        Assert.HasCount(3, Figures().Draws(Map(), [Walker(7, 0, state: 4)], 0));
    }

    [TestMethod]
    public void ABuilderWalkingAndAtWorkIsDrawnInTheRowsOfHisHandler()
    {
        var figures = Figures();
        var map = Map();
        Settler Builder(int frame, Activity activity) =>
            new(new Builder(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var work = figures.Draws(
            map,
            [
                Builder(
                    8,
                    new Builds
                    {
                        Site = 1,
                        Blowing = true,
                        Materials = 0,
                        SecondFigure = false,
                        Blows = 8,
                    }
                ),
            ],
            0
        );

        Assert.HasCount(3, figures.Draws(map, [Builder(7, ActivityOf(2))], 0));
        Assert.HasCount(3, work);
        Assert.IsTrue(work.All(term => term.Job is Builder && term.Frame == 8));
    }

    [TestMethod]
    public void ADiggerWalkingAndAtWorkIsDrawnInTheRowsOfHisHandler()
    {
        var figures = Figures();
        var map = Map();
        Settler Digger(int frame, Activity activity) =>
            new(new Digger(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        var work = figures.Draws(
            map,
            [
                Digger(
                    8,
                    new Digs
                    {
                        Pass = 15,
                        Target = 10,
                        Node = 6,
                        Phase = 1,
                    }
                ),
            ],
            0
        );

        Assert.HasCount(3, figures.Draws(map, [Digger(7, ActivityOf(2))], 0));
        Assert.HasCount(3, work);
        Assert.IsTrue(work.All(term => term.Job is Digger && term.Frame == 8));
    }

    [TestMethod]
    public void ASawmillerWalkingSawingAndCarryingAPlankOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsForALog()
    {
        var figures = Figures();
        var map = Map();
        Settler Sawmiller(int frame, Activity activity) =>
            new(new Sawmiller(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        Assert.HasCount(3, figures.Draws(map, [Sawmiller(7, ActivityOf(2))], 0));
        Assert.HasCount(3, figures.Draws(map, [Sawmiller(8, new Saws { Sawing = true })], 0));
        Assert.HasCount(
            3,
            figures.Draws(map, [Sawmiller(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Plank } })], 0)
        );
        Assert.IsEmpty(figures.Draws(map, [Sawmiller(8, new Saws { Sawing = false })], 0));
    }

    [TestMethod]
    public void AMillerWalkingGrindingAndCarryingFlourOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsOrPauses()
    {
        var figures = Figures();
        var map = Map();
        Settler Miller(int frame, Activity activity) =>
            new(new Miller(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        Assert.HasCount(3, figures.Draws(map, [Miller(7, ActivityOf(2))], 0));
        Assert.HasCount(3, figures.Draws(map, [Miller(8, new Grinds { Phase = 1 })], 0));
        Assert.HasCount(
            3,
            figures.Draws(map, [Miller(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Flour } })], 0)
        );
        Assert.IsEmpty(figures.Draws(map, [Miller(8, new Grinds { Phase = 0 })], 0));
        Assert.IsEmpty(figures.Draws(map, [Miller(8, new Grinds { Phase = 2 })], 0));
        Assert.HasCount(3, figures.Draws(map, [Miller(8, new Grinds { Phase = 3 })], 0));
        Assert.IsEmpty(figures.Draws(map, [Miller(8, new Grinds { Phase = 4 })], 0));
    }

    [TestMethod]
    public void ABakerWalkingBakingAndCarryingBreadOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsOrPauses()
    {
        var figures = Figures();
        var map = Map();
        Settler Baker(int frame, Activity activity) =>
            new(new Baker(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        Assert.HasCount(3, figures.Draws(map, [Baker(7, ActivityOf(2))], 0));
        Assert.HasCount(3, figures.Draws(map, [Baker(8, new Bakes { Phase = 1 })], 0));
        Assert.HasCount(
            3,
            figures.Draws(map, [Baker(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Bread } })], 0)
        );
        Assert.IsEmpty(figures.Draws(map, [Baker(8, new Bakes { Phase = 0 })], 0));
        Assert.IsEmpty(figures.Draws(map, [Baker(8, new Bakes { Phase = 2 })], 0));
    }

    [TestMethod]
    public void AButcherWalkingWorkingAndCarryingMeatOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsForAPig()
    {
        var figures = Figures();
        var map = Map();
        Settler Butcher(int frame, Activity activity) =>
            new(new Butcher(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        Assert.HasCount(3, figures.Draws(map, [Butcher(7, ActivityOf(2))], 0));
        Assert.HasCount(3, figures.Draws(map, [Butcher(8, new Butchers { Working = true })], 0));
        Assert.HasCount(
            3,
            figures.Draws(map, [Butcher(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Meat } })], 0)
        );
        Assert.IsEmpty(figures.Draws(map, [Butcher(8, new Butchers { Working = false })], 0));
    }

    [TestMethod]
    public void APigFarmerWalkingWorkingAndCarryingAPigOutIsDrawnInTheRowsOfHisHandlerAndNotWhereHeWaitsOrPauses()
    {
        var figures = Figures();
        var map = Map();
        Settler PigFarmer(int frame, Activity activity) =>
            new(new PigFarmer(), 0, 4, 3, 0, activity) { Frame = frame, Timer = 0 };

        Assert.HasCount(3, figures.Draws(map, [PigFarmer(7, ActivityOf(2))], 0));
        Assert.HasCount(3, figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 1 })], 0));
        Assert.HasCount(
            3,
            figures.Draws(map, [PigFarmer(7, new WaitsOutStep { Then = new PutsDownGood { Good = Good.Pig } })], 0)
        );
        // He is drawn in each stretch that works (1, 3, and 5) and in no other.
        Assert.IsEmpty(figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 0 })], 0));
        Assert.IsEmpty(figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 2 })], 0));
        Assert.HasCount(3, figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 3 })], 0));
        Assert.IsEmpty(figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 4 })], 0));
        Assert.HasCount(3, figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 5 })], 0));
        Assert.IsEmpty(figures.Draws(map, [PigFarmer(8, new TendsPigs { Phase = 6 })], 0));
    }

    [TestMethod]
    public void AWoodcutterWhoseSequenceHoldsAFigureByteFrom0x80IsDrawnInTheFellingRow()
    {
        var terms = Figures().Draws(Map(), [Walker(8, 0, state: 0x11)], 0);

        Assert.HasCount(3, terms);
        Assert.IsTrue(terms.All(term => term.Frame == 8));
    }

    [TestMethod]
    public void ASettlerOfAnotherJobDrawsNothing()
    {
        var jobless = new Settler(new Jobless(), 0, 4, 3, 0, ActivityOf(2)) { Frame = 7 };

        // The placement makes the terms of any job; the tables pick no sprite for a settler without a job.
        Assert.HasCount(3, Figures().Draws(Map(), [jobless], 0));
    }

    [TestMethod]
    public void AFrameWithNoSequenceDrawsNothing()
    {
        Assert.IsEmpty(Figures().Draws(Map(), [Walker(9, 0)], 0));
    }

    [TestMethod]
    public void ACarrierCarryingALogIsDrawnWithTheFigureByteOfTheLogAdded()
    {
        var terms = Figures().Draws(Map(), [Carrier(mode: 7)], 0);

        Assert.HasCount(3, terms);
        Assert.IsTrue(terms.All(term => term.Carried == TestCatalog.GoodOf(7)));
    }

    [TestMethod]
    public void ACarrierWithNoGoodIsDrawnAsBefore()
    {
        var terms = Figures().Draws(Map(), [Carrier(mode: 0)], 0);

        Assert.HasCount(3, terms);
        Assert.IsTrue(terms.All(term => term.Carried == Good.None));
    }

    // 0x406610 draws the figure, then the overlay over it, then the second figure over both; the row order of the settlers
    // keeps that order within each settler.
    [TestMethod]
    public void EachSettlerDrawsItsFigureThenItsOverlayThenItsSecondFigureWithSeveralSettlersOnARow()
    {
        var settlers = Enumerable.Range(0, 40).Select(_ => Walker(7, 0)).ToArray();

        var parts = Figures().Draws(Map(), settlers, 0).Select(term => term.Part).ToArray();

        CollectionAssert.AreEqual(Enumerable.Range(0, 40).SelectMany(_ => ThreeParts).ToArray(), parts);
    }

    [TestMethod]
    public void AJobWithNoSecondFigureDrawsItsFigureAndOverlay()
    {
        // The carrier's terms are the three parts too: the source answers the second figure with nothing where its row has none.
        var terms = Figures().Draws(Map(), [Carrier(mode: 0)], 0);

        CollectionAssert.AreEqual(ThreeParts, terms.Select(term => term.Part).ToArray());
    }

    // The settler's term names the settler in game terms: job, activity, heading, animation step (the time left, in clock
    // units), player, carried good, and the frame of its walk; the part says which of its three images the term is.
    [TestMethod]
    public void ASettlersTermsNameItsJobActivityHeadingStepPlayerAndCarriedGood()
    {
        var load = new Load { Good = Good.Log, Destination = null };
        var carrier = new Settler(
            new Carrier(),
            1,
            4,
            3,
            0,
            new WalksRoad
            {
                Load = load,
                Heading = new Heading { Value = 2 },
            }
        )
        {
            Frame = 7,
            Timer = 16,
        };

        var terms = Figures().Draws(Map(), [carrier], 8);

        Assert.HasCount(3, terms);
        CollectionAssert.AreEqual(ThreeParts, terms.Select(term => term.Part).ToArray());
        Assert.IsTrue(
            terms.All(term =>
                term.Job == new Carrier()
                && term.Activity == carrier.Activity
                && term.Heading == new Heading { Value = 2 }
                && term.Frame == 7
                && term.Step == 8
                && term.Player == 1
                && term.Carried == Good.Log
                && term.Row == 3
            )
        );
    }

    [TestMethod]
    public void ASettlerWithNoHeadingOrGoodCarriesNone()
    {
        var term = Figures().Draws(Map(), [Walker(7, 0, state: 0x11)], 0)[0];

        Assert.IsNull(term.Heading);
        Assert.AreEqual(Good.None, term.Carried);
        Assert.IsNull(term.Node);
    }

    // The step offsets come from the walk steps: the second triple of the frame 7 moves the figure by (-1, 0) where the first
    // moves it by (1, 2).
    [TestMethod]
    public void AWalkingSettlerIsPlacedFromTheStepOffsetTable()
    {
        var map = Map();
        var (nodeX, nodeY) = Camera.NodePosition(4, 3);
        var height = map.At(4, 3).Height;

        var second = Figures().Draws(map, [Walker(7, 8)], 0)[0];

        Assert.AreEqual((nodeX - 1, nodeY - 4 * height - 2), (second.X, second.Y));
        Assert.AreEqual(8, second.Step);
    }

    // The waiting carrier's term holds the node facts the source picks its figure and offsets by.
    [TestMethod]
    public void AWaitingCarriersTermHoldsItsNodesColumnLakeMarkAndRoads()
    {
        var roads = RoadSet.Of(0).With(3);
        var term = Figures().Draws(MapWithRoad(roads, lakeMark: true), [Carrier(mode: 7, state: 0x42)], 40)[0];

        Assert.AreEqual(
            new WaitingNode
            {
                Column = 4,
                LakeMark = true,
                Roads = roads,
            },
            term.Node
        );
        Assert.AreEqual(40, term.Step, "the game's clock");
    }

    // INV-0010: a settler term holds no entry number, archive, mask, texture, or palette index.
    [TestMethod]
    public void ASettlerTermHoldsNoEntryNumberAndNoColourIndex()
    {
        var names = typeof(SettlerDraw).GetProperties().Select(property => property.Name).ToArray();

        foreach (
            var forbidden in new[] { "Entry", "Colour", "Color", "Palette", "Mask", "Texture", "Archive", "Layer" }
        )
        {
            Assert.IsFalse(
                names.Any(name => name.Contains(forbidden, System.StringComparison.Ordinal)),
                $"SettlerDraw has a property naming {forbidden}"
            );
        }
    }
}
