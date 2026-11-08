using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Writes the typed state of the flags as the bytes of the original's flag item (ADR 0013): the status byte, the road
/// byte and the direction byte of a side. The game's digest and the history's snapshots take their values from here, in the
/// order the original lays them out, so neither changes with the representation.
/// </summary>
public static class FlagEncoder
{
    /// <summary>
    /// The status byte of a flag's item (byte 5): bit 7 marks a request for a carrier that found none, until the retry
    /// scan clears it, and bit <c>d</c> a road in direction <c>d</c> that holds a carrier.
    /// </summary>
    public static byte Status(Flag flag)
    {
        var status = flag.CarrierRequestFailed ? 0x80 : 0;
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            status |= flag.Side(direction).HoldsCarrier ? 1 << direction : 0;
        }

        return (byte)status;
    }

    /// <summary>
    /// The byte of a flag's item for the road that leaves it by a side (byte 6 + direction): bits 4 to 6 the road's length
    /// class (0 to 7), bit 7 while a carrier is on the way to the road, and the low nibble the carriers on it.
    /// </summary>
    public static byte RoadByte(FlagSide side) =>
        (byte)((side.Length.Value << 4) | (side.CarrierOnTheWay ? 0x80 : 0) | (side.Carriers & 0xf));

    /// <summary>
    /// The byte of a flag's item at <c>0x3c</c> + direction: bit 7 a good waits to go that way and bits 0 to 2 its place,
    /// bits 3 to 5 the direction in which the road leaves the flag at its far end.
    /// </summary>
    public static byte DirectionByte(FlagSide side) =>
        (byte)((side.WaitingPlace is { } place ? 0x80 | place : 0) | (side.FarDirection.Value << 3));

    /// <summary>Byte 4 of a flag's item: bit 7 while goods on the flag wait for their next turn.</summary>
    public static byte GoodsByte(Flag flag) => (byte)(flag.NeedsGoodsTurn ? 0x80 : 0);

    /// <summary>
    /// The byte of a flag's item for a place (bytes <c>0xc</c> to <c>0x13</c>): 0 for an empty place, else the good in the
    /// low 5 bits and, in bits 5 to 7, the direction the good was given plus one.
    /// </summary>
    public static byte GoodByte(WaitingGood place) =>
        (byte)(place.Good.Code | (place.Way is { } way ? (way.Value + 1) << 5 : 0));

    /// <summary>
    /// The destination word of a place (flag item <c>+0x14</c> + 2 * place): the original numbers its flags from 1, so the
    /// word is the destination's slot plus one, and 0 for none.
    /// </summary>
    public static ushort DestinationWord(WaitingGood place) => (ushort)(place.Destination is { } slot ? slot + 1 : 0);

    /// <summary>
    /// The items the digest takes of each flag slot in turn after the settlers: the flag's turn bit as 1 or 0, the eight
    /// destination words of its places, and the six direction bytes.
    /// </summary>
    public static IEnumerable<long> Words(FlagTable flags)
    {
        foreach (var flag in flags)
        {
            yield return flag.NeedsGoodsTurn ? 1 : 0;
            foreach (var place in flag.Places)
            {
                yield return DestinationWord(place);
            }

            for (var direction = 0; direction < Direction.Count; direction++)
            {
                yield return DirectionByte(flag.Side(direction));
            }
        }
    }

    /// <summary>The items the digest takes at the end of the goods: the eight good bytes of each flag slot in turn.</summary>
    public static IEnumerable<long> Places(FlagTable flags) =>
        flags.SelectMany(flag => flag.Places, (_, place) => (long)GoodByte(place));

    /// <summary>
    /// The items the digest takes of each flag slot in turn, free slots included: the node as column times 65536 plus row
    /// (-65537 for a free slot), the status byte, and the six road bytes.
    /// </summary>
    public static IEnumerable<long> Items(FlagTable flags)
    {
        foreach (var flag in flags)
        {
            yield return flag.X * 65536L + flag.Y;
            yield return Status(flag);
            for (var direction = 0; direction < Direction.Count; direction++)
            {
                yield return RoadByte(flag.Side(direction));
            }
        }
    }
}
