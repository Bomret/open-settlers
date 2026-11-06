using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// <c>fast-forward &lt;job&gt; state &lt;n&gt; max &lt;ticks&gt;</c> or <c>fast-forward &lt;job&gt; carrying max &lt;ticks&gt;</c>: in the game
/// view, runs <see cref="Game.Tick"/> without drawing until some settler of the job is in the state, or carries a good.
/// </summary>
public sealed record FastForwardStep : PlayStep
{
    /// <summary>The job to watch: <see cref="Settler.WoodcutterJob"/> or <see cref="Settler.CarrierJob"/>.</summary>
    public required int Job { get; init; }

    /// <summary>The state to wait for; null waits for a settler that carries a good.</summary>
    public int? State { get; init; }

    /// <summary>The most ticks the step runs; a condition first holding after that many ticks still counts as reached.</summary>
    public required int MaxTicks { get; init; }

    /// <summary>
    /// Whether the settler carries a good. A carrier does while it walks its road (state 3) with a good in its mode. A
    /// woodcutter does on the way back from the tree (the way-back mark in the high byte of its target, a positive south
    /// count in its heading), the log the hut's flag takes on arrival.
    /// </summary>
    public static bool Carries(Settler settler) =>
        settler.Job switch
        {
            Settler.CarrierJob => settler is { State: 3, Mode: > 0 },
            Settler.WoodcutterJob => settler.Target >> 8 == 0x80 && settler.Heading > 0,
            _ => false,
        };

    /// <summary>Whether some settler of the job meets the condition.</summary>
    public bool Holds(Game game)
    {
        foreach (var settler in game.Settlers)
        {
            if (settler.Job == Job && (State is { } state ? settler.State == state : Carries(settler)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ticks the game until the condition holds, none where it holds already. Null when it holds; else the problem,
    /// naming the step and the ticks run.
    /// </summary>
    public string? Run(Game game)
    {
        for (var ticks = 0; !Holds(game); ticks++)
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
    public string? Apply(Game? game, bool gameView, TickPacer pacer)
    {
        if (!gameView)
            return "fast-forward needs the game view, use --game-view";
        if (game is null)
            return "fast-forward: no game is running";

        var problem = Run(game);
        pacer.Hold();

        return problem;
    }
}
