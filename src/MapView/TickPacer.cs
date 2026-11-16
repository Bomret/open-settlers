using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Turns the time of the frames into whole game ticks at <see cref="Game.TicksPerSecond"/>: the frame rate does not set
/// the tick rate, and the fraction of a tick a frame leaves carries to the next. <see cref="Speed"/> multiplies the
/// rate. A frame runs at most 4 ticks times <see cref="Speed"/> (<see cref="Due"/>).
/// </summary>
public sealed class TickPacer
{
    double _carry;
    bool _held;

    /// <summary>
    /// The speed factor: 1, 2, 4, or 8, the factors of the History Edition's game-speed switch (<c>Speed_SetFactor</c>,
    /// <c>0x405fe0</c>, docs/rules/timing.md). It starts at 1, normal speed.
    /// </summary>
    public int Speed { get; private set; } = 1;

    /// <summary>
    /// Doubles <see cref="Speed"/>, and takes 8 back to 1 (stand-in: the order of the History Edition's switch is not
    /// recovered).
    /// </summary>
    public void NextSpeed() => Speed = Speed == 8 ? 1 : Speed * 2;

    /// <summary>
    /// The whole ticks that fall due after a frame of <paramref name="seconds"/>: at most 4 times <see cref="Speed"/>.
    /// The ticks past that bound, and the carry, are dropped.
    /// </summary>
    public int Due(double seconds)
    {
        if (_held)
        {
            return 0;
        }

        _carry += seconds * Game.TicksPerSecond * Speed;
        var ticks = (int)_carry;
        _carry -= ticks;

        // stand-in: the program has no such bound; a slow pass there gets more elapsed units (docs/rules/timing.md).
        // The bound keeps a long frame from running a backlog of ticks that makes the next frame longer still.
        var most = 4 * Speed;
        if (ticks > most)
        {
            _carry = 0;
            return most;
        }

        return ticks;
    }

    /// <summary>
    /// Holds the game: no tick falls due, and the time of the frames is dropped, until <see cref="ShotTaken"/> or <see cref="Reset"/>. A play
    /// run's fast-forward holds it so the next shot shows the tick it stopped at.
    /// </summary>
    public void Hold()
    {
        _held = true;
        _carry = 0;
    }

    /// <summary>A shot has been taken: lets a held game run on.</summary>
    public void ShotTaken() => _held = false;

    /// <summary>Drops the carry and releases a hold, as a new game starts. The speed stays.</summary>
    public void Reset()
    {
        _carry = 0;
        _held = false;
    }
}
