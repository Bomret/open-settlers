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

    /// <summary>A building asks the castle for a worker and a waiting settler is sent out; the other thing is the one at the other end.</summary>
    public const string WorkerRequested = "worker-requested";

    /// <summary>A building's handler finds no worker and marks the request to be made again.</summary>
    public const string BuildingHandled = "building-handled";

    /// <summary>The scan that clears the retry bit of a building's status.</summary>
    public const string RetryBitsCleared = "retry-bits-cleared";

    /// <summary>A settler's turn that only waits out or counts down its timer.</summary>
    public const string Visited = "visited";

    /// <summary>A knight's turn in the castle: its timer and the draws for a promotion.</summary>
    public const string KnightIdled = "knight-idled";

    /// <summary>The settler that stands by while the castle is built adds to the castle's progress.</summary>
    public const string Builder = "builder";

    /// <summary>A settler leaves the castle by its door, or waits for a busy node.</summary>
    public const string Left = "left";

    /// <summary>A settler takes another node.</summary>
    public const string Moved = "moved";

    /// <summary>A settler on the road chooses its way, or turns away from a flag no road leads on from.</summary>
    public const string Walked = "walked";

    /// <summary>A settler's step on a road, or the wait for a node another settler stands on.</summary>
    public const string Stepped = "stepped";

    /// <summary>A settler at a flag goes on to the building there.</summary>
    public const string Entered = "entered";

    /// <summary>A flag taken down (removal).</summary>
    public const string TookFlagDown = "took-flag-down";

    /// <summary>A building taken down (removal).</summary>
    public const string TookBuildingDown = "took-building-down";

    /// <summary>The flag at the end of a road that was taken down (removal).</summary>
    public const string EndOfRoad = "end-of-road";

    /// <summary>A settler on a road that goes is dismissed (removal).</summary>
    public const string Dismiss = "dismiss";

    /// <summary>A carrier that waits next to a flag is told to go home (removal).</summary>
    public const string DismissWaiting = "dismiss-waiting";

    /// <summary>A carrier that waits next to a flag is taken with the settlers of a road (removal).</summary>
    public const string TakeWaiting = "take-waiting";

    /// <summary>A dismissed carrier that waited goes home (removal).</summary>
    public const string Dismissed = "dismissed";

    /// <summary>A carrier that waited at the end of a joined road takes its place on it (removal).</summary>
    public const string Rejoined = "rejoined";

    /// <summary>A good lost is counted back against the building it was bound for (removal).</summary>
    public const string CountLost = "count-lost";

    /// <summary>A settler on a road to be joined is taken with it (removal).</summary>
    public const string Gather = "gather";

    /// <summary>The join of the two roads of a flag taken down (removal).</summary>
    public const string JoinRoads = "join-roads";

    /// <summary>The worker of a building taken down walks home (removal).</summary>
    public const string SendWorkerHome = "send-worker-home";

    /// <summary>What waits for a building taken down is released (removal).</summary>
    public const string ReleaseGoodsFor = "release-goods-for";

    /// <summary>A building that burns loses time off its countdown (removal).</summary>
    public const string Burn = "burn";

    /// <summary>A burnt building's slot is freed: the event that ends the building (removal).</summary>
    public const string FreeBuilding = "free-building";

    /// <summary>A flag's slot is freed: the event that ends the flag (removal).</summary>
    public const string FreeFlagSlot = "free-flag-slot";

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
        WorkerRequested,
        BuildingHandled,
        RetryBitsCleared,
        Visited,
        KnightIdled,
        Builder,
        Left,
        Moved,
        Walked,
        Stepped,
        Entered,
        TookFlagDown,
        TookBuildingDown,
        EndOfRoad,
        Dismiss,
        DismissWaiting,
        TakeWaiting,
        Dismissed,
        Rejoined,
        CountLost,
        Gather,
        JoinRoads,
        SendWorkerHome,
        ReleaseGoodsFor,
        Burn,
        FreeBuilding,
        FreeFlagSlot,
    ];
}
