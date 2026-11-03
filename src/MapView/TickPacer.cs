using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Turns the time of the frames into whole game ticks at <see cref="Game.TicksPerSecond"/>: the frame rate does not set
/// the tick rate, and the fraction of a tick a frame leaves carries to the next.
/// </summary>
public sealed class TickPacer
{
    private double _carry;

    /// <summary>The whole ticks that fall due after a frame of <paramref name="seconds"/>.</summary>
    public int Due(double seconds)
    {
        _carry += seconds * Game.TicksPerSecond;
        var ticks = (int)_carry;
        _carry -= ticks;

        return ticks;
    }

    /// <summary>Drops the carry, as a new game starts.</summary>
    public void Reset() => _carry = 0;
}
