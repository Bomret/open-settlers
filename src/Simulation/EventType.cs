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

    /// <summary>A settler sent home takes the node it stands on.</summary>
    public const string ClaimedHome = "claimed-home";

    /// <summary>A settler sent home searches for the nearest flag with a road.</summary>
    public const string SearchedHome = "searched-home";

    /// <summary>A settler on its way home ends its free walk; on a flag with a road it starts the walk to the storage.</summary>
    public const string ArrivedHome = "arrived-home";

    /// <summary>A settler on its way home enters the storage.</summary>
    public const string EnteredStorage = "entered-storage";

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
        ClaimedHome,
        SearchedHome,
        ArrivedHome,
        EnteredStorage,
    ];
}
