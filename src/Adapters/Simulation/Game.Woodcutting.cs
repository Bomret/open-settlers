using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

// The woodcutter's work cycle and the map's object pass (docs/rules/woodcutting.md): the worker at its hut picks a node
// of the first 128 around the hut with a random draw until a tree stands on it, walks there, cuts the tree, walks back
// to the hut's flag with a log, lays the log on the flag, and goes into the hut to start again. The object pass walks a
// cursor over the map and turns a felled tree into nothing in the time the original takes.
public sealed partial class Game
{
    // The trees are the object codes 8 to 0x17 (0x438c11, 0x437f8f; MapObjectKind.Tree), and the class table calls them
    // class 1 (docs/rules/build-sites.md). The trees of the codes 8 to 15 take the second cutting variant (the animation
    // with the byte 13 set).

    // The good of a log (the byte 7 the settler writes onto the flag, 0x438011).
    internal const int GoodLog = 7;

    // The objects the object pass handles: the cut writes the falling tree of the codes 0x5d to 0x66, the pass turns each of
    // them into the stump 0x53, and the stump vanishes by chance (0x428eb8).

    // The walk's remaining vector lives in the bytes of the settler item that the road walk uses otherwise: the east
    // steps in byte 11 (Settler.Mode), the south steps in byte 12 (the low byte of Settler.Target), byte 13 (the high
    // byte of Target) is the way-back mark, and byte 14 (Settler.Heading) the way-back's south steps.
    private static int StepsEast(Settler settler) => (sbyte)settler.Mode;

    private static int StepsSouth(Settler settler) => (sbyte)(settler.Target & 0xff);

    private static int Mark(Settler settler) => settler.Target >> 8;

    private static void SetVector(Settler settler, int east, int south)
    {
        settler.Mode = east & 0xff;
        settler.Target = (settler.Target & 0xff00) | (south & 0xff);
    }

    private static void SetMark(Settler settler, int mark) =>
        settler.Target = (settler.Target & 0xff) | ((mark & 0xff) << 8);

    // The game's random generator: it continues from the map's generation, as the original's does.
    private RandomGenerator _random;

    /// <summary>The game's random generator: it starts as the map's generation left it and draws only in the passes.</summary>
    internal RandomGenerator Random => _random;

    // The object pass (0x428d10): the clock at its last run, its countdown (a debt that wraps while 20 units are due),
    // and the cursor's column and row.
    private ushort _objectStamp;
    private ushort _objectTimer;
    private int _objectX;
    private int _objectY;

    // The draw of the game's generator: the new first word, which Rng_Step (0x466ade) and the draw at 0x44257a leave.
    private ushort Draw()
    {
        _random = _random.Step();

        return _random.W0;
    }

    // State 4, a settler that stepped through a door (0x43c96b): once its timer is down to the time the door leaves it, it
    // runs the routine of its job when a building stands there that is not marked bit 5; else it is state 0x19.
    private void Door(int slot, Settler settler)
    {
        var delta = (ushort)(_clock - settler.Stamp);
        settler.Stamp = _clock;
        var old = settler.Timer;
        settler.Timer = (ushort)(old - delta);
        if (delta <= old && (short)((ushort)settler.Target - settler.Timer) < 0)
        {
            RecordWork(slot, EventType.Door);

            return;
        }

        var node = Map.Index(settler.X, settler.Y);
        var building = BuildingAt(node);
        if (building is not null && (building.Status & 0x20) == 0)
        {
            settler.Timer = (ushort)settler.Target;
            // Mode is byte 11, which the free walk also holds the steps east in (StepsEast): at the door the walk's vector is
            // 0, so a value of 0xfe is the original's send-home mark and no walk's -2 east. The original shares the byte.
            if (settler.Mode == StorageWalk)
            {
                // The timer and stamp change is this rule's; EnterStorage records its own.
                RecordWork(slot, EventType.Door);
                EnterStorage(slot, node, settler);

                return;
            }

            if (settler.Job == WoodcutterJob)
            {
                // 0x43cf15: the woodcutter leaves the hut's node and looks for a tree.
                _occupant.Remove(node);
                settler.State = 0x12;
            }
        }
        else
        {
            settler.State = 0x19;
            settler.Mode = 0;
            settler.Timer = 0;
        }

        RecordWork(slot, EventType.Door);
    }

    // Records the change a rule of this file made to the settler in the slot (ADR 0011): its state now, the rule's type, and the
    // rule's area where the settler carries no command cause: the layout while one builds, the walk home for a settler on it
    // (a woodcutter walking home included), woodcutting for another woodcutter, and the settlers' rules for another job that
    // passes through these states.
    private void RecordWork(int slot, string type, string? other = null) =>
        History.RecordSettler(
            slot,
            type,
            InLayout || _walkingHome.Contains(slot) || _settlers[slot].Job != WoodcutterJob
                ? SettlerCause(slot)
                : Cause.Woodcutting,
            other
        );

    // State 0x12, the woodcutter's search (0x438b8c). When its timer has run out it draws a node of the spiral around the
    // hut, entries 1 to 128, until a tree stands on one; each node without a tree adds 400 to the timer, and the search
    // ends for this turn when that wraps the timer.
    private void SearchTree(int slot, Settler settler)
    {
        var found = SearchTurn(settler);
        RecordWork(slot, EventType.SearchTree);
        if (found)
        {
            StepOut(slot, settler);
        }
    }

    // One turn of the search; true where it found a tree and took state 7.
    private bool SearchTurn(Settler settler)
    {
        if (!TimerRan(settler))
        {
            return false;
        }

        while (true)
        {
            var entry = ((Draw() & 0x1fc) + 4) >> 2;
            var (dx, dy) = MapGenerator.SpiralOffsets[entry];
            var x = (settler.X + dx) & (Map.Size.Width - 1);
            var y = (settler.Y + dy) & (Map.Size.Height - 1);
            if (Map.At(x, y).Object.Kind == MapObjectKind.Tree)
            {
                // The vector to the tree is taken from the flag the first step reaches: one less each way.
                SetVector(settler, dx - 1, dy - 1);
                SetMark(settler, -(dx - 1));
                settler.Heading = (sbyte)-(dy - 1);
                settler.Next = 0x10;
                settler.State = 7;

                return true;
            }

            var old = settler.Timer;
            settler.Timer = (ushort)(old + 400);
            if (old > 0xfe6f)
            {
                return false;
            }
        }
    }

    // State 7, the step from the hut onto its flag (0x43e036): south-east, when the settler's node and that node are free
    // of other settlers; the time of the step is scaled by the door's speed, and the settler waits it out in state 5.
    private void StepOut(int slot, Settler settler)
    {
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, next) = Neighbour(settler, 1);
        var standing = Occupant(here);
        if ((standing == slot || standing == 0) && Occupant(next) == 0)
        {
            var door = BuildingAt(here);
            var shape = 0xd + Height(next) - Height(here);
            Move(slot, settler, x, y, next);
            settler.Frame = (byte)shape;
            var speed = (byte)(_tables.DoorSpeed(door?.TypeIndex ?? 0) ^ 0x1f);
            settler.Timer = (ushort)((ushort)(_tables.StepTime(shape) * speed) >> 5);
            settler.Stamp = _clock;
            settler.State = 5;
            RecordWork(slot, EventType.StepOut);

            return;
        }

        settler.Frame = 0x52;
        settler.Timer = 0;
        RecordWork(slot, EventType.StepOut);
    }

    // State 0x10, the free walk along a vector (0x436c38). Byte 15 holds the walk's memory: with bit 3 set and the low three
    // bits 0 the vector is used up and the settler has arrived (0x437ddd); the low three bits otherwise name the last
    // step's direction plus one, and bits 4 to 7 a debt of turns. Each step picks a direction (Choose), steps, takes the step's
    // time into the timer, and goes on while that does not wrap it.
    // **Not ported:** the passing of settlers when every direction is taken (the settler waits 0x7f units with the
    // frame 0x52 as it does when a blocked step is tried again), and the checks of a target node marked solid.
    private void FreeWalk(int slot, Settler settler)
    {
        var arrived = FreeWalkTurn(slot, settler);
        RecordWork(slot, EventType.FreeWalk);
        if (arrived)
        {
            Arrive(slot, settler);
        }
    }

    // One turn of the free walk; true where the vector is used up and the settler has arrived.
    private bool FreeWalkTurn(int slot, Settler settler)
    {
        if (!TimerRan(settler))
        {
            return false;
        }

        while (true)
        {
            var memory = settler.Next;
            if ((memory & 8) != 0 && (memory & 7) == 0)
            {
                return true;
            }

            var direction = Choose(settler, (memory & 8) != 0 ? (memory & 7) + 5 : (memory & 7) - 1);
            if (direction < 0)
            {
                settler.Frame = 0x52;
                settler.Timer = 0x7f;

                return false;
            }

            var (x, y, next) = Neighbour(settler, direction);
            var east = StepsEast(settler);
            var south = StepsSouth(settler);
            (east, south) = direction switch
            {
                0 => (east - 1, south),
                1 => (east - 1, south - 1),
                2 => (east, south - 1),
                3 => (east + 1, south),
                4 => (east + 1, south + 1),
                _ => (east, south + 1),
            };
            SetVector(settler, east, south);
            if ((sbyte)east == 0 && (sbyte)south == 0)
            {
                settler.Next = 8;
            }

            var here = Map.Index(settler.X, settler.Y);
            var shape = 4 + 9 * direction + Height(next) - Height(here);
            Move(slot, settler, x, y, next);
            var old = settler.Timer;
            settler.Timer = (ushort)(old + _tables.StepTime(shape));
            if (settler.Timer < old)
            {
                settler.Frame = (byte)shape;

                return false;
            }
        }
    }

    // A node the walk may step onto: not marked solid (the lake mark) and holding no settler.
    private bool Free(int index) => !Map.At(index).LakeMark && Occupant(index) == 0;

    // The direction a row entry names (0 east to 5 north, -1 for an entry that names none: a table with no row) where the
    // step it leads to is free; -1 for such an entry and for a node that is taken.
    private int Candidate(Settler settler, int direction) =>
        direction >= 0 && Free(Neighbour(settler, direction).Index) ? direction : -1;

    // The direction of the next step (0 to 5) or -1 where all are taken, and the memory it leaves in byte 15. A settler with a
    // memory (row 0 or more: the low bits less one, or plus five with bit 3) tries the six directions of that row of the
    // table (0x436e96): the candidate k costs 2, 1, 0, -1, -2, or -3 turns of the debt in bits 4 to 7, a debt that would
    // run out takes the step with the memory cleared, one that would overflow starts again from the vector. Else the
    // first direction of the vector's heading class is tried, and then the other five (0x4375ee) with a memory of the
    // step, the parity of the class and the candidate, and half of 6 less the candidate's counter as the debt.
    private int Choose(Settler settler, int memoryRow)
    {
        if (memoryRow >= 0)
        {
            for (var candidate = 0; candidate < 6; candidate++)
            {
                var entry = _tables.WalkDirection(memoryRow, candidate);
                var direction = Candidate(settler, entry);
                if (direction < 0)
                {
                    continue;
                }

                var delta = 2 - candidate;
                if (delta < 0)
                {
                    var sum = settler.Next + (-delta << 4);
                    if (sum > 0xff)
                    {
                        settler.Next = 0;

                        return Fresh(settler);
                    }

                    settler.Next = sum;
                }
                else if (delta > 0)
                {
                    if (settler.Next < delta << 4)
                    {
                        settler.Next = 0;

                        return direction;
                    }

                    settler.Next -= delta << 4;
                }

                settler.Next = (settler.Next & 0xf8) | (direction + 1);

                return direction;
            }

            settler.Next &= 0xf0;

            return -1;
        }

        return Fresh(settler);
    }

    // The choice from the vector's heading class (0x437072).
    private int Fresh(Settler settler)
    {
        var heading = HeadingClass(StepsEast(settler), StepsSouth(settler));
        var first = Candidate(settler, _tables.WalkDirection(heading, 0));
        if (first >= 0)
        {
            return first;
        }

        for (var candidate = 1; candidate < 6; candidate++)
        {
            var direction = Candidate(settler, _tables.WalkDirection(heading, candidate));
            if (direction < 0)
            {
                continue;
            }

            var counter = 5 - candidate;
            var parity = ((heading ^ counter) & 1) != 0 ? 8 : 0;
            settler.Next = direction + 1 + parity + ((6 - counter) >> 1 << 4);

            return direction;
        }

        settler.Next &= 0xf0;

        return -1;
    }

    // The class of the vector (east, south) left to walk, which picks the row of directions (0x437072): 12 for a vector
    // that points west and north, 18 for one that points east and south, plus 1 to 5 for how flat it is.
    private static int HeadingClass(int east, int south)
    {
        var hint = 0xc;
        if (east < 0)
        {
            var west = (byte)-east;
            if (south < 0)
            {
                var north = (byte)-south;
                if (north < west)
                {
                    hint += 2;
                    if ((byte)(north + north) < west)
                    {
                        hint++;
                    }
                }
                else if (north < (byte)(west + west))
                {
                    hint++;
                }
            }
            else
            {
                hint += 4;
                if (south >= west)
                {
                    hint++;
                }
            }

            return hint;
        }

        hint += 6;
        if (south < 0)
        {
            hint += 4;
            if ((byte)-south >= east)
            {
                hint++;
            }
        }
        else if (south >= east)
        {
            if (south < (byte)(east + east))
            {
                hint++;
            }
        }
        else
        {
            hint += 2;
            if ((byte)(south + south) < east)
            {
                hint++;
            }
        }

        return hint;
    }

    // The walk has used up its vector (0x437ddd, the woodcutter's branch at 0x437f53). On the tree's node the settler takes
    // the way back as its vector and starts cutting where the tree still stands; else it turns back at once. At the
    // flag on the way back it lays the log it carries onto the flag and goes into the hut.
    private void Arrive(int slot, Settler settler)
    {
        if (settler.Job != WoodcutterJob)
        {
            // **Not ported:** the arrival of the jobs 7, 8, 11, 14, and 20 where they are not walking home (0x437ddd).
            if (ArrivesHome(settler.Job) || (Mark(settler) == 0x80 && settler.Heading < 0))
            {
                ArriveHome(slot, settler);
                RecordWork(slot, EventType.Arrive);
            }

            return;
        }

        if (Mark(settler) == 0x80)
        {
            // A negative count marks the walk home (0x4385ab).
            if (settler.Heading < 0)
            {
                ArriveHome(slot, settler);
                RecordWork(slot, EventType.Arrive);

                return;
            }

            if (settler.Heading != 0)
            {
                PutGood(slot, settler, GoodLog);
            }

            settler.Mode = 0;
            EnterHut(slot, settler);

            return;
        }

        settler.Mode = Mark(settler);
        settler.Target = (settler.Target & 0xff00) | (settler.Heading & 0xff);
        var standing = Map.At(settler.X, settler.Y).Object;
        if (standing.Kind == MapObjectKind.Tree)
        {
            SetMark(settler, standing.HasSecondCutVariant ? 0xff : 0);
            settler.Heading = 0;
            settler.Frame = 0x74;
            settler.Timer = 0x3ff;
            settler.State = 0x11;
            RecordWork(slot, EventType.Arrive);

            return;
        }

        SetMark(settler, 0x80);
        settler.Heading = 0;
        settler.Next = 0;
        settler.Timer = 0;
        RecordWork(slot, EventType.Arrive);
    }

    // State 0x11, the cut (0x438a9e): each time the timer runs out the tree takes the next of its falling codes (0x5d to
    // 0x66, the second variant after 0x61 for the codes 8 to 15), the frame is 0x74 plus the count and the timer takes
    // the time that frame's table word gives. After the fifth the settler starts the way back with the log.
    private void Cut(Settler settler)
    {
        var slot = _settlers.IndexOf(settler);
        var tree = ThingId.OfObject("tree", settler.X, settler.Y).ToString();
        CutTurn(settler);
        RecordWork(slot, EventType.CutTree, tree);
    }

    private void CutTurn(Settler settler)
    {
        if (!TimerRan(settler))
        {
            return;
        }

        ref var node = ref Map.At(settler.X, settler.Y);
        while (true)
        {
            settler.Heading = (sbyte)(settler.Heading + 1);
            var count = settler.Heading;
            node.Object = MapObject.Falling(count, Mark(settler) != 0);
            if (count == 5)
            {
                break;
            }

            settler.Frame = (byte)(count + 0x74);
            var old = settler.Timer;
            settler.Timer = (ushort)(old + _tables.StepTime(settler.Frame));
            if (settler.Timer < old)
            {
                return;
            }
        }

        SetMark(settler, 0x80);
        settler.Heading = 1;
        settler.Next = 0;
        settler.State = 0x10;
        settler.Timer = 0;
    }

    // The settler lays a good on the first empty place of the flag it stands on (0x436b46). The flag changes and records; the
    // settler does not.
    private void PutGood(int slot, Settler settler, int good)
    {
        var flag = Map.At(settler.X, settler.Y).LinkedSlot;
        var place = _flags[flag].FirstEmptyPlace();
        if (place >= 0)
        {
            LayGood(flag, place, good);
            History.RecordFlag(flag, EventType.PutGood, Cause.Woodcutting, History.SettlerId(slot).ToString());
        }
    }

    // The step from a flag into the building north-west of it (0x43c7c2, and the turn of state 6): the settler takes the
    // node when no settler stands on it, with the time of the step and the part of it the door's speed leaves, and is in
    // state 4; else it waits in state 6.
    private void EnterHut(int slot, Settler settler)
    {
        settler.State = 6;
        var here = Map.Index(settler.X, settler.Y);
        var (x, y, node) = Neighbour(settler, 4);
        if (Occupant(node) != 0)
        {
            settler.Frame = 0x55;
            settler.Timer = 0;
            RecordWork(slot, EventType.EnterHut);

            return;
        }

        var shape = 0x28 + Height(node) - Height(here);
        Move(slot, settler, x, y, node);
        settler.Frame = (byte)shape;
        var time = _tables.StepTime(shape);
        settler.Timer = time;
        settler.Target = (ushort)(time * _tables.DoorSpeed(BuildingAt(node)?.TypeIndex ?? 0)) >> 5;
        settler.Stamp = _clock;
        settler.State = 4;
        RecordWork(slot, EventType.EnterHut);
    }

    // The map's object pass (0x428d10, called by the main loop's 0x4286d8 each pass). Each 20 clock units it takes the next
    // nodes of a cursor that moves 23 columns on and a row down where it wraps: on a lake node with detail it may move the
    // detail (the draws of the original), a node with an object code of 0x53 or more takes its handler: the cut tree codes
    // 0x5d to 0x66 become 0x53, and 0x53 vanishes when the draw's low two bits are 0. **Not ported:** the handlers of the codes
    // 0x67 and up (the original's growth and decay; the generation places none).
    private void ObjectPass()
    {
        var delta = (ushort)(_clock - _objectStamp);
        _objectStamp = _clock;
        var timer = _objectTimer;
        _objectTimer = (ushort)(timer - delta);
        if (delta <= timer)
        {
            return;
        }

        var count = 0;
        ushort due;
        do
        {
            count += (Map.Size.Width >> 5) * (Map.Size.Height >> 5);
            due = _objectTimer;
            _objectTimer = (ushort)(due + 20);
        } while (due < 0xffec);

        for (; count > 0; count--)
        {
            _objectX += 23;
            if (_objectX >= Map.Size.Width)
            {
                _objectX -= Map.Size.Width;
                _objectY = (_objectY + 1) & (Map.Size.Height - 1);
            }

            var node = Map.Index(_objectX, _objectY);
            ref var at = ref Map.At(node);
            if (at.WaterTriangle && at.LakeMark && at.LakeDetail != 0)
            {
                MoveLakeDetail(node);
            }

            var kind = at.Object.Kind;
            if (kind == MapObjectKind.Stump)
            {
                if ((Draw() & 3) == 0)
                {
                    at.Object = MapObject.None;
                }
            }
            else if (kind == MapObjectKind.FallingTree)
            {
                at.Object = MapObject.Stump;
            }
        }
    }

    // The lake detail of a node (0x428d6e): one draw; below 10 the detail may grow by one (when the draw's bits 8 to 13 are
    // 0), and it moves one to a neighbour that is a lake node too (south-east, south, north-west, or north by the draw's
    // bits 2 and 3).
    private void MoveLakeDetail(int node)
    {
        var draw = Draw();
        ref var at = ref Map.At(node);
        if ((sbyte)(at.LakeDetail - 10) >= 0)
        {
            return;
        }

        if ((draw & 0x3f00) == 0)
        {
            at.LakeDetail++;
        }

        var direction = (draw & 0xc) switch
        {
            0 => 1,
            4 => 2,
            8 => 4,
            _ => 5,
        };
        var (dx, dy) = RoadBuilding.Steps[direction];
        ref var neighbour = ref Map.At(node % Map.Size.Width + dx, node / Map.Size.Width + dy);
        if (neighbour.LakeMark)
        {
            at.LakeDetail--;
            neighbour.LakeDetail++;
        }
    }

    private void MixWoodcutting(ref ulong hash)
    {
        MixGoods(ref hash);
        hash = Mix(Mix(Mix(hash, _random.W0), _random.W1), _random.W2);
        hash = Mix(Mix(Mix(Mix(hash, _objectStamp), _objectTimer), _objectX), _objectY);
        foreach (var good in FlagEncoder.Places(_flags))
        {
            hash = Mix(hash, good);
        }
    }
}
