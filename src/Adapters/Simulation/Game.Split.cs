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
        var (first, second) = RoadBits(Map.At(index).Roads);
        var a = Gather(index, first);
        var b = Gather(index, second);

        // A carrier on its way to the road (marked at the end reached along the second direction) keeps the half
        // at the end flag it targets; the other end loses the mark. **Inferred:** with no such settler the end reached along the
        // second direction loses it.
        if (_flags[b.Far].Side(b.FarDirection).CarrierOnTheWay)
        {
            var onItsWay = _settlers.FirstOrDefault(settler =>
                HeadsTo(settler)
                && (
                    (settler.Target == a.Far && settler.Mode == a.FarDirection)
                    || (settler.Target == b.Far && settler.Mode == b.FarDirection)
                )
            );
            var loser = onItsWay?.Target == b.Far && onItsWay.Mode == b.FarDirection ? a : b;
            _flags[loser.Far].Side(loser.FarDirection).CarrierOnTheWay = false;
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
                if (_flags[slot].Side(direction) is { CarrierOnTheWay: false, Carriers: 0 })
                {
                    _roadCauses[half] = cause;
                }
            }
        }

        // The new flag, naming the far flags of the halves; each far flag, naming the new flag; the carriers on a half, naming
        // its two flags.
        TouchFlag(slot, EventType.SplitRoads, $"{History.FlagId(a.Far)} {History.FlagId(b.Far)}");
        foreach (var half in new[] { a, b })
        {
            TouchFlag(half.Far, EventType.SplitRoads, History.FlagId(slot).ToString());
            foreach (var carrierSlot in half.Settlers)
            {
                Touch(carrierSlot, EventType.SplitRoads, $"{History.FlagId(half.Far)} {History.FlagId(slot)}");
            }
        }

        _raze = null;
    }

    // 0x466719: the new flag's end of a half and the far flag's end. The new flag's end is cleared and takes the mark of a
    // carrier on its way from the far flag's end, which keeps only that mark; the half's length class goes into both; the
    // directions of the two ends name each other; the carriers on the half count on it (CountCarriers) up to the capacity of
    // its length class, one less where a carrier is on its way to it.
    private void LinkHalf(int flag, int direction, Walked half)
    {
        var (far, farDirection) = (half.Far, half.FarDirection);
        var side = _flags[flag].Side(direction);
        var farSide = _flags[far].Side(farDirection);
        var onItsWay = farSide.CarrierOnTheWay;
        var length = LengthClass.OfSteps(half.Steps);
        side.SetLength(length, onItsWay);
        farSide.SetLength(length, onItsWay);
        side.FarDirection = farDirection;
        farSide.FarDirection = direction;
        side.HoldsCarrier = false;
        farSide.HoldsCarrier = false;
        CountCarriers(
            half.Settlers,
            (flag, direction),
            (far, farDirection),
            _tables.RoadCapacity(length.Value) - (onItsWay ? 1 : 0)
        );
    }
}
