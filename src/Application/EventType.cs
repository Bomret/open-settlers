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

    /// <summary>The stonecutter's search for a stone heap.</summary>
    public const string SearchStone = "search-stone";

    /// <summary>The stonecutter's cut; the other thing is the heap, named by its node.</summary>
    public const string CutStone = "cut-stone";

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

    /// <summary>A settler sent home takes the node it stands on.</summary>
    public const string ClaimedHome = "claimed-home";

    /// <summary>A settler sent home searches for the nearest flag with a road.</summary>
    public const string SearchedHome = "searched-home";

    /// <summary>A settler on its way home ends its free walk; on a flag with a road it starts the walk to the storage.</summary>
    public const string ArrivedHome = "arrived-home";

    /// <summary>A settler on its way home enters the storage.</summary>
    public const string EnteredStorage = "entered-storage";

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
    public const string CastleSettlerBuilds = "castle-settler-builds";

    /// <summary>A builder's turn in the building site: a blow, a material taken, or the wait for the next one.</summary>
    public const string Builds = "builds";

    /// <summary>The builder's last blow: the building is finished and its flag asks for nothing.</summary>
    public const string BuildingFinished = "building-finished";

    /// <summary>The builder of a finished building waits at its door for the node of the flag, or steps onto the flag.</summary>
    public const string LeavesSite = "leaves-site";

    /// <summary>A settler leaves the castle by its door, or waits for a busy node.</summary>
    public const string Left = "left";

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

    /// <summary>A flag put on a road splits it into two roads: the new flag, the far flags, and the carriers on the halves.</summary>
    public const string SplitRoads = "split-roads";

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

    /// <summary>The castle hands a good of its stock out to a request: the good lies on the castle's flag, bound for the request's flag.</summary>
    public const string Handout = "handout";

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
        WorkerRequested,
        BuildingHandled,
        RetryBitsCleared,
        Visited,
        KnightIdled,
        CastleSettlerBuilds,
        Left,
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
        SplitRoads,
        SendWorkerHome,
        ReleaseGoodsFor,
        Burn,
        FreeBuilding,
        FreeFlagSlot,
        Door,
        SearchTree,
        SearchStone,
        CutStone,
        StepOut,
        FreeWalk,
        Arrive,
        CutTree,
        PutGood,
        EnterHut,
        CarrierRequested,
        VisitFlag,
        ClearFlagRetryBits,
        EnterRoad,
        WalkRoad,
        Wait,
        Fetch,
        Claim,
        Exchange,
        Deliver,
        Handover,
        Handout,
    ];
}
