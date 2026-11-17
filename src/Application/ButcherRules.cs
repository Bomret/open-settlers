using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The butcher's shop and its butcher (<c>docs/rules/butchering.md</c>): the building's handler (<c>0x42e1b3</c>) keeps the rank of its flag's
/// request for pigs, the start routine of job 13 at the door (<c>0x43d275</c>) makes that request, and state 0x26 (<c>0x4332a8</c>) takes a pig
/// out of the building's fill, works on it for one stretch of 0x5ff clock units with the node held, and sends the butcher out with meat
/// (state 0xb, <c>0x43e1d0</c>), who lays it on the flag (state 0xd, <c>0x43c6aa</c>) and goes back in. One pig makes one meat. **Not ported:** the
/// player statistic the step out counts (the goods made). It is the rules module of the butcher job (<see cref="JobRules{TJob}"/>) and takes
/// the movement, the flag table (the shop's flag holds its request and takes the meat), the history, and the step out with the meat and the
/// put of it (<see cref="HutWork.StepOutWithGood"/>) from the <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class ButcherRules(JobParts parts) : JobRules<Butcher>(parts)
{
    readonly SettlerMovement _movement = parts.Movement;
    readonly FlagTable _flags = parts.Flags;
    readonly GameHistory _history = parts.History;
    readonly HutWork _hut = parts.Hut;

    // The time the work on a pig takes, which the timer is set to (0x43331f), and the frame of the work (0x433317).
    const ushort WorkTime = 0x5ff;
    const int WorkFrame = 0x8c;

    // The fill byte counts the pigs on site in its high nibble.
    const int OnSite = 0xf0;
    const int OneOnSite = 0x10;

    // The rank the request for pigs starts from, the constant the handler shifts (0x42e1de).
    const byte RankBase = 0xff;

    /// <inheritdoc />
    public override bool CanStandInHut => true;

    /// <summary>Registers the behaviour of the butcher's work.</summary>
    public override void Register(ActivityRegistry registry) =>
        registry.Register<Butchers>((slot, settler, _) => Work(slot, settler));

    /// <summary>
    /// The handler of the shop's type (<c>0x42e1b3</c>) after its request for a worker, in the turn of each building that is no site: with
    /// the worker inside, or where the request it just made found no worker (the failed request leaves the word at <c>[edi]</c> with bit 6
    /// set, which the handler tests next), the rank of the flag's request for pigs is 0xff shifted right by the goods the building holds, on
    /// site and on the way, and 0 where it holds eight or more. A building of another type is left alone.
    /// </summary>
    public override void Turn(Building shop)
    {
        if (shop.Type != BuildingType.Butcher || shop.Constructing || !(shop.WorkerInside || shop.RequestFailed))
        {
            return;
        }

        FoodHutRequest.SetRank(_movement, _flags, shop, Good.Pig, FoodHutRequest.RankOf(shop, RankBase));
    }

    /// <summary>
    /// The start routine of job 13 (<c>0x43d275</c>), run where the butcher has stepped through the door: he leaves the shop's node, and where
    /// the mode byte he walked in with is not 0 (the first time, not after he put meat down) the flag asks for pigs, with rank 0, and for
    /// nothing in the other word. He then waits for a pig.
    /// </summary>
    public override void Start(int slot, Settler settler, Building shop)
    {
        _movement.Release(_movement.Map.Index(settler.X, settler.Y));
        if (_movement.ResidueOf(settler.Activity).Mode != 0)
        {
            // The request words' class bytes are written (0x42 to the class of the pig, 0x44 to 0), the first word's rank too (0x43).
            FoodHutRequest.Ask(_movement, _flags, shop, Good.Pig);
        }

        _movement.Become(settler, new Butchers { Working = false });
    }

    // State 0x26 (0x4332a8). Waiting, without a pig on site (the high nibble of the fill byte) he changes nothing. With one he takes it out
    // of the fill, claims the node, and works: the frame 0x8c, the timer 0x5ff, the stamp the clock. Working, the time since the stamp
    // comes off the timer, and where it has run out the node goes free and he steps out with the meat.
    void Work(int slot, Settler settler)
    {
        var node = _movement.Map.Index(settler.X, settler.Y);
        var done = false;
        if (!((Butchers)settler.Activity).Working)
        {
            if (_movement.BuildingAt(node) is { } shop && (shop.FillLow & OnSite) != 0)
            {
                shop.SetFill(FillCounter.Low, shop.FillLow - OneOnSite);
                _movement.Become(settler, new Butchers { Working = true });
                _movement.Occupy(node, slot);
                settler.Frame = WorkFrame;
                settler.Timer = WorkTime;
                settler.Stamp = _movement.Clock;
            }
        }
        else if (_movement.TimerRan(settler))
        {
            _movement.Release(node);
            _movement.Become(settler, new StepsOutWithGood { Good = Good.Meat });
            done = true;
        }

        _history.RecordSettler(slot, EventType.Butchers, Cause.Settlers);
        if (done)
        {
            _hut.StepOutWithGood(slot, settler);
        }
    }
}
