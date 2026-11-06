using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The types of an event (ADR 0011): each names the rule that made the change. The set is closed; a rule that records a new
/// kind of change adds its type here.
/// </summary>
public static class EventType
{
    /// <summary>The castle's placement: the castle, its flag, and its first settlers.</summary>
    public const string PlaceCastle = "place-castle";

    /// <summary>A flag's placement.</summary>
    public const string PlaceFlag = "place-flag";

    /// <summary>A road's building: the status and road bytes of the flags at its two ends.</summary>
    public const string BuildRoad = "build-road";

    /// <summary>A building's placement.</summary>
    public const string PlaceBuilding = "place-building";

    /// <summary>The layout entry that puts the castle's woodcutter in his hut.</summary>
    public const string LayoutInHut = "layout-in-hut";

    /// <summary>The layout entry that puts a carrier on its road.</summary>
    public const string LayoutWaitingOnRoad = "layout-waiting-on-road";

    /// <summary>The layout entry that puts a good on a flag.</summary>
    public const string LayoutGood = "layout-good";

    /// <summary>A road asks the castle for a carrier: the settler sent, and the status and road bytes of the road's flags.</summary>
    public const string CarrierRequested = "carrier-requested";

    /// <summary>A flag's turn changes the status bits of its roads.</summary>
    public const string VisitFlag = "visit-flag";

    /// <summary>The retry scan clears a flag's bit for a request that found no carrier.</summary>
    public const string ClearFlagRetryBits = "clear-flag-retry-bits";

    /// <summary>A carrier takes the road it was sent to.</summary>
    public const string EnterRoad = "enter-road";

    /// <summary>A carrier on its road waits out a step at a node.</summary>
    public const string WalkRoad = "walk-road";

    /// <summary>A carrier next to the flag at the end of its road waits.</summary>
    public const string Wait = "wait";

    /// <summary>Every type, for a check of a name.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        PlaceCastle,
        PlaceFlag,
        BuildRoad,
        PlaceBuilding,
        LayoutInHut,
        LayoutWaitingOnRoad,
        LayoutGood,
        CarrierRequested,
        VisitFlag,
        ClearFlagRetryBits,
        EnterRoad,
        WalkRoad,
        Wait,
    ];
}
