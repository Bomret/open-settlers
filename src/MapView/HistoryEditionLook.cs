using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The look of a job under the History Edition source: the figure handler of the job (<c>0x43f744</c> plus 8 for each job), what it
/// adds to a settler's figure byte to make the figure word (<see cref="SettlerFigureTables"/>). A job without a look has no drawn
/// figure. The figure tables stay inside this source (ADR 0014, INV-0010).
/// </summary>
public abstract class HistoryEditionLook
{
    /// <summary>The job the look is for.</summary>
    public abstract Job Job { get; }

    /// <summary>What the look adds to <paramref name="figure"/>, the figure byte of the settler of <paramref name="draw"/>.</summary>
    public abstract int Add(SettlerDraw draw, int figure);
}

/// <summary>
/// The History Edition look of the job <typeparamref name="TJob"/> (ADR 0016, glossary: Job module), read from the job's data in the
/// <see cref="RuleTables"/>. A look derives from this type and declares a constructor that takes the <see cref="RuleTables"/> alone,
/// from which it reads the job's data; the generator writes <c>HistoryEditionLookRegistry</c> from the looks it finds, and fails the
/// build for a look with no such constructor and for a second look of one job.
/// </summary>
/// <typeparam name="TJob">The job the look is for.</typeparam>
public abstract class HistoryEditionLook<TJob> : HistoryEditionLook
    where TJob : Job, new()
{
    /// <inheritdoc />
    public sealed override Job Job { get; } = new TJob();
}

/// <summary>The figure handlers share the figure byte's top bit, which marks the frames of a settler's work.</summary>
internal static class FigureBytes
{
    // The handlers (0x43fb6b and its siblings) test the figure byte for bit 7.
    private const int HighFigure = 0x80;

    /// <summary>Whether the figure byte is one of the work's frames (from 0x80).</summary>
    public static bool IsWork(int figure) => figure >= HighFigure;

    /// <summary>The way back with a load: the free walk back to the hut's flag with the log or stone (<c>0x43fc0b</c>, <c>0x43ff40</c>).</summary>
    public static bool WayBackWithLoad(SettlerDraw draw) =>
        draw.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: true } };
}
