using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Turns the time of the frames into whole game ticks at <see cref="Game.TicksPerSecond"/>: the frame rate does not set
/// the tick rate, and the fraction of a tick a frame leaves carries to the next.
/// </summary>
public sealed class TickPacer
{
    double _carry;
    bool _held;

    /// <summary>The whole ticks that fall due after a frame of <paramref name="seconds"/>.</summary>
    public int Due(double seconds)
    {
        if (_held)
        {
            return 0;
        }

        _carry += seconds * Game.TicksPerSecond;
        var ticks = (int)_carry;
        _carry -= ticks;

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

    /// <summary>Drops the carry and releases a hold, as a new game starts.</summary>
    public void Reset()
    {
        _carry = 0;
        _held = false;
    }
}
