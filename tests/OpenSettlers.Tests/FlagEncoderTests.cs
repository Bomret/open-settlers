using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        side.Length = new LengthClass(2);
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
                4 * 65536 + 5, 0, 0, 0, 0, 0, 0, 0,
                -65537, 0x80, 0, 0, 0, 1, 0, 0,
                2 * 65536 + 2, 0, 0, 0, 0, 0, 0, 0,
            },
            items
        );
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
        Assert.AreEqual(new LengthClass(expected), LengthClass.OfSteps(steps));
    }
}
