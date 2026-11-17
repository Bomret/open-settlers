using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The bakery and its baker (<c>docs/rules/baking.md</c>): the building's handler (<c>0x42e37e</c>) keeps the rank of its flag's request for
/// flour, the start routine of job 16 at the door (<c>0x43d3d9</c>) makes that request, and state 0x24 (<c>0x433694</c>) takes a flour out of
/// the building's fill, bakes it (0x2ff clock units with the node held, then a pause of 0x5dc units with the node free), and sends the baker
/// out with bread (state 0xb, <c>0x43e1d0</c>), who lays it on the flag (state 0xd, <c>0x43c6aa</c>) and goes back in. One flour makes one
/// bread. **Not ported:** the player statistic the step out counts (the goods made). It is the rules module of the baker job
/// (<see cref="JobRules{TJob}"/>) and takes the movement, the flag table, the history, and the step out with the bread and the put of it
/// (<see cref="HutWork.StepOutWithGood"/>) from the <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class BakerRules(JobParts parts) : JobRules<Baker>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly FlagTable _flags = parts.Flags;
    readonly GameHistory _history = parts.History;
    readonly HutWork _hut = parts.Hut;

    // The time a baking takes, which the timer is set to (0x43370b), and the frame of the work (0x433701).
    const ushort BakeTime = 0x2ff;
    const int BakeFrame = 0x8a;

    // The time the pause adds to the timer (0x4337ec); the pause ends the loop where the timer carries.
    const int PauseTime = 0x5dc;

    // The stretches of the work: byte 11 counts them up from 0, which waits for flour, to the one after which the bread is out.
    const int BreadReady = 3;

    // The fill byte counts the flour on site in its high nibble.
    const int OnSite = 0xf0;
    const int OneOnSite = 0x10;

    // The most goods the building's fill counts (on site and on the way) before it asks for no more flour (0x42e3f7).
    const int StoreFull = 8;

    // The rank the request starts from: the handler loads 0xff (0x42e405) and shifts it right by the goods the building holds; the mill's
    // handler reads the player's word instead.
    const byte RankBase = 0xff;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <summary>Registers the behaviour of the baker's work.</summary>
    public override void Register(ActivityRegistry registry) =>
        registry.Register<Bakes>((slot, settler, _) => Work(slot, settler));

    /// <summary>
    /// The handler of the bakery's type (<c>0x42e37e</c>) after its request for a worker, in the turn of each building that is no site: with
    /// the worker inside, or where the request it just made found no worker (the failed request leaves the word at <c>[edi]</c> with bit 6
    /// set, which the handler tests next), the rank of the flag's request for flour is 0xff shifted right by the goods the building holds,
    /// on site and on the way, and 0 where it holds eight or more. A building of another type is left alone.
    /// </summary>
    public override void Turn(Building bakery)
    {
        if (bakery.Type != BuildingType.Baker || bakery.Constructing || !(bakery.WorkerInside || bakery.RequestFailed))
        {
            return;
        }

        var held = (bakery.FillLow >> 4) + (bakery.FillLow & 0xf);
        var rank = held < StoreFull ? (byte)(RankBase >> held) : (byte)0;
        if (_movement.Tables.ClassOfGood(Good.Flour) is { } asked)
        {
            var flag = _flags[bakery.Flag];
            flag.SetRequest(asked.Word, flag.RequestOf(asked.Word) with { Rank = rank });
        }
    }

    /// <summary>
    /// The start routine of job 16 (<c>0x43d3d9</c>), run where the baker has stepped through the door: he leaves the bakery's node, and where
    /// the mode byte he walked in with is not 0 (the first time, not after he put bread down) the flag asks for flour, with rank 0, and for
    /// nothing in the other word. He then waits for flour.
    /// </summary>
    public override void Start(int slot, Settler settler, Building bakery)
    {
        _movement.Release(_movement.Map.Index(settler.X, settler.Y));
        if (_movement.ResidueOf(settler.Activity).Mode != 0)
        {
            // The class bytes are written (0x42 to the class of the flour, 0x44 to 0), the first word's rank too (0x43); the second word's
            // rank stays.
            var flag = _flags[bakery.Flag];
            foreach (var word in new[] { RequestWord.First, RequestWord.Second })
            {
                flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
            }

            if (_movement.Tables.ClassOfGood(Good.Flour) is { } asked)
            {
                flag.SetRequest(asked.Word, new Request { Asks = asked, Rank = 0 });
            }
        }

        _movement.Become(settler, new Bakes { Phase = 0 });
    }

    // State 0x24 (0x433694). Waiting (stretch 0), without flour on site (the high nibble of the fill byte) he changes nothing. With one he
    // takes it out of the fill, claims the node, and bakes: the frame 0x8a, the timer 0x2ff, the stamp the clock. In the other stretches the
    // time since the stamp comes off the timer, and where it has run out the next stretch starts: the pause (2) marks the building as working
    // (bit 4 of its status), lets the node go, and adds 0x5dc to the timer, and the bread is ready at once where that leaves the timer
    // behind; after the pause the building is no longer working and he steps out with the bread.
    void Work(int slot, Settler settler)
    {
        var node = _movement.Map.Index(settler.X, settler.Y);
        var phase = ((Bakes)settler.Activity).Phase;
        var done = false;
        if (phase == 0)
        {
            if (_movement.BuildingAt(node) is { } bakery && (bakery.FillLow & OnSite) != 0)
            {
                bakery.SetFill(FillCounter.Low, bakery.FillLow - OneOnSite);
                _movement.Become(settler, new Bakes { Phase = 1 });
                settler.Frame = BakeFrame;
                settler.Timer = BakeTime;
                settler.Stamp = _movement.Clock;
                _movement.Occupy(node, slot);
            }
        }
        else if (_movement.TimerRan(settler))
        {
            done = NextStretch(settler, node, phase);
        }

        _history.RecordSettler(slot, EventType.Bakes, Cause.Settlers);
        if (done)
        {
            _hut.StepOutWithGood(slot, settler);
        }
    }

    // The loop at 0x433774: the next stretch of the work. True where the bread is ready.
    bool NextStretch(Settler settler, int node, int phase)
    {
        while (true)
        {
            phase++;
            if (phase == BreadReady)
            {
                if (_movement.BuildingAt(node) is { } bakery)
                {
                    bakery.Grinding = false;
                }

                _movement.Become(settler, new StepsOutWithGood { Good = Good.Bread });

                return true;
            }

            _movement.Become(settler, new Bakes { Phase = phase });
            if (_movement.BuildingAt(node) is { } working)
            {
                working.Grinding = true;
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
