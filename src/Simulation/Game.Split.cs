using System.Linq;

namespace OpenSettlers.Simulation;

// The split of a road by a flag put on one of its nodes (docs/rules/roads.md#a-flag-on-a-road): Road_SplitAtFlag 0x4662e2 with
// the walk Road_GatherSettlers 0x463a40 (Gather) and the join of the flag to each end flag, Road_LinkHalf 0x466719. It is the
// inverse of the join of a flag taken down (JoinRoads). **Not ported:** the boatmen of the water roads.
public sealed partial class Game
{
    // The flag in the slot has just been put on the node of a road. The road becomes two roads that meet at the flag, and
    // the settlers on it go to the half they stand on or walk to (Gather and the carrier on its way). The half that holds no
    // carrier asks the castle for one at the next flag pass of its flags (VisitFlag), with a request that takes the cause.
    private void SplitRoad(int index, int slot, Cause cause)
    {
        if (RoadThrough(index) is not { } road)
        {
            return;
        }

        _raze = cause;
        var (first, second) = RoadBits(Map.Layer(MapLayer.Flags)[index] & 0x3f);
        var a = Gather(index, first);
        var b = Gather(index, second);

        // A carrier on its way to the road (bit 7 of the road byte at the end reached along the second direction) keeps the half
        // at the end flag it targets; the other end loses the mark. **Inferred:** with no such settler the end reached along the
        // second direction loses it.
        if ((_roadBytes[b.Far][b.FarDirection] & 0x80) != 0)
        {
            var onItsWay = _settlers.FirstOrDefault(settler =>
                HeadsTo(settler)
                && (
                    (settler.Target == a.Far && settler.Mode == a.FarDirection)
                    || (settler.Target == b.Far && settler.Mode == b.FarDirection)
                )
            );
            var loser = onItsWay?.Target == b.Far && onItsWay.Mode == b.FarDirection ? a : b;
            _roadBytes[loser.Far][loser.FarDirection] &= 0x7f;
        }

        // The half on the side of the road's start, and the steps the road took before the node.
        var fromSide = a.Far == road.FromSlot && a.FarDirection == road.FromDirection;
        var (toStart, toEnd) = fromSide ? (a, b) : (b, a);
        var (startDirection, endDirection) = fromSide ? (first, second) : (second, first);
        var steps = toStart.Steps;
        _roads.Remove(road);
        var halves = new[]
        {
            new Road(
                road.FromSlot,
                road.FromDirection,
                slot,
                startDirection,
                [.. road.Directions.Take(steps)],
                road.Water
            ),
            new Road(slot, endDirection, road.ToSlot, road.ToDirection, [.. road.Directions.Skip(steps)], road.Water),
        };
        _roads.AddRange(halves);

        LinkHalf(slot, first, a);
        LinkHalf(slot, second, b);

        // A half that holds no carrier and none on its way is asked for at the next flag pass, with the cause of this command.
        _roadCauses.Remove(road);
        // The cause only attributes the request (INV-0007): a request is made with or without it.
        if (cause.IsCommand)
        {
            foreach (var (half, direction) in new[] { (halves[0], startDirection), (halves[1], endDirection) })
            {
                if ((_roadBytes[slot][direction] & 0x8f) == 0)
                {
                    _roadCauses[half] = cause;
                }
            }
        }

        // The new flag, naming the far flags of the halves; each far flag, naming the new flag; the carriers on a half, naming
        // its two flags.
        TouchFlag(slot, EventType.SplitRoads, $"{FlagId(a.Far)} {FlagId(b.Far)}");
        foreach (var half in new[] { a, b })
        {
            TouchFlag(half.Far, EventType.SplitRoads, FlagId(slot).ToString());
            foreach (var carrierSlot in half.Settlers)
            {
                Touch(carrierSlot, EventType.SplitRoads, $"{FlagId(half.Far)} {FlagId(slot)}");
            }
        }

        _raze = null;
    }

    // 0x466719: the new flag's end of a half and the far flag's end. The road byte of the new flag's end is cleared and takes
    // bit 7 from the far flag's byte, which keeps only that bit; the half's length class goes into both; the directions of the
    // two ends name each other; the carriers on the half count on it (CountCarriers) up to the capacity of its length class, one
    // less where a carrier is on its way to it.
    private void LinkHalf(int flag, int direction, Walked half)
    {
        var (far, farDirection) = (half.Far, half.FarDirection);
        var onItsWay = (_roadBytes[far][farDirection] & 0x80) != 0;
        var length = LengthClass(half.Steps);
        _roadBytes[flag][direction] = (byte)((_roadBytes[far][farDirection] & 0x80) | length);
        _roadBytes[far][farDirection] = (byte)((_roadBytes[far][farDirection] & 0x80) | length);
        _directionBytes[flag][direction] = (byte)((_directionBytes[flag][direction] & 0xc7) | (farDirection << 3));
        _directionBytes[far][farDirection] = (byte)((_directionBytes[far][farDirection] & 0xc7) | (direction << 3));
        _flagStatus[flag] &= (byte)~(1 << direction);
        _flagStatus[far] &= (byte)~(1 << farDirection);
        CountCarriers(
            half.Settlers,
            (flag, direction),
            (far, farDirection),
            _tables.RoadCapacities[length >> 4] - (onItsWay ? 1 : 0)
        );
    }
}
