using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// The player's settler supply (<c>docs/rules/new-settlers.md</c>): from the castle's placement on, a timer makes one settler without a
/// job in the castle for each period that passes, and every few settlers it owes a knight, which it makes where the castle holds a
/// sword and a shield (<c>Player_MakeSettlers</c>, <c>0x428a75</c>, with <c>Settler_CreateInStorage</c>, <c>0x44333d</c>). The castle is
/// the one storage, and the player is the one player. All of its arithmetic is 16-bit, as the original's.
/// </summary>
public sealed class SettlerSupply
{
    /// <summary>The lowest settler rate.</summary>
    public const int LowestRate = 0;

    /// <summary>The highest settler rate: the free-game bar sets a value from 0 to 40 (<c>0x46919a</c>).</summary>
    public const int HighestRate = 40;

    // The period is (RateBase - rate) * PeriodUnits clock units (0x42055e).
    const int RateBase = 60;
    const int PeriodUnits = 50;

    // Each try adds KnightStep to the knight accumulator, which starts at KnightStart (0x42055e); a carry owes a knight (0x428a75).
    const ushort KnightStep = 20000;
    const ushort KnightStart = 0x8000;

    // The knights owed at most (0x428a75).
    const int MostKnightsOwed = 2;

    // The job of a new knight (0x428a75 sets the job bits of byte 0 to 22), and the goods it takes from the stock: the sword and
    // the shield, goods 25 and 26 (which one is which is open).
    static readonly Job NewKnight = Job.Of(22);
    static readonly Good Sword = new() { Code = 25 };
    static readonly Good Shield = new() { Code = 26 };

    readonly GameClock _clock;
    readonly CastleStock _stock;
    readonly SettlerTable _settlers;
    readonly GameHistory _history;

    /// <summary>A supply at the settler rate <paramref name="rate"/>, 0 to 40; any other rate is refused.</summary>
    public SettlerSupply(int rate, GameClock clock, CastleStock stock, SettlerTable settlers, GameHistory history)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rate, LowestRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rate, HighestRate);
        _clock = clock;
        _stock = stock;
        _settlers = settlers;
        _history = history;
        Rate = rate;
        Period = (ushort)((RateBase - rate) * PeriodUnits);
        Countdown = Period;
    }

    /// <summary>The settler rate, 0 to 40.</summary>
    public int Rate { get; }

    /// <summary>The clock units between two settlers (word at <c>+0x122</c> of the player block).</summary>
    public ushort Period { get; }

    /// <summary>The clock units left of the current period, a wrapped value below zero (word at <c>+0x120</c>).</summary>
    public ushort Countdown { get; private set; }

    /// <summary>The clock at the supply's last turn, or at the castle's placement (word at <c>+0x11e</c>).</summary>
    public ushort LastClock { get; private set; }

    /// <summary>The knight accumulator: a carry out of it owes a knight (word at <c>+0x126</c>).</summary>
    public ushort KnightAccumulator { get; private set; } = KnightStart;

    /// <summary>The knights owed, 0 to 2 (word at <c>+0x10c</c>).</summary>
    public int KnightsOwed { get; private set; }

    /// <summary>Whether the castle stands, so the supply turns.</summary>
    public bool Running { get; private set; }

    /// <summary>The castle is placed: the supply counts from the clock now (<c>Castle_Place</c> stores it, <c>0x442e4c</c>).</summary>
    public void Start()
    {
        LastClock = _clock.Now;
        Running = true;
    }

    /// <summary>
    /// The supply's turn in a pass, after the objects' turn: the clock units since its last turn come off the countdown, and each
    /// period that has passed makes one settler. A try that makes no knight still passes its period and feeds the accumulator.
    /// </summary>
    public void Turn()
    {
        if (!Running)
        {
            return;
        }

        var elapsed = (ushort)(_clock.Now - LastClock);
        LastClock = _clock.Now;
        var left = Countdown;
        Countdown = (ushort)(left - elapsed);
        if (elapsed <= left)
        {
            return;
        }

        bool carried;
        do
        {
            var accumulator = KnightAccumulator;
            KnightAccumulator = (ushort)(accumulator + KnightStep);
            if (accumulator + KnightStep > ushort.MaxValue)
            {
                KnightsOwed = Math.Min(KnightsOwed + 1, MostKnightsOwed);
            }

            MakeSettler();
            var countdown = Countdown;
            Countdown = (ushort)(countdown + Period);
            carried = countdown + Period > ushort.MaxValue;
        } while (!carried);
    }

    // One try: a settler without a job waits in the castle, on the castle's node, stamped with the clock. With a knight owed and a
    // sword and a shield in the stock it is made a knight at once and the stock gives up one of each.
    void MakeSettler()
    {
        var castle = _stock.Castle!.Value;
        var settler = new Settler(new Jobless(), 0, castle.X, castle.Y, _clock.Now, new WaitsInCastle());
        var slot = _settlers.Add(settler);
        _history.FillSettler(slot);
        if (KnightsOwed > 0 && _stock.Count(Sword) > 0 && _stock.Count(Shield) > 0)
        {
            settler.TakeUp(NewKnight);
            _stock.Take(Sword);
            _stock.Take(Shield);
            KnightsOwed--;
        }

        _history.RecordSettler(slot, EventType.SettlerMade, Cause.Settlers);
    }

    /// <summary>The supply's state in one line, for a test that compares it.</summary>
    public override string ToString() =>
        $"rate={Rate} period={Period} countdown={Countdown} last={LastClock} knights={KnightAccumulator},{KnightsOwed} running={Running}";
}
