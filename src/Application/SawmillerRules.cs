using System;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The sawmill and its sawmiller (<c>docs/rules/sawmilling.md</c>): the building's handler keeps the rank of its flag's request for logs
/// (<c>0x42da0b</c>), the start routine of job 6 at the door makes that request (<c>0x43cf41</c>), and state 0x18 (<c>0x4359fe</c>) takes a log out
/// of the building's fill, saws it for 0x93f clock units, and sends the sawmiller out with a plank (state 0xb, <c>0x43e1d0</c>), who lays it on
/// the flag (state 0xd, <c>0x43c6aa</c>) and goes back in. **Not ported:** the player statistic the step out counts (the goods made, byte
/// <c>-0x6f</c> plus the good of the player's block) and the sound of the saw's frames (<c>0x43fc85</c>). It is the rules module of the
/// sawmiller job (<see cref="JobRules{TJob}"/>) and takes the movement, the flag table (the sawmill's flag holds its request and takes the
/// planks), the history, the laying of a good, and the entry into the hut (which the put runs at once) from the <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class SawmillerRules(JobParts parts) : JobRules<Sawmiller>(parts)
{
    readonly SettlerMovement movement = parts.Movement;
    readonly FlagTable flags = parts.Flags;
    readonly GameHistory history = parts.History;
    readonly Action<int, int, int> layGood = parts.LayGood;
    readonly HutWork hut = parts.Hut;

    // The time a log takes to saw, which the timer is set to (0x435a75), and the frame of the work (0x435a6d).
    const ushort SawTime = 0x93f;
    const int SawFrame = 0x7c;

    // The frame of a settler that waits for a busy node (0x43e44b), and the step onto the flag south-east of the door.
    const int WaitFrame = 0x52;
    const int ToFlag = 1;
    const int StepFrameBase = 0xd;

    // The fill byte of the logs counts the logs on site in its high nibble.
    const int OnSite = 0xf0;
    const int OneOnSite = 0x10;

    // The most goods the building's fill counts (on site and on the way) before it asks for no more logs (0x42da84).
    const int StoreFull = 8;

    const int FlagPlaces = 8;

    /// <summary>Registers the behaviour of the sawmiller's activities.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<Saws>((slot, settler, _) => Work(slot, settler));
        registry.Register<StepsOutWithGood>((slot, settler, _) => StepOut(slot, settler));
        registry.Register<PutsDownGood>((slot, settler, _) => PutDown(slot, settler));
    }

    /// <summary>
    /// The handler of the sawmill's type (<c>0x42da0b</c>) after its request for a worker, in the turn of each building that is no site:
    /// with the worker inside, the rank of the flag's request for logs is the full rank shifted right by the logs the building holds, on
    /// site and on the way, and 0 where it holds eight or more. A building of another type is left alone.
    /// </summary>
    public override void Turn(Building mill)
    {
        if (mill.Type != BuildingType.Sawmill || mill.Constructing || !mill.WorkerInside)
        {
            return;
        }

        var held = (mill.FillHigh >> 4) + (mill.FillHigh & 0xf);
        var rank = held < StoreFull ? (byte)(0xff >> held) : (byte)0;
        if (movement.Tables.ClassOfGood(Good.Log) is { } asked)
        {
            var flag = flags[mill.Flag];
            flag.SetRequest(asked.Word, flag.RequestOf(asked.Word) with { Rank = rank });
        }
    }

    /// <summary>
    /// The start routine of job 6 (<c>0x43cf41</c>), run where the sawmiller has stepped through the door: he leaves the sawmill's node, and
    /// where the mode byte he walked in with is not 0 (the first time, not after he put a plank down) the flag asks for logs, with rank 0,
    /// and for nothing in the other word. He then waits for a log.
    /// </summary>
    public override void Start(int slot, Settler settler, Building mill)
    {
        movement.Release(movement.Map.Index(settler.X, settler.Y));
        if (movement.ResidueOf(settler.Activity).Mode != 0)
        {
            // The request words' class bytes are written (0x42 to 0, 0x44 to the class of the log), the second word's rank too (0x45); the
            // first word's rank stays.
            var flag = flags[mill.Flag];
            foreach (var word in new[] { RequestWord.First, RequestWord.Second })
            {
                flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
            }

            if (movement.Tables.ClassOfGood(Good.Log) is { } asked)
            {
                flag.SetRequest(asked.Word, new Request { Asks = asked, Rank = 0 });
            }
        }

        movement.Become(settler, new Saws { Sawing = false });
    }

    // State 0x18 (0x4359fe). Without a log on site (the high nibble of the fill byte) the sawmiller waits and changes nothing. With
    // one he takes it out of the fill, claims the node, and saws: the frame 0x7c, the timer 0x93f, the stamp the clock. When the timer has
    // run out he lets the node go and steps out with a plank.
    void Work(int slot, Settler settler)
    {
        var node = movement.Map.Index(settler.X, settler.Y);
        var sawing = ((Saws)settler.Activity).Sawing;
        var done = sawing && movement.TimerRan(settler);
        if (done)
        {
            movement.Release(node);
            movement.Become(settler, new StepsOutWithGood { Good = Good.Plank });
        }
        else if (!sawing && movement.BuildingAt(node) is { } mill && (mill.FillHigh & OnSite) != 0)
        {
            mill.SetFill(FillCounter.High, mill.FillHigh - OneOnSite);
            movement.Become(settler, new Saws { Sawing = true });
            movement.Occupy(node, slot);
            settler.Frame = SawFrame;
            settler.Timer = SawTime;
            settler.Stamp = movement.Clock;
        }

        history.RecordSettler(slot, EventType.Saws, Cause.Settlers);
        if (done)
        {
            StepOut(slot, settler);
        }
    }

    // State 0xb (0x43e228): the step from the door onto the flag south-east of it, as the step of state 7 is (0x43e036), with one more
    // condition: the flag must have a free place. A node with another settler on it, or a flag with no free place, makes him wait
    // with the frame 0x52. The time of the step is scaled by the door's speed, and he waits it out in state 5 before he lays the good down.
    void StepOut(int slot, Settler settler)
    {
        var map = movement.Map;
        var here = map.Index(settler.X, settler.Y);
        var (x, y, next) = movement.Neighbour(settler, ToFlag);
        var standing = movement.Occupant(here);
        if (
            (standing == slot || standing == 0)
            && movement.Occupant(next) == 0
            && flags[map.At(next).LinkedSlot].FirstEmptyPlace() >= 0
        )
        {
            var door = movement.BuildingAt(here);
            var shape = StepFrameBase + movement.Height(next) - movement.Height(here);
            var carried = ((StepsOutWithGood)settler.Activity).Good;
            movement.Move(slot, settler, x, y, next);
            settler.Frame = (byte)shape;
            var speed = (byte)(movement.Tables.DoorSpeed(door) ^ 0x1f);
            settler.Timer = (ushort)((ushort)(movement.Tables.StepTime(shape) * speed) >> 5);
            settler.Stamp = movement.Clock;
            settler.Activity = new WaitsOutStep { Then = new PutsDownGood { Good = carried } };
        }
        else
        {
            settler.Frame = WaitFrame;
            settler.Timer = 0;
        }

        history.RecordSettler(slot, EventType.StepOut, Cause.Settlers);
    }

    // State 0xd (0x43c6aa): the good goes on the first empty place of the flag, or on the last place where the first seven hold goods;
    // the flag's turn is due. The settler's byte 11 is 0 from here on, and he goes into the building (state 6).
    void PutDown(int slot, Settler settler)
    {
        var good = ((PutsDownGood)settler.Activity).Good;
        var flag = movement.Map.At(settler.X, settler.Y).LinkedSlot;
        var place = flags[flag].FirstEmptyPlace();
        layGood(flag, place >= 0 ? place : FlagPlaces - 1, good.Code);
        history.RecordFlag(flag, EventType.PutGood, Cause.Settlers, history.SettlerId(slot).ToString());
        movement.Become(settler, new EntersBuilding(), movement.ResidueOf(settler.Activity) with { Mode = 0 });
        hut.EnterHut(slot, settler);
    }
}
