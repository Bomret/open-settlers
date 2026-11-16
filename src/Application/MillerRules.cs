using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The mill and its miller (<c>docs/rules/milling.md</c>): the building's handler (<c>0x42e2a0</c>) keeps the rank of its flag's request for grain,
/// the start routine of job 15 at the door (<c>0x43d33d</c>) makes that request, and state 0x23 (<c>0x43386b</c>) takes a grain out of the
/// building's fill, grinds it in four stretches (two grindings of 0x17f clock units with the node held, each followed by a pause of
/// 0x5dc units with the node free), and sends the miller out with flour (state 0xb, <c>0x43e1d0</c>), who lays it on the flag (state 0xd,
/// <c>0x43c6aa</c>) and goes back in. **Not ported:** the player statistic the step out counts (the goods made). It is the rules module of the
/// miller job (<see cref="JobRules{TJob}"/>) and takes the movement, the flag table (the mill's flag holds its request and takes the flour),
/// the history, and the step out with the flour and the put of it (<see cref="HutWork.StepOutWithGood"/>) from the <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class MillerRules(JobParts parts) : JobRules<Miller>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly FlagTable _flags = parts.Flags;
    readonly GameHistory _history = parts.History;
    readonly HutWork _hut = parts.Hut;

    // The time a grinding takes, which the timer is set to (0x433998 and 0x4338f9), and the frame of the work (0x4338ea).
    const ushort GrindTime = 0x17f;
    const int GrindFrame = 0x89;

    // The time a pause adds to the timer (0x43398f, 0x4339c0); the pause ends the loop where the timer carries.
    const int PauseTime = 0x5dc;

    // The stretches of the work: the byte 11 counts them up from 0, which waits for grain, to the one after which the flour is out.
    const int SecondGrinding = 3;
    const int FlourReady = 5;

    // The fill byte counts the grain on site in its high nibble.
    const int OnSite = 0xf0;
    const int OneOnSite = 0x10;

    // The most goods the building's fill counts (on site and on the way) before it asks for no more grain (0x42e33e).
    const int StoreFull = 8;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <summary>Registers the behaviour of the miller's work.</summary>
    public override void Register(ActivityRegistry registry) =>
        registry.Register<Grinds>((slot, settler, _) => Work(slot, settler));

    /// <summary>
    /// The handler of the mill's type (<c>0x42e2a0</c>) after its request for a worker, in the turn of each building that is no site: with
    /// the worker inside, or where the request it just made found no worker (the failed request leaves the word at <c>[edi]</c> with bit 6
    /// set, which the handler tests next), the rank of the flag's request for grain is the player's start rank
    /// (<see cref="RuleTables.MillRankBase"/>) shifted right by the goods the building holds, on site and on the way, and 0 where it holds
    /// eight or more. A building of another type is left alone.
    /// </summary>
    public override void Turn(Building mill)
    {
        if (mill.Type != BuildingType.Mill || mill.Constructing || !(mill.WorkerInside || mill.RequestFailed))
        {
            return;
        }

        var held = (mill.FillLow >> 4) + (mill.FillLow & 0xf);
        var rank = held < StoreFull ? (byte)(_movement.Tables.MillRankBase >> held) : (byte)0;
        if (_movement.Tables.ClassOfGood(Good.Grain) is { } asked)
        {
            var flag = _flags[mill.Flag];
            flag.SetRequest(asked.Word, flag.RequestOf(asked.Word) with { Rank = rank });
        }
    }

    /// <summary>
    /// The start routine of job 15 (<c>0x43d33d</c>), run where the miller has stepped through the door: he leaves the mill's node, and where
    /// the mode byte he walked in with is not 0 (the first time, not after he put flour down) the flag asks for grain, with rank 0, and for
    /// nothing in the other word. He then waits for grain.
    /// </summary>
    public override void Start(int slot, Settler settler, Building mill)
    {
        _movement.Release(_movement.Map.Index(settler.X, settler.Y));
        if (_movement.ResidueOf(settler.Activity).Mode != 0)
        {
            // The request words' class bytes are written (0x42 to the class of the grain, 0x44 to 0), the first word's rank too (0x43); the
            // second word's rank stays.
            var flag = _flags[mill.Flag];
            foreach (var word in new[] { RequestWord.First, RequestWord.Second })
            {
                flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
            }

            if (_movement.Tables.ClassOfGood(Good.Grain) is { } asked)
            {
                flag.SetRequest(asked.Word, new Request { Asks = asked, Rank = 0 });
            }
        }

        _movement.Become(settler, new Grinds { Phase = 0 });
    }

    // State 0x23 (0x43386b). Waiting (stretch 0), without grain on site (the high nibble of the fill byte) he changes nothing. With
    // one he takes it out of the fill, marks the building as working (bit 4 of its status), claims the node, and grinds: the frame 0x89,
    // the timer 0x17f, the stamp the clock. In the other stretches the time since the stamp comes off the timer, and where it has run
    // out the next stretch starts: the pauses (2, 4) let the node go and add 0x5dc to the timer, and the next stretch follows at once
    // where that leaves the timer behind; the second grinding (3) sets the frame, the node, and the timer as the first does; after
    // the fourth the building is no longer working and he steps out with the flour.
    void Work(int slot, Settler settler)
    {
        var node = _movement.Map.Index(settler.X, settler.Y);
        var phase = ((Grinds)settler.Activity).Phase;
        var done = false;
        if (phase == 0)
        {
            if (_movement.BuildingAt(node) is { } mill && (mill.FillLow & OnSite) != 0)
            {
                mill.SetFill(FillCounter.Low, mill.FillLow - OneOnSite);
                mill.Grinding = true;
                _movement.Become(settler, new Grinds { Phase = 1 });
                _movement.Occupy(node, slot);
                settler.Frame = GrindFrame;
                settler.Timer = GrindTime;
                settler.Stamp = _movement.Clock;
            }
        }
        else if (_movement.TimerRan(settler))
        {
            done = NextStretch(slot, settler, node, phase);
        }

        _history.RecordSettler(slot, EventType.Grinds, Cause.Settlers);
        if (done)
        {
            _hut.StepOutWithGood(slot, settler);
        }
    }

    // The loop at 0x43395f: the next stretch of the work. True where the flour is ready.
    bool NextStretch(int slot, Settler settler, int node, int phase)
    {
        while (true)
        {
            phase++;
            if (phase == FlourReady)
            {
                if (_movement.BuildingAt(node) is { } mill)
                {
                    mill.Grinding = false;
                }

                _movement.Become(settler, new StepsOutWithGood { Good = Good.Flour });

                return true;
            }

            _movement.Become(settler, new Grinds { Phase = phase });
            if (phase == SecondGrinding)
            {
                settler.Frame = GrindFrame;
                _movement.Occupy(node, slot);
                settler.Timer = GrindTime;

                return false;
            }

            _movement.Release(node);
            var carried = settler.Timer + PauseTime > ushort.MaxValue;
            settler.Timer = (ushort)(settler.Timer + PauseTime);
            if (carried)
            {
                return false;
            }
        }
    }
}
