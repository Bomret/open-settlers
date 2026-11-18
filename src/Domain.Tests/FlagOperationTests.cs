using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;

namespace OpenSettlers.Tests;

// The named operations the application changes flag, flag-side, and waiting-good state through: each one writes what the
// rules write together, and nothing else.
[TestClass]
public sealed class FlagOperationTests
{
    static readonly GoodClass Planks = new() { Class = 1, Word = RequestWord.First };

    [TestMethod]
    public void AskForGivesTheWordItsClassAtRankZero()
    {
        var flag = new Flag(4, 5);

        flag.AskFor(RequestWord.First, Planks);

        Assert.AreEqual(new Request { Asks = Planks, Rank = 0 }, flag.RequestOf(RequestWord.First));
    }

    [TestMethod]
    public void RankRequestKeepsTheClass()
    {
        var flag = new Flag(4, 5);
        flag.AskFor(RequestWord.First, Planks);

        flag.RankRequest(RequestWord.First, 0x3e);

        Assert.AreEqual(new Request { Asks = Planks, Rank = 0x3e }, flag.RequestOf(RequestWord.First));
    }

    [TestMethod]
    public void ClearAskKeepsTheRank()
    {
        var flag = new Flag(4, 5);
        flag.AskFor(RequestWord.First, Planks);
        flag.RankRequest(RequestWord.First, 0x3e);

        flag.ClearAsk(RequestWord.First);

        Assert.AreEqual(new Request { Asks = null, Rank = 0x3e }, flag.RequestOf(RequestWord.First));
    }

    [TestMethod]
    [DataRow((byte)0x3f, (byte)0x1f)]
    [DataRow((byte)0x3e, (byte)0)]
    [DataRow((byte)0x40, (byte)0)]
    public void ServeRequestShiftsAnOddRankAndZeroesAnEvenOne(byte before, byte after)
    {
        var flag = new Flag(4, 5);
        flag.AskFor(RequestWord.Second, Planks);
        flag.RankRequest(RequestWord.Second, before);

        flag.ServeRequest(RequestWord.Second);

        Assert.AreEqual(after, flag.RequestOf(RequestWord.Second).Rank);
    }

    [TestMethod]
    public void ClearCarrierRetryClearsTheFailure()
    {
        var flag = new Flag(4, 5);
        flag.RecordCarrierRequest(failed: true);

        flag.ClearCarrierRetry();

        Assert.IsFalse(flag.CarrierRequestFailed);
    }

    [TestMethod]
    public void RecordCarrierRequestSetsAndClearsTheFailure()
    {
        var flag = new Flag(4, 5);

        flag.RecordCarrierRequest(failed: true);
        var set = flag.CarrierRequestFailed;
        flag.RecordCarrierRequest(failed: false);

        Assert.IsTrue(set);
        Assert.IsFalse(flag.CarrierRequestFailed);
    }

    [TestMethod]
    public void TheGoodsTurnIsMarkedDueAndCleared()
    {
        var flag = new Flag(4, 5);

        flag.MarkGoodsTurnDue();
        var due = flag.NeedsGoodsTurn;
        flag.ClearGoodsTurn();

        Assert.IsTrue(due);
        Assert.IsFalse(flag.NeedsGoodsTurn);
    }

    [TestMethod]
    public void TakeRoadGivesTheFarDirectionAndClearsTheWaitingPlace()
    {
        var side = new FlagSide();
        side.Wait(3);

        side.TakeRoad(2);

        Assert.AreEqual(2, (int)side.FarDirection);
        Assert.IsNull(side.WaitingPlace);
    }

    [TestMethod]
    public void RerouteGivesTheFarDirectionAndDropsTheCarrierBitButKeepsTheWaitingPlace()
    {
        var side = new FlagSide();
        side.HoldCarrier();
        side.Wait(3);

        side.Reroute(4);

        Assert.AreEqual(4, (int)side.FarDirection);
        Assert.IsFalse(side.HoldsCarrier);
        Assert.AreEqual(3, side.WaitingPlace);
    }

    [TestMethod]
    public void CarrierEntersEndsTheWayAndCountsOne()
    {
        var side = new FlagSide();
        side.ExpectCarrier();

        side.CarrierEnters();

        Assert.IsFalse(side.CarrierOnTheWay);
        Assert.AreEqual(1, side.Carriers);
    }

    [TestMethod]
    public void CarrierLeavesAndKeepCarriersChangeTheCount()
    {
        var side = new FlagSide();
        side.KeepCarriers(2);

        side.CarrierLeaves();

        Assert.AreEqual(1, side.Carriers);
    }

    [TestMethod]
    public void EndRoadClearsTheWayAndTheCarrierBitButNotTheCount()
    {
        var side = new FlagSide();
        side.ExpectCarrier();
        side.HoldCarrier();
        side.KeepCarriers(2);

        side.EndRoad();

        Assert.IsFalse(side.CarrierOnTheWay);
        Assert.IsFalse(side.HoldsCarrier);
        Assert.AreEqual(2, side.Carriers);
    }

    [TestMethod]
    public void ForgetExpectedCarrierClearsTheWayOnly()
    {
        var side = new FlagSide();
        side.ExpectCarrier();
        side.HoldCarrier();

        side.ForgetExpectedCarrier();

        Assert.IsFalse(side.CarrierOnTheWay);
        Assert.IsTrue(side.HoldsCarrier);
    }

    [TestMethod]
    public void LayWritesGoodDestinationAndWay()
    {
        var place = new WaitingGood();

        place.Lay(Good.Log, 3, 2);

        Assert.AreEqual(Good.Log, place.Good);
        Assert.AreEqual(3, place.Destination);
        Assert.AreEqual(2, (int?)place.Way);
    }

    [TestMethod]
    public void ReplaceDropsTheWayAndKeepsTheGivenDestination()
    {
        var place = new WaitingGood();
        place.Lay(Good.Log, 3, 2);

        place.Replace(TestCatalog.GoodOf(9), 5);

        Assert.AreEqual(TestCatalog.GoodOf(9), place.Good);
        Assert.AreEqual(5, place.Destination);
        Assert.IsNull(place.Way);
    }

    [TestMethod]
    public void ClearEmptiesThePlaceAndKeepsTheDestination()
    {
        var place = new WaitingGood();
        place.Lay(Good.Log, 3, 2);

        place.Clear();

        Assert.IsTrue(place.IsEmpty);
        Assert.IsNull(place.Way);
        Assert.AreEqual(3, place.Destination);
    }

    [TestMethod]
    public void SendToAndDirectSetTheDestinationAndTheWayAlone()
    {
        var place = new WaitingGood();
        place.Lay(Good.Log, 3, 2);

        place.SendTo(null);
        place.Direct(4);

        Assert.IsNull(place.Destination);
        Assert.AreEqual(4, (int?)place.Way);
        Assert.AreEqual(Good.Log, place.Good);
    }
}
