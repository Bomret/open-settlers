namespace OpenSettlers.Domain;

/// <summary>
/// A settler: one item of the original's settler table (<c>Table16</c>, <c>docs/rules/settlers.md</c>), one object for its
/// whole life. It holds its identity and body (the node it stands on, its player, its 16-bit wrapping timer and stamp, and
/// the frame of its walk), a <see cref="Domain.Job"/> that can change, and one <see cref="Activity"/> that holds only that
/// activity's data. The slot in the <see cref="SettlerTable"/> is the settler's number and its thing ID.
/// </summary>
public sealed class Settler
{
    /// <summary>A settler of <paramref name="job"/> and <paramref name="player"/> on the node, in <paramref name="activity"/>.</summary>
    public Settler(Job job, int player, int x, int y, ushort stamp, Activity activity)
    {
        Job = job;
        Player = player;
        X = x;
        Y = y;
        Stamp = stamp;
        Activity = activity;
    }

    /// <summary>The job. A settler without a job becomes a carrier when it takes a road; the settler stays the same.</summary>
    public Job Job { get; private set; }

    /// <summary>
    /// The settler takes up <paramref name="job"/>: a jobless settler a worker request sends out, a worker who becomes a carrier, a
    /// knight promoted. It stays the same object in the same slot, so its thing ID and its history go on.
    /// </summary>
    public void TakeUp(Job job) => Job = job;

    /// <summary>The player.</summary>
    public int Player { get; }

    /// <summary>The frame of the walk: the shape of the last step, which picks the walk's animation.</summary>
    public int Frame { get; set; }

    /// <summary>The time left in clock units until the settler acts again; a wrapped value is a debt.</summary>
    public ushort Timer { get; set; }

    /// <summary>The node the settler stands on, column.</summary>
    public int X { get; set; }

    /// <summary>The node the settler stands on, row.</summary>
    public int Y { get; set; }

    /// <summary>The clock (16 bits) at the settler's last turn.</summary>
    public ushort Stamp { get; set; }

    /// <summary>What the settler does now.</summary>
    public Activity Activity { get; set; }
}
