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

    /// <summary>A waiting carrier sees a good to fetch and turns toward it.</summary>
    public const string Fetch = "fetch";

    /// <summary>A carrier takes the node it stands on.</summary>
    public const string Claim = "claim";

    /// <summary>A carrier and a flag swap goods; also the flag's side of it.</summary>
    public const string Exchange = "exchange";

    /// <summary>A carrier turns to the building it brings a good to.</summary>
    public const string Deliver = "deliver";

    /// <summary>A carrier gives a good to a building, and the building's storage gains it.</summary>
    public const string Handover = "handover";

    /// <summary>Every type, for a check of a name.</summary>
    public static IReadOnlyList<string> All { get; } =
    [PlaceCastle, PlaceFlag, BuildRoad, PlaceBuilding, LayoutInHut, LayoutWaitingOnRoad, LayoutGood, Fetch, Claim, Exchange, Deliver, Handover];
}
