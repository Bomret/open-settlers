using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenSettlers.Simulation;

// Taking down a flag, a road, or a building (docs/rules/removal.md): the click routines of the original, the check 0x44b6e6 that
// says what a node is, the flag's removal 0x462153, the road's take-down 0x462e1d with 0x463204, the building's 0x4617fd with the
// release of its goods 0x4312ed, and the states the settlers take (0x19 and 0x1c walk home, 0x44 and 0x45 for a carrier that
// waited). **Not ported:** what the player's own block holds (the per-player request words, the counts of the buildings), the
// storage item's request fields (0x4312ed's last loop), the territory a military building or the castle held, the boatmen of the
// water roads, and the click on the castle (the check gives it class 0, so the click is refused).
public sealed partial class Game
{
    // The class the program's table gives the object of a flag, and the owner bits of a node of player 0.
    private const int FlagObjectClass = 3;
    private const int PlayerOwner = 4 << 5;

    // The types whose item holds a storage (0x462c8d does not count a good against them): the castle and the type 0xa.
    private const int StorehouseType = 0xa;

    /// <summary>
    /// What the original's check <c>0x44b6e6</c> says a click on the node is, for the classes the removal acts on: 1 a flag the
    /// click refuses, 2 a flag it takes down, 3 a building, 4 a node of a road that is no flag; 0 for every other node (the check
    /// gives 5 to 7 for nodes where something may be built).
    /// </summary>
    internal int ClickClass(int x, int y) => ClickClass(Map.Index(x, y));

    /// <summary>
    /// The command a click of the remove tool on the node asks for: <see cref="CommandKind.RemoveFlag"/> for a flag the original
    /// takes down, <see cref="CommandKind.RemoveBuilding"/> for a building that stands, <see cref="CommandKind.RemoveRoad"/>
    /// for a node of a road, and null where the original's check refuses the click.
    /// </summary>
    public int? RemovalAt(int x, int y) =>
        ClickClass(x, y) switch
        {
            2 => CommandKind.RemoveFlag,
            3 => CommandKind.RemoveBuilding,
            4 => CommandKind.RemoveRoad,
            _ => null,
        };

    private int ClickClass(int index)
    {
        if (Castle is null || (Map.Layer(MapLayer.Height)[index] & 0xe0) != PlayerOwner)
        {
            return 0;
        }

        var flags = Map.Layer(MapLayer.Flags)[index];
        var objects = Map.Layer(MapLayer.Object);
        var objectClass = BuildSite.ObjectClass(_tables, objects[index]);
        if (objectClass == FlagObjectClass)
        {
            var (x, y) = Position(index);
            var (dx, dy) = RoadBuilding.Steps[4];
            // A flag with a building at its north-west (the bit 0x10) is refused; so is a flag with roads unless it has two,
            // each a road of land, to different flags.
            if ((flags & 0x10) != 0 && BuildSite.ObjectClass(_tables, objects[Map.Index(x + dx, y + dy)]) > 3)
            {
                return 1;
            }

            if ((flags & 0x3f) != 0)
            {
                var slot = Map.Layer(MapLayer.Resource)[index];
                var count = 0;
                var far = -1;
                for (var direction = 5; direction >= 0; direction--)
                {
                    if (RoadAt(slot, direction) is not { } road)
                    {
                        continue;
                    }

                    var other = FarEnd(road, slot, direction).Flag;
                    if (road.Water || (far >= 0 && far == other))
                    {
                        return 1;
                    }

                    count++;
                    far = other;
                }

                if (count != 2)
                {
                    return 1;
                }
            }

            return 2;
        }

        if (objectClass > 3)
        {
            return objectClass != 6 && BuildingAt(index) is { IsDemolishing: false } ? 3 : 0;
        }

        var roads = flags & 0x3f;

        return roads != 0 && roads != 0x02 && roads != 0x10 ? 4 : 0;
    }

    private (int X, int Y) Position(int index) => (index % Map.Size.Width, index / Map.Size.Width);

    // The node a step in a direction leads to.
    private int NodeAfter(int index, int direction)
    {
        var (x, y) = Position(index);
        var (dx, dy) = RoadBuilding.Steps[direction];

        return Map.Index(x + dx, y + dy);
    }

    // The direction of a settler's heading as the walk reads it: a negative heading marks a blocked step and gives the direction
    // less 6.
    private static int Back(Settler settler) => settler.Heading < 0 ? settler.Heading + 6 : settler.Heading;

    // Settlers whose target is a flag (the states 2, 15, and 5 or 7 on their way to state 2): 0x4312ed and 0x462153 send them
    // home when what they walk to is gone. The original also tests state 1, a settler that waits in the castle; its target is
    // 0 there, and no flag has slot 0 (the dummy of the flag table), so the test never hits. The engine's slot 0 is the castle's
    // flag, so the test is left out.
    private static bool HeadsTo(Settler settler) =>
        settler.State is 2 or 0xf || (settler.State is 5 or 7 && settler.Next == 2);

    private bool RemoveFlagAt(int x, int y)
    {
        var index = Map.Index(x, y);
        if (ClickClass(index) != 2)
        {
            return false;
        }

        TakeFlagDown(index);

        return true;
    }

    private bool RemoveBuildingAt(int x, int y)
    {
        var index = Map.Index(x, y);
        if (ClickClass(index) != 3 || BuildingAt(index) is not { } building)
        {
            return false;
        }

        TakeBuildingDown(building);

        return true;
    }

    private bool RemoveRoadAt(int x, int y)
    {
        var index = Map.Index(x, y);
        if (ClickClass(index) != 4)
        {
            return false;
        }

        var bits = Map.Layer(MapLayer.Flags)[index] & 0x3f;
        if (RoadThrough(index) is not { } road || (bits & (bits - 1)) == 0)
        {
            return false;
        }

        // The take-down walks from the node along its first two road bits (0x462e1d); a second bit found at 4 with bit 5 set
        // is taken for 5.
        var first = BitOperations.TrailingZeroCount(bits);
        var second = first + 1;
        while ((bits >> second & 1) == 0)
        {
            second++;
        }

        if (second == 4 && (bits & 0x20) != 0)
        {
            second = 5;
        }

        TakeRoadDown(index, first);
        TakeRoadDown(index, second);
        _roads.Remove(road);

        return true;
    }

    // The road that passes a node that holds no flag.
    private Road? RoadThrough(int index)
    {
        foreach (var road in _roads)
        {
            var node = Map.Index(_flags[road.FromSlot].X, _flags[road.FromSlot].Y);
            for (var step = 0; step < road.Directions.Count - 1; step++)
            {
                node = NodeAfter(node, road.Directions[step]);
                if (node == index)
                {
                    return road;
                }
            }
        }

        return null;
    }

    // 0x463204: the road's bits are taken from the node on in one direction until the flag at its end; the settlers on the way
    // are dismissed, and the flag at the end loses its road (EndOfRoad).
    private void TakeRoadDown(int index, int direction)
    {
        var flags = Map.Bytes(MapLayer.Flags);
        DismissWaiting(index);
        var standing = Occupant(index);
        if (standing != 0 && _settlers[standing].State is 2 or 3 && Back(_settlers[standing]) == direction)
        {
            Dismiss(_settlers[standing]);
        }

        flags[index] &= (byte)~(1 << direction);
        while (true)
        {
            index = NodeAfter(index, direction);
            var back = (direction + 3) % 6;
            flags[index] &= (byte)~(1 << back);
            var rest = flags[index];
            if ((rest & 0x80) != 0)
            {
                EndOfRoad(index, back);

                return;
            }

            if ((rest & 0x3f) == 0)
            {
                return;
            }

            direction = BitOperations.TrailingZeroCount(rest);
            DismissWaiting(index);
            standing = Occupant(index);
            if (standing != 0 && _settlers[standing].State is 2 or 3)
            {
                Dismiss(_settlers[standing]);
            }
        }
    }

    // The flag at the end of a road that was taken down (0x463204 from 0x46358c): a settler on the flag that came along the road
    // is dismissed; the flag's item loses the road's carrier bits, the settlers on their way to the road's end go home, and the
    // goods that were to go along the road lose that direction.
    private void EndOfRoad(int index, int back)
    {
        var slot = Map.Layer(MapLayer.Resource)[index];
        var standing = Occupant(index);
        if (standing != 0 && _settlers[standing].State is 0xe or 3 or 2 && Back(_settlers[standing]) == back)
        {
            Dismiss(_settlers[standing]);
        }

        var carrierOnTheWay = (_roadBytes[slot][back] & 0x80) != 0;
        _roadBytes[slot][back] &= 0x7f;
        _flagStatus[slot] &= (byte)~(1 << back);
        if (carrierOnTheWay)
        {
            foreach (var settler in _settlers.Where(HeadsTo))
            {
                if (settler.Target == slot && settler.Mode == back)
                {
                    settler.Mode = StorageWalk;
                    settler.Target = 0;
                }
            }
        }

        _directionBytes[slot][back] &= 0x78;
        var mark = (back + 1) << DirectionShift;
        for (var place = 7; place >= 0; place--)
        {
            var good = _flagGoods[slot][place];
            if (good != 0 && (good & 0xe0) == mark)
            {
                _flagGoods[slot][place] &= 0x1f;
                _goodsBit[slot] = true;
            }
        }
    }

    // 0x46385b (the boatman's state 0x1a is not ported): a settler on a road that goes is dismissed. One on its way to a building
    // at a flag frees the building's wait for a worker; one on its way to a road frees the road's carrier-on-the-way bits; one
    // that carries a good counts it lost. It then looks for the nearest flag on the way home (state 0x19).
    // **Inferred:** the carrier dismissed while it carries a good is read from the code; no emulator or debugger run reaches
    // this. Open lead in docs/milestones/m3-first-loop.md (a carrier carrying a good on a road that goes).
    private void Dismiss(Settler settler)
    {
        if (settler.State == 2)
        {
            var mode = (sbyte)settler.Mode;
            if (mode == -1)
            {
                if (BuildingOfFlag(settler.Target) is { } building)
                {
                    var onTheWay = (building.Status & 0x80) != 0;
                    building.Status &= 0x7f;
                    if (!onTheWay && (building.Fill & 0xff) != 0xff)
                    {
                        building.Fill = (ushort)((building.Fill & 0xff00) | ((building.Fill - 1) & 0xff));
                    }
                }
            }
            else if (mode is >= 0 and < 6)
            {
                _roadBytes[settler.Target][mode] &= 0x7f;
                if (RoadAt(settler.Target, mode) is { } road)
                {
                    var (far, farDirection) = FarEnd(road, settler.Target, mode);
                    _roadBytes[far][farDirection] &= 0x7f;
                }
            }
        }
        else if (settler.Mode != 0)
        {
            CountLost(settler.Mode, settler.Target);
        }

        settler.State = 0x19;
        settler.Mode = 0;
    }

    // 0x463ef9: the carriers that wait at a node next to a flag (the states 0x42, 0x43, 0x45) are told to go home (state 0x44).
    private void DismissWaiting(int index)
    {
        foreach (var settler in _settlers.Where(settler => Map.Index(settler.X, settler.Y) == index))
        {
            if (settler.State == 0x44)
            {
                return;
            }

            if (settler.State is 0x45 or 0x43 or 0x42)
            {
                settler.State = 0x44;

                return;
            }
        }
    }

    // 0x464008: a carrier that waits at a node next to a flag is taken with the settlers on the road (state 0x45); its slot, or -1.
    private int TakeWaiting(int index)
    {
        for (var slot = 0; slot < _settlers.Count; slot++)
        {
            var settler = _settlers[slot];
            if (Map.Index(settler.X, settler.Y) != index)
            {
                continue;
            }

            switch (settler.State)
            {
                case 0x42 or 0x43:
                    settler.State = 0x45;

                    return slot;
                case 0x45:
                    return slot;
                case 0x44:
                    return -1;
            }
        }

        return -1;
    }

    // State 0x44 (0x43007b): a dismissed carrier that waited takes the node it stands on where no settler stands and goes
    // home (state 0x19); else it tries again at its next turn.
    private void Dismissed(int slot, Settler settler)
    {
        var node = Map.Index(settler.X, settler.Y);
        if (Occupant(node) != 0)
        {
            return;
        }

        _occupant[node] = slot;
        settler.Stamp = _clock;
        settler.Timer = 0;
        settler.State = 0x19;
        settler.Mode = 0;
    }

    // State 0x45 (0x4300f1): a carrier that waited at the end of a road that was joined takes state 0x43 with the highest road
    // direction of its node as its heading, and runs that state at once.
    private void Rejoined(int slot, Settler settler)
    {
        settler.State = 0x43;
        var bits = Map.Layer(MapLayer.Flags)[Map.Index(settler.X, settler.Y)] & 0x3f;
        var direction = 5;
        while (direction >= 0 && (bits >> direction & 1) == 0)
        {
            direction--;
        }

        settler.Heading = direction;
        Claim(slot, settler);
    }

    // 0x462c8d: a good that is lost (carried by a settler who is dismissed, or lying on a flag that is taken down) is counted back.
    // The goods 14 and 15 count against the total of resource 1; a good that has a kind counts against the byte of the fill of
    // the building at its destination flag, except the buildings with a storage. **Not ported:** a destination flag with no
    // building, where the original reads a null pointer.
    private void CountLost(int good, int destination)
    {
        good &= 0x1f;
        if (good is 0xe or 0xf)
        {
            Map.Resource1Total--;
        }

        var kind = _tables.GoodKinds[good];
        if (kind > 1 || destination == 0 || BuildingOfFlag(destination - 1) is not { } building)
        {
            return;
        }

        if (building.TypeIndex is Building.CastleType or StorehouseType)
        {
            return;
        }

        building.Fill =
            kind == 0
                ? (ushort)((building.Fill & 0xff00) | ((building.Fill - 1) & 0xff))
                : (ushort)((building.Fill & 0xff) | ((((building.Fill >> 8) - 1) & 0xff) << 8));
    }

    private sealed record Walked(int Steps, List<int> Settlers, int Far, int FarDirection);

    // 0x463a40: the walk along the road that leaves the flag in a direction, to the flag at its end. It counts the steps, takes
    // the settlers on the road (a carrier in state 3 that is not marked to go, at the first flag and at the far flag only when
    // it came along the road; a carrier that waits next to a flag takes state 0x45), and gives the far flag and the direction
    // the road leaves it in.
    private Walked Gather(int index, int direction)
    {
        var flags = Map.Layer(MapLayer.Flags);
        var settlers = new List<int>();
        DismissWaiting(index);
        var standing = Occupant(index);
        if (
            standing != 0
            && _settlers[standing] is { State: 3 } first
            && first.Next != 0xff
            && Back(first) == direction
        )
        {
            first.Next = 0;
            settlers.Add(standing);
        }

        var steps = 0;
        while (true)
        {
            steps++;
            index = NodeAfter(index, direction);
            var back = (direction + 3) % 6;
            var rest = flags[index] & ~(1 << back);
            if ((rest & 0x80) != 0)
            {
                standing = Occupant(index);
                if (
                    standing != 0
                    && _settlers[standing] is { State: 0xe or 3 } last
                    && (last.State == 0xe || last.Next != 0xff)
                    && Back(last) == back
                )
                {
                    last.Next = 0;
                    settlers.Add(standing);
                }

                return new Walked(steps, settlers, Map.Layer(MapLayer.Resource)[index], back);
            }

            direction = BitOperations.TrailingZeroCount(rest);
            if (TakeWaiting(index) is var waiting and >= 0)
            {
                settlers.Add(waiting);
            }

            standing = Occupant(index);
            if (standing != 0 && _settlers[standing] is { State: 3 } passing && passing.Next != 0xff)
            {
                passing.Next = 0;
                settlers.Add(standing);
            }
        }
    }

    // The directions of a road walked from one of its flags, the road leaving that flag in a direction.
    private static List<int> PathFrom(Road road, int flag, int direction) =>
        road.FromSlot == flag && road.FromDirection == direction
            ? [.. road.Directions]
            : [.. road.Directions.Reverse().Select(step => (step + 3) % 6)];

    // 0x462153: a flag is taken down. A flag with roads (the check allows two) joins its two roads into one between the flags at
    // their far ends; the flag then goes as a flag with no road does: the node loses its flag bit, object, and link, the
    // settlers on their way to it go home, the goods that lie on it are counted lost, and its slot is free.
    private void TakeFlagDown(int index)
    {
        var flags = Map.Bytes(MapLayer.Flags);
        var slot = Map.Layer(MapLayer.Resource)[index];
        var standing = Occupant(index);
        if (standing != 0)
        {
            var settler = _settlers[standing];
            if (settler.State is 0x4a or 7 or 5)
            {
                settler.Next = 0x19;
            }
            else if (settler.State == 2 && (flags[index] & 0x3f) == 0)
            {
                settler.State = 0x19;
            }
        }

        if ((flags[index] & 0x3f) != 0)
        {
            JoinRoads(index, slot);
        }

        flags[index] &= 0x7f;
        foreach (var settler in _settlers.Where(HeadsTo))
        {
            if (settler.Target == slot)
            {
                settler.Mode = StorageWalk;
                settler.Target = 0;
            }
        }

        Map.Bytes(MapLayer.Object)[index] &= 0x80;
        Map.Bytes(MapLayer.Resource)[index] = 0;
        for (var place = 7; place >= 0; place--)
        {
            if (_flagGoods[slot][place] != 0)
            {
                CountLost(_flagGoods[slot][place], _goodsDestination[slot][place]);
            }
        }

        FreeFlagSlot(slot);
    }

    // The join of the two roads of a flag (0x462153): one road of the summed length between the flags at the far ends. The
    // settlers on the old roads count on the new one up to the number its length class holds (the table 0x462acd); the others
    // are marked to go (the next state 0xff) and a good they carry is lost.
    // **Inferred:** the case of more settlers than the length class holds (the mark 0xff) is read from the code; no emulator or
    // debugger run reaches it. Open lead in docs/milestones/m3-first-loop.md (more settlers on a joined road than its class holds).
    private void JoinRoads(int index, int slot)
    {
        var bits = Map.Layer(MapLayer.Flags)[index] & 0x3f;
        var first = BitOperations.TrailingZeroCount(bits);
        var last = 5;
        while ((bits >> last & 1) == 0)
        {
            last--;
        }

        if (RoadAt(slot, first) is not { } roadA || RoadAt(slot, last) is not { } roadB)
        {
            return;
        }

        var a = Gather(index, first);
        var b = Gather(index, last);
        var path = PathFrom(roadA, slot, first).AsEnumerable().Reverse().Select(step => (step + 3) % 6).ToList();
        path.AddRange(PathFrom(roadB, slot, last));
        _roads.Remove(roadA);
        _roads.Remove(roadB);
        _roads.Add(new Road(a.Far, a.FarDirection, b.Far, b.FarDirection, path, false));

        _directionBytes[a.Far][a.FarDirection] = (byte)(
            (_directionBytes[a.Far][a.FarDirection] & 0xc7) | (b.FarDirection << 3)
        );
        _directionBytes[b.Far][b.FarDirection] = (byte)(
            (_directionBytes[b.Far][b.FarDirection] & 0xc7) | (a.FarDirection << 3)
        );
        _flagStatus[a.Far] &= (byte)~(1 << a.FarDirection);
        _flagStatus[b.Far] &= (byte)~(1 << b.FarDirection);
        var length = LengthClass(path.Count);
        _roadBytes[a.Far][a.FarDirection] = length;
        _roadBytes[b.Far][b.FarDirection] = length;
        var carriers = a.Settlers.Concat(b.Settlers).ToList();
        if (carriers.Count != 0)
        {
            _flagStatus[a.Far] |= (byte)(1 << a.FarDirection);
            _flagStatus[b.Far] |= (byte)(1 << b.FarDirection);
            var kept = carriers.Count;
            var capacity = _tables.RoadCapacities[length >> 4];
            if (capacity < carriers.Count)
            {
                kept = capacity;
                var excess = carriers.Count - capacity;
                foreach (var carrier in carriers.Select(carrierSlot => _settlers[carrierSlot]))
                {
                    if (carrier.State == 0x45)
                    {
                        carrier.State = 0x44;
                    }
                    else
                    {
                        carrier.Next = 0xff;
                        if (carrier.Mode != 0)
                        {
                            var good = carrier.Mode;
                            carrier.Mode = 0;
                            CountLost(good, carrier.Target);
                        }
                    }

                    if (--excess == 0)
                    {
                        break;
                    }
                }
            }

            _roadBytes[a.Far][a.FarDirection] |= (byte)kept;
            _roadBytes[b.Far][b.FarDirection] |= (byte)kept;
        }

        foreach (var settler in _settlers.Where(HeadsTo))
        {
            if (
                (settler.Target == a.Far && settler.Mode == a.FarDirection)
                || (settler.Target == b.Far && settler.Mode == b.FarDirection)
            )
            {
                settler.Mode = StorageWalk;
                settler.Target = 0;
            }
        }
    }

    // 0x4617fd for a building that is no military building and not the castle: the building is marked taken down (it burns for
    // 0x7ff clock units, Burn), its node and its flag lose the marks of the building, its worker walks home, the goods that wait
    // for it lose their destination (ReleaseGoodsFor), and its flag goes where no road leaves it.
    private void TakeBuildingDown(Building building)
    {
        var flags = Map.Bytes(MapLayer.Flags);
        var flag = _flags[building.Flag];
        var flagNode = Map.Index(flag.X, flag.Y);
        building.Status |= Building.Demolishing;
        flags[Map.Index(building.X, building.Y)] &= 0xfd;
        flags[flagNode] &= 0xef;
        building.Status &= ~0x18;
        building.Fill = 0;
        var worker = building.Worker;
        building.Worker = 0x7ff;
        building.Stamp = _clock;
        if ((building.Status & 0x40) != 0)
        {
            building.Status &= ~0x40;
            SendWorkerHome(worker);
        }

        ReleaseGoodsFor(building.Flag);
        if ((flags[flagNode] & 0x3f) == 0 && (Map.Layer(MapLayer.Object)[flagNode] & 0x7f) == 1)
        {
            TakeFlagDown(flagNode);
        }
    }

    // The worker inside a building that is taken down: a settler of the job 4 loses its job; it walks home from where it stands
    // (state 0x19 where it holds its node, else state 0x1c to claim it).
    private void SendWorkerHome(int slot)
    {
        var settler = _settlers[slot];
        if (settler.Job == BuilderJob)
        {
            settler.Job = 0;
            settler.Mode = 0;
        }

        settler.Timer = 0;
        if (Occupant(Map.Index(settler.X, settler.Y)) == slot)
        {
            settler.State = 0x19;
            settler.Mode = 0;
        }
        else
        {
            settler.State = 0x1c;
        }
    }

    // 0x4312ed: what waits for the building at a flag is released. A settler on its way to the flag (the mode below 0) goes home;
    // a carrier that carries a good there loses the destination; a good on any flag that was to go to the flag loses its
    // destination and its direction, and the flag gets its turn again. **Not ported:** the loop over the storage items.
    private void ReleaseGoodsFor(int flag)
    {
        foreach (var settler in _settlers)
        {
            if (HeadsTo(settler) && settler.Target == flag && (settler.Mode & 0x80) != 0)
            {
                settler.Mode = StorageWalk;
                settler.Target = 0;
            }
            else if (settler.State == 3 && settler.Mode != 0 && settler.Target == flag + 1)
            {
                settler.Target = 0;
            }
        }

        for (var holder = 0; holder < _flags.Count; holder++)
        {
            for (var place = 7; place >= 0; place--)
            {
                var good = _flagGoods[holder][place];
                if (good == 0 || _goodsDestination[holder][place] != flag + 1)
                {
                    continue;
                }

                _goodsDestination[holder][place] = NoDestination;
                _goodsBit[holder] = true;
                var way = good & 0xe0;
                if (way == 0)
                {
                    continue;
                }

                _flagGoods[holder][place] &= 0x1f;
                var best = 0;
                var bestPlace = -1;
                for (var other = 0; other < 8; other++)
                {
                    var candidate = _flagGoods[holder][other];
                    if (candidate != 0 && (candidate & 0xe0) == way && best < TransportPriority(candidate & 0x1f))
                    {
                        best = TransportPriority(candidate & 0x1f);
                        bestPlace = other;
                    }
                }

                var direction = (way >> DirectionShift) - 1;
                _directionBytes[holder][direction] &= 0x78;
                if (bestPlace >= 0)
                {
                    _directionBytes[holder][direction] |= (byte)(0x80 | bestPlace);
                }
            }
        }
    }

    // The building pass for a building that burns (0x42cb2d): the time since its last turn comes off the countdown of 0x7ff in
    // its worker word, and when that wraps the building is gone.
    private void Burn(int slot, Building building)
    {
        var elapsed = (ushort)(_clock - building.Stamp);
        building.Stamp = _clock;
        var left = (ushort)building.Worker;
        building.Worker = (ushort)(left - elapsed);
        if (left < elapsed)
        {
            FreeBuilding(slot, building);
        }
    }

    // 0x42cf9d for a building without a territory: the node loses its mark 0x40, object, and link, and the slot is free; the
    // table's count falls past the free slots at its end.
    private void FreeBuilding(int slot, Building building)
    {
        var node = Map.Index(building.X, building.Y);
        Map.Bytes(MapLayer.Flags)[node] &= 0xbf;
        Map.Bytes(MapLayer.Object)[node] &= 0x80;
        Map.Bytes(MapLayer.Resource)[node] = 0;
        _buildings[slot] = Building.Free();
        while (_buildings.Count > 0 && _buildings[^1].IsFree)
        {
            _buildings.RemoveAt(_buildings.Count - 1);
        }
    }
}
