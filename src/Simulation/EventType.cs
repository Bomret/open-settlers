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

    /// <summary>The woodcutter's turn at a door: the timer runs down, then he leaves the hut or goes on to his job.</summary>
    public const string Door = "door";

    /// <summary>The woodcutter's search for a tree.</summary>
    public const string SearchTree = "search-tree";

    /// <summary>The woodcutter's step from the hut onto its flag.</summary>
    public const string StepOut = "step-out";

    /// <summary>A step of a settler's free walk.</summary>
    public const string FreeWalk = "free-walk";

    /// <summary>A settler's arrival at the end of a free walk.</summary>
    public const string Arrive = "arrive";

    /// <summary>The woodcutter's cut; the other thing is the tree, named by its node.</summary>
    public const string CutTree = "cut-tree";

    /// <summary>A good put on a flag; the other thing is the flag on the settler's event and the settler on the flag's.</summary>
    public const string PutGood = "put-good";

    /// <summary>A settler's step from a flag into the building.</summary>
    public const string EnterHut = "enter-hut";

    /// <summary>Every type, for a check of a name.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        PlaceCastle, PlaceFlag, BuildRoad, PlaceBuilding, LayoutInHut, LayoutWaitingOnRoad, LayoutGood,
        Door, SearchTree, StepOut, FreeWalk, Arrive, CutTree, PutGood, EnterHut,
    ];
}
