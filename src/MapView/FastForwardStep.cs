using System;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// <c>fast-forward &lt;job&gt; state &lt;n&gt; max &lt;ticks&gt;</c>, <c>fast-forward &lt;job&gt; carrying max &lt;ticks&gt;</c>, or
/// <c>fast-forward until &lt;condition&gt; max &lt;ticks&gt;</c>: in the game view, runs <see cref="Game.Tick"/> without drawing until
/// some settler of the job is in the activity whose original state code is the number, carries a good, or the condition (an
/// <c>expect</c> on a game field or an <c>expect-count</c>, without its step word) holds.
/// </summary>
public sealed record FastForwardStep : PlayStep
{
    /// <summary>The job to watch: <see cref="Domain.Job.Woodcutter"/> or <see cref="Domain.Job.Carrier"/>; null with <see cref="Until"/>.</summary>
    public Job? Job { get; init; }

    /// <summary>The condition an <c>until</c> step waits for: an <see cref="ExpectStep"/> or an <see cref="ExpectCountStep"/>.</summary>
    public PlayStep? Until { get; init; }

    /// <summary>
    /// The code of the original's settler state the settler's activity stands for, as the play script writes it (a number of
    /// the original); null waits for a settler that carries a good.
    /// </summary>
    public int? State { get; init; }

    /// <summary>The most ticks the step runs; a condition first holding after that many ticks still counts as reached.</summary>
    public required int MaxTicks { get; init; }

    /// <summary>
    /// Whether the settler carries a good. A carrier does while it walks its road with a good in its hands. A woodcutter
    /// does on the way back from the tree, the log the hut's flag takes on arrival.
    /// </summary>
    public static bool Carries(Settler settler) =>
        settler.Job switch
        {
            var job when job == Domain.Job.Carrier => settler.Activity is WalksRoad { Load.Good.IsNone: false },
            var job when job == Domain.Job.Woodcutter => settler.Activity
                is WalksFree { Trip.Purpose: BackToHut { Carrying: true } },
            _ => false,
        };

    /// <summary>
    /// Whether some settler of the job meets the condition, or the <see cref="Until"/> condition holds on what
    /// <paramref name="state"/> observes.
    /// </summary>
    public bool Holds(Game game, Func<PlayState>? state = null)
    {
        if (Until is not null)
        {
            var now = state?.Invoke() ?? throw new InvalidOperationException("an until step needs the play state");

            return (Until is ExpectStep expect ? now.Check(expect) : now.Check((ExpectCountStep)Until)) is null;
        }

        foreach (var settler in game.Settlers)
        {
            // The raw-code boundary: a play script writes the original's state numbers, so State is compared with the
            // activity's original state code.
            if (settler.Job == Job && (State is { } code ? settler.Activity.Code == code : Carries(settler)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ticks the game until the condition holds, none where it holds already. Null when it holds; else the problem,
    /// naming the step and the ticks run.
    /// </summary>
    public string? Run(Game game, Func<PlayState>? state = null)
    {
        for (var ticks = 0; !Holds(game, state); ticks++)
        {
            if (ticks == MaxTicks)
                return $"{Text}: not reached after {ticks} ticks";

            game.Tick();
        }

        return null;
    }

    /// <summary>The step as the script wrote it.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The step in a view: <see cref="Run"/> on its game, then holds <paramref name="pacer"/> until the next shot. A
    /// view with no game view (the map view) or no game refuses it; null when the step is met, else the problem.
    /// </summary>
    public string? Apply(Game? game, bool gameView, TickPacer pacer, Func<PlayState>? state = null)
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
