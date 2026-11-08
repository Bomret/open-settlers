namespace OpenSettlers.Simulation;

/// <summary>
/// A record of one change a rule made to a settler, building, or flag (<c>CONTEXT.md</c>, ADR 0011).
/// </summary>
/// <param name="Tick">The tick the change happened in: <see cref="Game.TickCount"/> as the rule ran.</param>
/// <param name="Type">The rule that made the change, one of <see cref="EventType.All"/>.</param>
/// <param name="Thing">The thing that changed.</param>
/// <param name="State">The thing's state after the change.</param>
/// <param name="Cause">Why the change happened.</param>
/// <param name="Other">
/// The other thing the event names, as the ID it had when the event happened (<see cref="ThingId"/>, or a map object by its
/// node, <see cref="ThingId.OfObject"/>); null for none.
/// </param>
public sealed record ThingEvent(long Tick, string Type, ThingId Thing, ThingState State, Cause Cause, string? Other);
