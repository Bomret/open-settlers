using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

// The walk home of a settler whose work was taken away, as the teardown of a building sends it (0x461b77 sets state 0x1c;
// docs/rules/settlers.md, "Walking home"): the claim of the node it stands on (state 0x1c), the search for the nearest
// flag of its player that a road leaves (state 0x19), the free walk to it (state 0x10, FreeWalk), and the arrival (0x4385ab),
// which starts the walk along the roads to a storage (state 2 with the mode 0xfe, Walk), and the entry into the castle
// (EnterStorage). **Not ported:** the boatman's own state 0x1a (water roads are not ported), the arrival of the jobs whose
// branch of 0x437ddd is not the generic one (see ArrivesHome), and the entry of the jobs 4 and 21 into a storage.
public sealed partial class Game
{
    // The settlers on the walk home, from the claim of their node to the entry into the storage: the steps of the shared walk
    // routines (FreeWalk, Walk) record the walk home as their cause (SettlerCause; history only, no rule reads it).
    private readonly HashSet<int> _walkingHome = [];

    // The mode of state 2 that walks to a storage along the roads (byte 11 = 0xfe).
    private const int StorageWalk = 0xfe;

    // The state a settler of the jobs 22 to 26 (the knights) takes where the others take the free walk (0x435821).
    private const int KnightWalk = 0x35;

    // The entries of the spiral table the search reads: 1 to 258, from either end.
    private const int SearchEntries = 258;

    // State 0x1c (0x434946): the settler takes the node it stands on when no other settler stands there, with the frame
    // 0x52, no timer, and the mode 0, and goes on as state 0x19. Else it waits.
    private void HomeClaim(int slot, Settler settler)
    {
        var index = Map.Index(settler.X, settler.Y);
        if (Occupant(index) != 0)
        {
            return;
        }

        _occupant[index] = slot;
        _walkingHome.Add(slot);
        settler.Frame = 0x52;
        settler.Timer = 0;
        settler.State = 0x19;
        settler.Mode = 0;
        settler.Stamp = _clock;
        History.RecordSettler(slot, EventType.ClaimedHome, Cause.WalkHome);
    }

    // The storage the walk home goes to (0x45d09b): the nearest by roads of the flags that hold a storage's request bytes.
    // The castle is the only storage, so it is the castle's flag where that flag is the flag the settler stands on or a road
    // leads to it, else -1.
    private int StorageFlag(int flag)
    {
        var castle = _buildings[0].Flag;

        return flag == castle || Route(flag, castle) >= 0 ? castle : -1;
    }

    // The settler's job takes it into the storage (0x43cd06 for the jobs whose routine of the table at 0x43ca54 begins with the
    // test of the mode 0xfe, which is all but the jobs 4 and 21): the node it stands on is free again, and it waits in the
    // storage's list (state 1, mode 0, the storage's slot as heading, no target); the turn of state 1 enters it in the list.
    // The castle is storage 0.
    private void EnterStorage(int slot, int node, Settler settler)
    {
        _walkingHome.Remove(slot);
        if (settler.Job is 4 or 21)
        {
            return;
        }

        _occupant.Remove(node);
        settler.State = 1;
        settler.Mode = 0;
        settler.Target = 0;
        settler.Heading = 0;
        settler.Next = 0;
        History.RecordSettler(slot, EventType.EnteredStorage, Cause.WalkHome, History.BuildingId(0).ToString());
    }

    // The owner a node of the settler's player has (0x4354b8).
    private static Owner OwnerOf(Settler settler) => Owner.Player(settler.Player);

    // Whether a flag of the settler's player stands on the node and a road leaves it (the flag mark of the node, bits 0 to 5 of the
    // flag item's byte 4 not 0, the owners equal: 0x4355 and 0x4385ab).
    private bool HomeFlag(Settler settler, int x, int y)
    {
        var index = Map.Index(x, y);
        if (!Map.At(index).FlagMark || Map.At(index).Owner != OwnerOf(settler))
        {
            return false;
        }

        var flag = _flags.FindIndex(f => f.X == x && f.Y == y);

        return flag >= 0 && Enumerable.Range(0, 6).Any(direction => Link(flag, direction) >= 0);
    }

    // State 0x19 (0x4353ef), once its timer has run out: the spiral search around the settler for the nearest such flag (the
    // entries 1 to 258 of the table, or 258 to 1 where byte 11 is not 0: the far end first), whose offset becomes the vector of the
    // free walk (bytes 11 and 12, byte 13 = 0x80 marks a walk to a flag, byte 14 = -1 and byte 15 = 0). Where none is found a
    // random node is taken (HomeNode). The jobs 22 to 26 take state 0x35 where the others take 0x10.
    private void HomeSearch(int slot, Settler settler)
    {
        if (!TimerRan(settler))
        {
            // TimerRan has counted the timer down and stamped the settler: the clock moves between two turns, so the stamp
            // changed, and the turn is a change to record.
            History.RecordSettler(slot, EventType.SearchedHome, Cause.WalkHome);

            return;
        }

        var width = Map.Size.Width - 1;
        var height = Map.Size.Height - 1;
        var far = settler.Mode != 0;
        int dx = 0;
        int dy = 0;
        var found = false;
        for (var count = 0; count < SearchEntries && !found; count++)
        {
            var entry = far ? SearchEntries - count : 1 + count;
            (dx, dy) = MapGenerator.SearchOffsets[entry];
            found = HomeFlag(settler, (settler.X + dx) & width, (settler.Y + dy) & height);
        }

        if (!found)
        {
            (dx, dy) = HomeNode(settler);
        }

        SetVector(settler, dx, dy);
        SetMark(settler, 0x80);
        settler.Heading = -1;
        settler.Next = 0;
        settler.State = settler.Job is >= FirstKnightJob and <= 26 ? KnightWalk : 0x10;
        settler.Timer = 0;
        History.RecordSettler(slot, EventType.SearchedHome, Cause.WalkHome);
    }

    // The random choice where no flag is near (0x43560c): the draw of the generator gives two offsets, its low byte and
    // its high byte each masked and less a half, for ten tries around the settler (a mask of 15), then twenty each (masks 31 and
    // 63); a node is taken that holds no object (object code 0) and a height not 0, and in the three rings of the
    // settler's own player. After the three rings any such node is taken. **Inferred:** the second and third rings and the
    // last phase (no emulator run reaches them; the first ring is matched). Returns the offsets, wrapped to bytes as the
    // vector holds them.
    private (int Dx, int Dy) HomeNode(Settler settler)
    {
        var width = Map.Size.Width - 1;
        var height = Map.Size.Height - 1;
        var owner = OwnerOf(settler);
        var tries = 10;
        var mask = 15;
        var half = 8;
        var anywhere = false;
        while (true)
        {
            if (tries == 0)
            {
                if (mask == 0x3f)
                {
                    anywhere = true;
                    mask = 15;
                    half = 8;
                    tries = -1;
                }
                else
                {
                    tries = 0x13;
                    mask = mask * 2 + 1;
                    half *= 2;
                }
            }
            else
            {
                tries--;
            }

            var draw = Draw();
            var dx = (draw & 0xff & mask) - half;
            var dy = (draw >> 8 & mask) - half;
            var index = Map.Index((settler.X + dx) & width, (settler.Y + dy) & height);
            if (!Map.At(index).Object.Exists && Map.At(index).Height != 0 && (anywhere || Map.At(index).Owner == owner))
            {
                return ((sbyte)dx, (sbyte)dy);
            }
        }
    }

    // The jobs whose arrival at the end of a free walk is the generic one of 0x4385ab whatever the vector (the table at
    // 0x437dfb): all but the woodcutter (5), the jobs 7, 8, 11, 14, and 20, and the knights. Those jobs' own branches hand over
    // to it where the walk is a walk home (byte 13 = 0x80, byte 14 negative).
    private static bool ArrivesHome(int job) => job is not (5 or 7 or 8 or 11 or 14 or 20 or >= FirstKnightJob);

    // The arrival of a settler on the way home (0x4385ab): on a flag of its player with a road it starts the walk to the
    // storage (state 2, mode 0xfe, target 0, heading 0, next 0, timer 0); anywhere else it searches again (state 0x19, mode 0).
    private void ArriveHome(int slot, Settler settler)
    {
        settler.Timer = 0;
        if (HomeFlag(settler, settler.X, settler.Y))
        {
            settler.State = 2;
            settler.Mode = StorageWalk;
            settler.Target = 0;
            settler.Heading = 0;
            settler.Next = 0;
            History.RecordSettler(slot, EventType.ArrivedHome, Cause.WalkHome, History.BuildingId(0).ToString());

            return;
        }

        settler.State = 0x19;
        settler.Mode = 0;
        History.RecordSettler(slot, EventType.ArrivedHome, Cause.WalkHome);
    }
}
