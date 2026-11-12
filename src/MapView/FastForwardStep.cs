using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// A <c>fast-forward</c> step: in the game view, runs <see cref="Game.Tick"/> without drawing until its condition holds.
/// <see cref="FastForwardJobStep"/> waits for a settler of a job, <see cref="FastForwardUntilStep"/> for a condition.
/// </summary>
public abstract record FastForwardStep : PlayStep
{
    /// <summary>The most ticks the step runs; a condition first holding after that many ticks still counts as reached.</summary>
    public required int MaxTicks { get; init; }

    /// <summary>The step as the script wrote it.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// Whether the step's condition holds on <paramref name="game"/>. <paramref name="state"/> builds what a play run
    /// observes, called with whether the state lists the things.
    /// </summary>
    public abstract bool Holds(Game game, Func<bool, PlayState> state);

    /// <summary>
    /// Ticks the game until the condition holds, none where it holds already. Null when it holds; else the problem,
    /// naming the step and the ticks run.
    /// </summary>
    public string? Run(Game game, Func<bool, PlayState> state)
    {
        for (var ticks = 0; !Holds(game, state); ticks++)
        {
            if (ticks == MaxTicks)
                return $"{Text}: not reached after {ticks} ticks";

            game.Tick();
        }

        return null;
    }

    /// <summary>
    /// The step in a view: <see cref="Run"/> on its game, then holds <paramref name="pacer"/> until the next shot. A
    /// view with no game view (the map view) or no game refuses it; null when the step is met, else the problem.
    /// </summary>
    public string? Apply(Game? game, bool gameView, TickPacer pacer, Func<bool, PlayState> state)
    {
        if (!gameView)
            return "fast-forward needs the game view, use --game-view";
        if (game is null)
            return "fast-forward: no game is running";

        var problem = Run(game, state);
        pacer.Hold();

        return problem;
    }
}

/// <summary>
/// <c>fast-forward &lt;job&gt; state &lt;n&gt; max &lt;ticks&gt;</c> or <c>fast-forward &lt;job&gt; carrying max
/// &lt;ticks&gt;</c>: runs until some settler of the job is in the activity whose original state code is the number, or
/// carries a good.
/// </summary>
public sealed record FastForwardJobStep : FastForwardStep
{
    /// <summary>The job to watch: a job the name table names (<see cref="Named"/>).</summary>
    public required Job Job { get; init; }

    /// <summary>
    /// The code of the original's settler state the settler's activity stands for, as the play script writes it (a number of
    /// the original); null waits for a settler that carries a good.
    /// </summary>
    public int? State { get; init; }

    /// <summary>
    /// The job the name table names <paramref name="name"/>, found by trying each job number through
    /// <see cref="Domain.Job.Of"/>; null for a name the table does not hold. Each name stands for one job (ADR 0010).
    /// </summary>
    public static Job? Named(string name)
    {
        for (var code = 0; code < 256; code++)
        {
            if (Simulation.NameTable.JobName(code) == name)
                return Domain.Job.Of(code);
        }

        return null;
    }

    /// <summary>The job names the name table holds, in job-number order.</summary>
    public static IEnumerable<string> Names() =>
        Enumerable.Range(0, 256).Select(Simulation.NameTable.JobName).OfType<string>().Distinct();

    /// <summary>Whether some settler of the job meets the condition; reads the game alone and builds no state.</summary>
    public override bool Holds(Game game, Func<bool, PlayState> state)
    {
        foreach (var settler in game.Settlers)
        {
            // The raw-code boundary: a play script writes the original's state numbers, so State is compared with the
            // activity's original state code.
            if (settler.Job == Job && (State is { } code ? settler.Activity.Code == code : settler.Activity.Carries))
                return true;
        }

        return false;
    }
}

/// <summary>
/// <c>fast-forward until &lt;condition&gt; max &lt;ticks&gt;</c>: runs until the condition (an <c>expect</c> on a game
/// field or an <c>expect-count</c>, without its step word) holds.
/// </summary>
public sealed record FastForwardUntilStep : FastForwardStep
{
    /// <summary>The condition the step waits for: an <see cref="ExpectStep"/> or an <see cref="ExpectCountStep"/>.</summary>
    public required IPlayCondition Until { get; init; }

    /// <summary>
    /// Whether the condition holds on what <paramref name="state"/> observes. <paramref name="state"/> is called with the
    /// condition's <see cref="IPlayCondition.ReadsThings"/>, so a condition on a game field lists no things.
    /// </summary>
    public override bool Holds(Game game, Func<bool, PlayState> state) => Until.Check(state(Until.ReadsThings)) is null;
}
