namespace OpenSettlers.Simulation;

/// <summary>
/// A settler: one item of the original's settler table (<c>Table16</c>, 16 bytes), kept field by field
/// (<c>docs/rules/settlers.md</c>). The slot in <see cref="Game.Settlers"/> is the settler's number. The fields hold
/// what the original's bytes hold, so the walk's arithmetic (16-bit timers that wrap) is the original's.
/// </summary>
public sealed class Settler
{
    internal Settler(int job, int player, int x, int y, ushort stamp, int state, int home)
    {
        Job = job;
        Player = player;
        X = x;
        Y = y;
        Stamp = stamp;
        State = state;
        Heading = home;
    }

    /// <summary>The job (byte 0 bits 2 to 6): 11 is the woodcutter, 21 a settler without a job yet.</summary>
    public int Job { get; internal set; }

    /// <summary>The player (byte 0 bits 0 and 1).</summary>
    public int Player { get; }

    /// <summary>The frame of the walk (byte 1): the shape of the last step, which picks the walk's animation.</summary>
    public int Frame { get; internal set; }

    /// <summary>The time left in clock units until the settler acts again (word at 2); a wrapped value is a debt.</summary>
    public ushort Timer { get; internal set; }

    /// <summary>The node the settler stands on, column.</summary>
    public int X { get; internal set; }

    /// <summary>The node the settler stands on, row.</summary>
    public int Y { get; internal set; }

    /// <summary>The clock (16 bits) at the settler's last turn (word at 8).</summary>
    public ushort Stamp { get; internal set; }

    /// <summary>The state (byte 10): 1 waits in the castle, 15 leaves it, 5 waits out a step, 2 walks, 4 enters a door.</summary>
    public int State { get; internal set; }

    /// <summary>What the settler walks to (byte 11): 255 is the building at the flag of <see cref="Target"/>.</summary>
    public int Mode { get; internal set; }

    /// <summary>The slot of the flag the settler walks to (word at 12); in state 4 the time the door step has left.</summary>
    public int Target { get; internal set; }

    /// <summary>
    /// Byte 14: the slot of the storage the settler waits in, or on the walk the direction back to the node it came
    /// from (a negative number marks a blocked step).
    /// </summary>
    public int Heading { get; internal set; }

    /// <summary>The state the settler takes when the step it waits out ends (byte 15).</summary>
    public int Next { get; internal set; }
}
