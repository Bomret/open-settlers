using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The flag's typed state is written as the original's bytes at the edge (ADR 0013): the status byte, the road byte and the
// direction byte of a side, in the values the original's flag item holds.
[TestClass]
public sealed class FlagEncoderTests
{
    [TestMethod]
    public void AFlagWithNothingSetEncodesAsZeros()
    {
        var flag = new Flag(4, 5);

        Assert.AreEqual(0, FlagEncoder.Status(flag));
        Assert.IsTrue(Enumerable.Range(0, 6).All(d => FlagEncoder.RoadByte(flag.Side(d)) == 0));
        Assert.IsTrue(Enumerable.Range(0, 6).All(d => FlagEncoder.DirectionByte(flag.Side(d)) == 0));
    }

    [TestMethod]
    public void TheStatusHoldsTheFailedRequestInBit7AndTheRoadsWithACarrierInTheBitOfTheirDirection()
    {
        var flag = new Flag(4, 5);
        flag.CarrierRequestFailed = true;
        flag.Side(0).HoldsCarrier = true;
        flag.Side(5).HoldsCarrier = true;

        Assert.AreEqual(0x80 | 0x01 | 0x20, FlagEncoder.Status(flag));
    }

    [TestMethod]
    public void TheRoadByteHoldsTheLengthClassInBits4To6TheWayMarkInBit7AndTheCarriersInTheLowNibble()
    {
        var side = new Flag(4, 5).Side(2);
        side.Length = new LengthClass { Value = 2 };
        side.CarrierOnTheWay = true;
        side.Carriers = 3;

        Assert.AreEqual(0x20 | 0x80 | 3, FlagEncoder.RoadByte(side));
    }

    [TestMethod]
    public void TheDirectionByteHoldsThePlaceOfTheWaitingGoodInBit7AndBits0To2AndTheFarDirectionInBits3To5()
    {
        var waiting = new Flag(4, 5).Side(1);
        waiting.WaitingPlace = 5;
        waiting.FarDirection = 2;
        var none = new Flag(4, 5).Side(1);
        none.FarDirection = 3;

        Assert.AreEqual(0x80 | 5 | 2 << 3, FlagEncoder.DirectionByte(waiting));
        Assert.AreEqual(3 << 3, FlagEncoder.DirectionByte(none));
    }

    [TestMethod]
    public void AFlagsItemsAreItsNodeItsStatusAndItsSixRoadBytesAndAFreeSlotKeepsItsBytes()
    {
        var table = new FlagTable();
        table.Add(4, 5);
        table.Add(6, 7);
        table.Add(2, 2);
        table[1].Side(3).Carriers = 1;
        table[1].CarrierRequestFailed = true;
        table.Free(1);

        var items = FlagEncoder.Items(table).ToArray();

        CollectionAssert.AreEqual(
            new long[]
            {
                4 * 65536 + 5,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                -65537,
                0x80,
                0,
                0,
                0,
                1,
                0,
                0,
                2 * 65536 + 2,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
            },
            items
        );
    }

    [TestMethod]
    public void AGoodWithADirectionEncodesAsTheGoodInBits0To4AndTheDirectionPlusOneInBits5To7()
    {
        var place = new Flag(4, 5).Places[2];
        place.Good = new Good { Code = 7 };
        place.Way = 3;

        Assert.AreEqual(7 | 4 << 5, FlagEncoder.GoodByte(place));
    }

    [TestMethod]
    public void AGoodWithNoDirectionAndAnEmptyPlaceEncodeAsTheCodeAndZero()
    {
        var flag = new Flag(4, 5);
        flag.Places[0].Good = new Good { Code = 9 };

        Assert.AreEqual(9, FlagEncoder.GoodByte(flag.Places[0]));
        Assert.AreEqual(0, FlagEncoder.GoodByte(flag.Places[1]));
    }

    [TestMethod]
    public void ADestinationEncodesAsTheSlotPlusOneAndNoDestinationAsZero()
    {
        var places = new Flag(4, 5).Places;
        places[0].Destination = 0;
        places[1].Destination = 3;

        Assert.AreEqual(1, FlagEncoder.DestinationWord(places[0]));
        Assert.AreEqual(4, FlagEncoder.DestinationWord(places[1]));
        Assert.AreEqual(0, FlagEncoder.DestinationWord(places[2]));
    }

    [TestMethod]
    public void AnEmptyPlaceKeepsTheDestinationOfItsLastGoodInTheEncoding()
    {
        var place = new Flag(4, 5).Places[4];
        place.Good = new Good { Code = 7 };
        place.Destination = 0;

        place.Good = Good.None;

        Assert.AreEqual(0, FlagEncoder.GoodByte(place));
        Assert.AreEqual(1, FlagEncoder.DestinationWord(place));
    }

    [TestMethod]
    public void TheGoodsByteHasBit7WhileGoodsWaitForTheirTurn()
    {
        var flag = new Flag(4, 5);

        Assert.AreEqual(0, FlagEncoder.GoodsByte(flag));

        flag.NeedsGoodsTurn = true;

        Assert.AreEqual(0x80, FlagEncoder.GoodsByte(flag));
    }

    [TestMethod]
    public void ThePlaceOfAFlagIsTheFirstEmptyOneAndNoneWhenAllHoldAGood()
    {
        var flag = new Flag(4, 5);
        flag.Places[0].Good = new Good { Code = 7 };
        flag.Places[1].Good = new Good { Code = 7 };

        Assert.AreEqual(2, flag.FirstEmptyPlace());

        foreach (var place in flag.Places)
        {
            place.Good = new Good { Code = 7 };
        }

        Assert.AreEqual(-1, flag.FirstEmptyPlace());
    }

    [TestMethod]
    public void TheDigestWordsOfAFlagAreItsTurnBitItsEightDestinationWordsAndItsSixDirectionBytes()
    {
        var table = new FlagTable();
        table.Add(4, 5);
        table[0].NeedsGoodsTurn = true;
        table[0].Places[1].Destination = 0;
        table[0].Side(2).WaitingPlace = 1;
        table[0].Side(2).FarDirection = 5;

        CollectionAssert.AreEqual(
            new long[] { 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0x80 | 1 | 5 << 3, 0, 0, 0 },
            FlagEncoder.Words(table).ToArray()
        );
    }

    [TestMethod]
    public void TheDigestRequestsOfAFlagAreTheClassAndRankOfEachOfItsTwoRequestWords()
    {
        var table = new FlagTable();
        table.Add(4, 5);
        table[0]
            .SetRequest(
                RequestWord.First,
                new Request
                {
                    Asks = new GoodClass { Class = 3, Word = RequestWord.First },
                    Rank = 0x3e,
                }
            );
        table[0]
            .SetRequest(
                RequestWord.Second,
                new Request
                {
                    Asks = new GoodClass { Class = 0, Word = RequestWord.Second },
                    Rank = 7,
                }
            );
        table.Add(6, 5);

        CollectionAssert.AreEqual(
            new long[] { 3, 0x3e, 0, 7, -1, 0, -1, 0 },
            FlagEncoder.Requests(table).ToArray(),
            "a class of 0 differs from no class"
        );
    }

    [TestMethod]
    public void TheDigestPlacesOfAFlagAreTheEightGoodBytes()
    {
        var table = new FlagTable();
        table.Add(4, 5);
        table[0].Places[0].Good = new Good { Code = 9 };
        table[0].Places[7].Good = new Good { Code = 7 };
        table[0].Places[7].Way = 0;

        CollectionAssert.AreEqual(new long[] { 9, 0, 0, 0, 0, 0, 0, 7 | 1 << 5 }, FlagEncoder.Places(table).ToArray());
    }

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(3, 0)]
    [DataRow(4, 1)]
    [DataRow(5, 1)]
    [DataRow(6, 2)]
    [DataRow(7, 3)]
    [DataRow(9, 3)]
    [DataRow(10, 4)]
    [DataRow(12, 4)]
    [DataRow(13, 5)]
    [DataRow(17, 5)]
    [DataRow(18, 6)]
    [DataRow(23, 6)]
    [DataRow(24, 7)]
    [DataRow(60, 7)]
    public void ARoadOfSomeStepsIsOfALengthClass(int steps, int expected)
    {
        Assert.AreEqual(new LengthClass { Value = expected }, LengthClass.OfSteps(steps));
    }
}
