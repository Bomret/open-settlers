using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The pig farm and its pig farmer (<c>docs/rules/pig-farming.md</c>): the building's handler (<c>0x42e0fa</c>) keeps the rank of its flag's
/// request for grain, the start routine of job 12 at the door (<c>0x43d179</c>) makes that request and sets the pigs the building holds to
/// one (byte 9 of the building), and state 0x25 (<c>0x4333c1</c>) takes a grain out of the building's fill and works in stretches. The odd
/// stretches work for 0x17f clock units with the node held, the even ones pause for 0x800 units with the node free, and in each pause the
/// pigs the building holds may grow (a draw against the program's table). After the sixth stretch the building may give a pig: always at 8
/// held, never below 4, and with the odds of the held count in 20 between. A pig goes out as the sawmiller's plank does (state 0xb,
/// <c>0x43e1d0</c>, then <see cref="HutWork.PutDownGood"/>). Where no pig is given the work goes on with the next round, or, with the odds of
/// 1 in 16, waits for a new grain. **Not ported:** the player statistic the step out counts (the goods made). It is the rules module of the
/// pig farmer job (<see cref="JobRules{TJob}"/>) and takes the movement, the flag table, the history, the draws, and the step out with a
/// good (<see cref="HutWork.StepOutWithGood"/>) from the <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class PigFarmerRules(JobParts parts) : JobRules<PigFarmer>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly FlagTable _flags = parts.Flags;
    readonly GameHistory _history = parts.History;
    readonly HutWork _hut = parts.Hut;
    readonly IRandomDraws _draws = parts.Draws;

    // The time a work stretch takes, which the timer is set to (0x433435 and the others), and the frame of the work (0x43342d).
    const ushort WorkTime = 0x17f;
    const int WorkFrame = 0x8b;

    // The time a pause adds to the timer (0x433677); the pause ends the loop where the timer carries.
    const int PauseTime = 0x800;

    // The stretch after which the farm may give a pig (byte 11 reaches 7: 0x4334cb).
    const int PigStretch = 7;

    // The pigs the building holds (byte 9): a pig is always given at the most, never below the least, and in between with the odds of the
    // held count in 20 (0x433536 and 0x43353b).
    const int MostPigs = 8;
    const int LeastPigs = 4;
    const int PigOdds = 20;

    // Where no pig is given the work waits for a new grain with the odds of 1 in 16 (the draw's low four bits are 0, 0x433598).
    const int WaitMask = 0xf;

    // The fill byte counts the grain on site in its high nibble.
    const int OnSite = 0xf0;
    const int OneOnSite = 0x10;

    // The most goods the building's fill counts (on site and on the way) before it asks for no more grain (0x42e173).
    const int StoreFull = 8;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <summary>Registers the behaviour of the pig farmer's work.</summary>
    public override void Register(ActivityRegistry registry) =>
        registry.Register<TendsPigs>((slot, settler, _) => Work(slot, settler));

    /// <summary>
    /// The handler of the pig farm's type (<c>0x42e0fa</c>) after its request for a worker, in the turn of each building that is no site:
    /// with the worker inside, or where the request it just made found no worker, the rank of the flag's request for grain is the player's
    /// start rank (<see cref="PigFarmRates.RankBase"/>) shifted right by the goods the building holds, on site and on the way, and 0 where it
    /// holds eight or more. A building of another type is left alone.
    /// <para>
    /// **Stand-in:** the handler has no preamble that sets the pointer to the player block (the mill's has one): it reads the pointer the code
    /// before it left in a scratch word. After the request that finds a worker the castle's handler (not ported) left a pointer to something
    /// else, so the rank the emulator shows is 0 for the turns while the worker is on the way, and the farm asks for no grain then, as its
    /// request has no class before the worker's start routine runs. The engine writes 0 in those turns.
    /// </para>
    /// </summary>
    public override void Turn(Building farm)
    {
        if (farm.Type != BuildingType.PigFarm || farm.Constructing)
        {
            return;
        }

        byte rank;
        if (farm.WorkerInside || farm.RequestFailed)
        {
            var held = (farm.FillLow >> 4) + (farm.FillLow & 0xf);
            rank =
                held < StoreFull
                    ? (byte)(_movement.Tables.Get<PigFarmRates>(new PigFarmer()).RankBase >> held)
                    : (byte)0;
        }
        else if (farm.WorkerOnTheWay)
        {
            rank = 0;
        }
        else
        {
            return;
        }

        if (_movement.Tables.ClassOfGood(Good.Grain) is { } asked)
        {
            var flag = _flags[farm.Flag];
            flag.SetRequest(asked.Word, flag.RequestOf(asked.Word) with { Rank = rank });
        }
    }

    /// <summary>
    /// The start routine of job 12 (<c>0x43d179</c>), run where the pig farmer has stepped through the door: he leaves the farm's node, and
    /// where the mode byte he walked in with is not 0 (the first time) the farm holds one pig and the flag asks for grain, with rank 0, and
    /// for nothing in the other word; he then waits for grain. Where the mode byte is 0 (after he put a pig down) he goes on at the sixth stretch
    /// with his timer at 0.
    /// </summary>
    public override void Start(int slot, Settler settler, Building farm)
    {
        _movement.Release(_movement.Map.Index(settler.X, settler.Y));
        if (_movement.ResidueOf(settler.Activity).Mode == 0)
        {
            _movement.Become(settler, new TendsPigs { Phase = PigStretch - 1 });
            settler.Timer = 0;

            return;
        }

        farm.FillHigh = 1;
        var flag = _flags[farm.Flag];
        foreach (var word in new[] { RequestWord.First, RequestWord.Second })
        {
            flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
        }

        if (_movement.Tables.ClassOfGood(Good.Grain) is { } asked)
        {
            flag.SetRequest(asked.Word, new Request { Asks = asked, Rank = 0 });
        }

        _movement.Become(settler, new TendsPigs { Phase = 0 });
    }

    // State 0x25 (0x4333c1). Waiting (stretch 0), without grain on site (the high nibble of the fill byte) he changes nothing. With one he
    // takes it out of the fill, claims the node, and works: the frame 0x8b, the timer 0x17f, the stamp the clock. In the other stretches the
    // time since the stamp comes off the timer, and where it has run out the next stretch starts.
    void Work(int slot, Settler settler)
    {
        var node = _movement.Map.Index(settler.X, settler.Y);
        var phase = ((TendsPigs)settler.Activity).Phase;
        var done = false;
        if (phase == 0)
        {
            if (_movement.BuildingAt(node) is { } farm && (farm.FillLow & OnSite) != 0)
            {
                farm.SetFill(FillCounter.Low, farm.FillLow - OneOnSite);
                BeginWork(slot, settler, node, 1);
                settler.Stamp = _movement.Clock;
            }
        }
        else if (_movement.TimerRan(settler))
        {
            done = NextStretch(slot, settler, node, phase);
        }

        _history.RecordSettler(slot, EventType.TendsPigs, Cause.Settlers);
        if (done)
        {
            _hut.StepOutWithGood(slot, settler);
        }
    }

    // A work stretch: the node is his, the frame is the work's, and the timer is the work's time.
    void BeginWork(int slot, Settler settler, int node, int phase)
    {
        _movement.Become(settler, new TendsPigs { Phase = phase });
        _movement.Occupy(node, slot);
        settler.Frame = WorkFrame;
        settler.Timer = WorkTime;
    }

    // The loop at 0x4334a2: the next stretch of the work. True where a pig is out.
    bool NextStretch(int slot, Settler settler, int node, int phase)
    {
        while (true)
        {
            phase++;
            if (phase % 2 == 1 && phase < PigStretch)
            {
                BeginWork(slot, settler, node, phase);

                return false;
            }

            if (phase == PigStretch)
            {
                return AfterRound(slot, settler, node);
            }

            _movement.Become(settler, new TendsPigs { Phase = phase });
            _movement.Release(node);
            Grow(node);
            var carried = settler.Timer + PauseTime > ushort.MaxValue;
            settler.Timer = (ushort)(settler.Timer + PauseTime);
            if (carried)
            {
                return false;
            }
        }
    }

    // The pause (0x4335ed): with fewer than eight pigs held, a draw of the generator below the table's entry of the held count makes one more.
    void Grow(int node)
    {
        if (_movement.BuildingAt(node) is not { } farm || farm.FillHigh == MostPigs)
        {
            return;
        }

        if (_draws.Draw() < _movement.Tables.Get<PigFarmRates>(new PigFarmer()).Growth[farm.FillHigh])
        {
            farm.FillHigh++;
        }
    }

    // After the sixth stretch (0x4334f5): a pig is given where eight are held, or from four on where a draw of 20 is below the held count. The
    // pig goes out (true). Else the work goes on with the next round (a work stretch at once), or, with the odds of 1 in 16, waits for grain.
    bool AfterRound(int slot, Settler settler, int node)
    {
        if (_movement.BuildingAt(node) is { } farm && GivesPig(farm.FillHigh))
        {
            farm.FillHigh--;
            _movement.Become(settler, new StepsOutWithGood { Good = Good.Pig });

            return true;
        }

        if ((_draws.Draw() & WaitMask) == 0)
        {
            _movement.Become(settler, new TendsPigs { Phase = 0 });

            return false;
        }

        BeginWork(slot, settler, node, 1);
        settler.Stamp = _movement.Clock;

        return false;
    }

    bool GivesPig(int held) => held == MostPigs || (held >= LeastPigs && (_draws.Draw() * PigOdds >> 16) < held);
}
