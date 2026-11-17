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
/// planks), the history, and the step out with the plank and the put of it (<see cref="HutWork.StepOutWithGood"/>, shared with the other
/// workers of a building) from the <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class SawmillerRules(JobParts parts) : JobRules<Sawmiller>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly FlagTable _flags = parts.Flags;
    readonly GameHistory _history = parts.History;
    readonly HutWork _hut = parts.Hut;

    // The time a log takes to saw, which the timer is set to (0x435a75), and the frame of the work (0x435a6d).
    const ushort SawTime = 0x93f;
    const int SawFrame = 0x7c;

    // The fill byte of the logs counts the logs on site in its high nibble.
    const int OnSite = 0xf0;
    const int OneOnSite = 0x10;

    // The most goods the building's fill counts (on site and on the way) before it asks for no more logs (0x42da84).
    const int StoreFull = 8;

    /// <summary>Registers the behaviour of the sawmiller's activities.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<Saws>((slot, settler, _) => Work(slot, settler));
    }

    /// <summary>
    /// The handler of the sawmill's type (<c>0x42da0b</c>) after its request for a worker, in the turn of each building that is no site:
    /// with the worker inside, or where the request it just made found no worker (the failed request leaves the word at <c>[edi]</c> with
    /// bit 6 set, which the handler tests next at <c>0x42da4c</c>, as the mill's does), the rank of the flag's request for logs is the full
    /// rank shifted right by the logs the building holds, on site and on the way, and 0 where it holds eight or more. A building of
    /// another type is left alone.
    /// </summary>
    public override void Turn(Building mill)
    {
        if (mill.Type != BuildingType.Sawmill || mill.Constructing || !(mill.WorkerInside || mill.RequestFailed))
        {
            return;
        }

        var held = (mill.FillHigh >> 4) + (mill.FillHigh & 0xf);
        var rank = held < StoreFull ? (byte)(0xff >> held) : (byte)0;
        if (_movement.Tables.ClassOfGood(Good.Log) is { } asked)
        {
            var flag = _flags[mill.Flag];
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
        _movement.Release(_movement.Map.Index(settler.X, settler.Y));
        if (_movement.ResidueOf(settler.Activity).Mode != 0)
        {
            // The request words' class bytes are written (0x42 to 0, 0x44 to the class of the log), the second word's rank too (0x45); the
            // first word's rank stays.
            var flag = _flags[mill.Flag];
            foreach (var word in new[] { RequestWord.First, RequestWord.Second })
            {
                flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
            }

            if (_movement.Tables.ClassOfGood(Good.Log) is { } asked)
            {
                flag.SetRequest(asked.Word, new Request { Asks = asked, Rank = 0 });
            }
        }

        _movement.Become(settler, new Saws { Sawing = false });
    }

    // State 0x18 (0x4359fe). Without a log on site (the high nibble of the fill byte) the sawmiller waits and changes nothing. With
    // one he takes it out of the fill, claims the node, and saws: the frame 0x7c, the timer 0x93f, the stamp the clock. When the timer has
    // run out he lets the node go and steps out with a plank.
    void Work(int slot, Settler settler)
    {
        var node = _movement.Map.Index(settler.X, settler.Y);
        var sawing = ((Saws)settler.Activity).Sawing;
        var done = sawing && _movement.TimerRan(settler);
        if (done)
        {
            _movement.Release(node);
            _movement.Become(settler, new StepsOutWithGood { Good = Good.Plank });
        }
        else if (!sawing && _movement.BuildingAt(node) is { } mill && (mill.FillHigh & OnSite) != 0)
        {
            mill.SetFill(FillCounter.High, mill.FillHigh - OneOnSite);
            _movement.Become(settler, new Saws { Sawing = true });
            _movement.Occupy(node, slot);
            settler.Frame = SawFrame;
            settler.Timer = SawTime;
            settler.Stamp = _movement.Clock;
        }

        _history.RecordSettler(slot, EventType.Saws, Cause.Settlers);
        if (done)
        {
            _hut.StepOutWithGood(slot, settler);
        }
    }
}
