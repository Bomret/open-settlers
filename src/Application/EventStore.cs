using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// Every event of one game in the order recorded (ADR 0011). Always on, no cap. The <see cref="GameHistory"/> fills it; it is outside the state
/// hash and no rule reads it (INV-0007).
/// </summary>
public sealed class EventStore
{
    readonly List<ThingEvent> _events = [];
    readonly Dictionary<ThingId, List<ThingEvent>> _byThing = [];

    /// <summary>All events in the order they were recorded.</summary>
    public IReadOnlyList<ThingEvent> All => _events;

    /// <summary>The number of events.</summary>
    public int Count => _events.Count;

    /// <summary>The tick of the last event, or -1 for none.</summary>
    public long LastTick => _events.Count > 0 ? _events[^1].Tick : -1;

    /// <summary>The events of the thing in the order recorded; empty for a thing that has none.</summary>
    public IReadOnlyList<ThingEvent> Of(ThingId thing) => _byThing.TryGetValue(thing, out var events) ? events : [];

    internal void Add(ThingEvent thingEvent)
    {
        _events.Add(thingEvent);
        if (!_byThing.TryGetValue(thingEvent.Thing, out var events))
        {
            _byThing[thingEvent.Thing] = events = [];
        }

        events.Add(thingEvent);
    }
}
