namespace OpenSettlers.Simulation;

/// <summary>
/// A record of one change a rule made to a settler, building, or flag (<c>CONTEXT.md</c>, ADR 0011).
/// </summary>
public sealed record ThingEvent
{
    /// <summary>The tick the change happened in: <c>Game.TickCount</c> as the rule ran.</summary>
    public required long Tick { get; init; }

    /// <summary>The rule that made the change, one of <see cref="EventType.All"/>.</summary>
    public required string Type { get; init; }

    /// <summary>The thing that changed.</summary>
    public required ThingId Thing { get; init; }

    /// <summary>The thing's state after the change.</summary>
    public required ThingState State { get; init; }

    /// <summary>Why the change happened.</summary>
    public required Cause Cause { get; init; }

    /// <summary>
    /// The other thing the event names, as the ID it had when the event happened (<see cref="ThingId"/>, or a map object by its
    /// node, <see cref="ThingId.OfObject"/>); null for none.
    /// </summary>
    public required string? Other { get; init; }
}
