using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// The tables of the original's program data that the rules read, as typed values: the object classes, the start stock,
/// the step times, and the others below. They come from the install's <c>Siedler1.exe</c> and are never restated in
/// source (INV-0001): <c>ProgramTablesImporter</c> in the History Edition source builds them from the program's bytes, and a test
/// builds its own from made-up values. The rules read the typed accessors (<see cref="ClassOf"/>, <see cref="StepTime"/>,
/// <see cref="DoorSpeed(int)"/>, and the others). The raw tables the importer's tests check against the original, and the
/// <see cref="Figures"/> the map view draws from, are read-only values: no member hands out the stored data for change, and the constructor
/// copies the arrays it is given.
/// </summary>
public sealed class RuleTables
{
    /// <summary>The number of object codes, and the size of the class table.</summary>
    public const int ClassCount = 128;

    /// <summary>The number of goods of the start stock rows and of the transport priority table.</summary>
    public const int Goods = 26;

    /// <summary>The number of rows of the start stock table (the levels 0, 10, 20, 30, and 40).</summary>
    public const int StartStockRows = 5;

    /// <summary>
    /// The first index of <see cref="StepTimes"/> the table holds: a step into a node much lower than its start
    /// reads the program bytes before the table, as the original does.
    /// </summary>
    public const int StepTimeFirst = -32;

    /// <summary>The number of words of <see cref="StepTimes"/> (the indices <see cref="StepTimeFirst"/> to 127).</summary>
    public const int StepTimeCount = 160;

    /// <summary>The number of building types the door speed table covers.</summary>
    public const int DoorSpeedCount = 64;

    /// <summary>The number of entries of the scan window table, one for each map size value.</summary>
    public const int ScanWindowCount = 8;

    /// <summary>
    /// The number of building types the size and needs tables cover: the types 0 to 0x17 (the size table at <c>0x42d307</c> and the
    /// needs table at <c>0x449f18</c> hold no entry of the castle, type 0x18).
    /// </summary>
    public const int BuildingTypeCount = 24;

    /// <summary>The number of rows of the figure bases (the figure word's high byte, 0 to 0x88).</summary>
    public const int FigureBaseRowCount = 137;

    /// <summary>The number of goods the carried figure table covers, good 0 included.</summary>
    public const int CarriedFigureCount = 27;

    /// <summary>The number of frames the frame row table covers.</summary>
    public const int FrameRowCount = 181;

    /// <summary>The number of phases of the waiting carriers.</summary>
    public const int IdlePhaseCount = 16;

    /// <summary>The number of figure words of the waiting carriers.</summary>
    public const int IdleFigureCount = 128;

    /// <summary>The number of offsets of the waiting carriers.</summary>
    public const int IdleOffsetCount = 128;

    /// <summary>The number of length classes of a road.</summary>
    public const int RoadCapacityCount = 8;

    /// <summary>The number of rows of the free walk's direction choice.</summary>
    public const int WalkRowCount = 24;

    /// <summary>The number of candidates of a row of the free walk's direction choice, and its width in the program.</summary>
    public const int WalkRowSize = 8;

    readonly ImmutableArray<ObjectClass> _classes;
    readonly ImmutableArray<byte> _roadCapacities;
    readonly ImmutableArray<byte> _walkRows;
    readonly ImmutableArray<byte> _doorSpeeds;
    readonly ImmutableArray<ushort> _scanWindows;
    readonly ImmutableArray<byte> _buildingSizes;
    readonly ImmutableArray<byte> _buildingNeeds;
    readonly ImmutableArray<ImmutableArray<DistributionStep>> _distribution;
    readonly ImmutableArray<WorkerRequest?> _workers;
    readonly ImmutableArray<ushort> _materialOrder;
    readonly ImmutableArray<ushort> _blowSteps;

    /// <summary>
    /// Creates tables from their contents. The tables of the settlers' walk (<paramref name="stepTimes"/>,
    /// <paramref name="doorSpeeds"/>, <paramref name="scanWindows"/>) are empty where a test needs none; the walk then
    /// reads them as zeros.
    /// </summary>
    public RuleTables(
        ObjectClass[] classes,
        byte[][] startStock,
        ushort[]? stepTimes = null,
        byte[]? doorSpeeds = null,
        ushort[]? scanWindows = null,
        short[]? figureBases = null,
        short[]? figureOffsets = null,
        byte[]? walkRows = null,
        short[]? carriedFigures = null,
        byte[]? roadCapacities = null,
        sbyte[]? frameRows = null,
        short[]? idlePhases = null,
        short[]? idleFigures = null,
        sbyte[]? idleOffsets = null,
        byte[]? buildingSizes = null,
        byte[]? buildingNeeds = null,
        DistributionStep[][]? distribution = null,
        byte plankRankBase = 0,
        byte millRankBase = 0,
        WorkerRequest?[]? workers = null,
        ushort[]? materialOrder = null,
        ushort[]? blowSteps = null,
        IEnumerable<(Job Job, object Data)>? jobData = null,
        ContentCatalog? goods = null
    )
    {
        if (
            classes.Length != ClassCount
            || startStock.Length != StartStockRows
            || startStock.Any(row => row.Length != Goods)
        )
        {
            throw new ArgumentException("A class table holds 128 bytes and a start stock table 5 rows of 26.");
        }

        // Every table is copied, the goods of each distribution step included, so no caller keeps a hand on what the tables hold.
        _classes = [.. classes];
        StartStock = [.. startStock.Select(row => row.ToImmutableArray())];
        StepTimes = [.. stepTimes ?? new ushort[StepTimeCount]];
        _doorSpeeds = [.. doorSpeeds ?? new byte[DoorSpeedCount]];
        _scanWindows = [.. scanWindows ?? new ushort[ScanWindowCount]];
        _walkRows = [.. walkRows ?? new byte[WalkRowCount * WalkRowSize]];
        _roadCapacities = [.. roadCapacities ?? new byte[RoadCapacityCount]];
        // stand-in: a catalog a test does not give holds no good, so every good reads as the stand-in facts of the accessors below.
        Catalog = goods ?? ContentCatalog.Empty;
        Figures = new FigureTables(
            figureBases,
            figureOffsets,
            carriedFigures,
            idlePhases,
            idleFigures,
            idleOffsets,
            frameRows
        );
        // stand-in: a table a test does not give makes every type small, so a test that places a building needs no table.
        _buildingSizes = [.. buildingSizes ?? [.. Enumerable.Repeat((byte)1, BuildingTypeCount)]];
        _buildingNeeds = [.. buildingNeeds ?? new byte[2 * BuildingTypeCount]];
        // stand-in: a table a test does not give hands no good out.
        _distribution =
        [
            .. (
                distribution
                ??
                [
                    [],
                    [],
                    [],
                ]
            ).Select(order =>
                order.Select(step => step with { Goods = step.Goods.ToImmutableArray() }).ToImmutableArray()
            ),
        ];
        PlankRankBase = plankRankBase;
        MillRankBase = millRankBase;
        // stand-in: a table a test does not give makes no building ask for a worker.
        _workers = [.. workers ?? new WorkerRequest?[BuildingTypeCount]];
        // stand-in: a table a test does not give makes every material a plank and every blow add nothing.
        _materialOrder = [.. materialOrder ?? new ushort[BuildingTypeCount]];
        _blowSteps = [.. blowSteps ?? new ushort[2 * BuildingTypeCount]];
        // A job's data is exactly what is given: a job's data that is missing is an error when read (see Get).
        _jobData = [];
        foreach (var (job, data) in jobData ?? [])
        {
            _jobData[(job, data.GetType())] = data;
        }
    }

    readonly Dictionary<(Job, Type), object> _jobData;

    /// <summary>
    /// The data of type <typeparamref name="T"/> that the job <paramref name="job"/> owns: the figure adds of its handler, or the
    /// request a site makes for it. The importer reads each from the job's tables module. Throws where the tables hold none: a job's
    /// missing data is an error and no default.
    /// </summary>
    public T Get<T>(Job job)
        where T : notnull =>
        _jobData.TryGetValue((job, typeof(T)), out var data)
            ? (T)data
            : throw new InvalidOperationException(
                $"The tables hold no {typeof(T).Name} for the job {job.GetType().Name}."
            );

    /// <summary>The class of each object code.</summary>
    public ImmutableArray<ObjectClass> Classes => _classes;

    /// <summary>The class of the object <paramref name="mapObject"/>.</summary>
    public ObjectClass ClassOf(MapObject mapObject) => _classes[mapObject.Code];

    /// <summary>The start stock rows, each <see cref="Goods"/> counts.</summary>
    public ImmutableArray<ImmutableArray<byte>> StartStock { get; }

    /// <summary>The figure tables the settlers are drawn from.</summary>
    public FigureTables Figures { get; }

    /// <summary>
    /// The goods of the game, one entry each (ADR 0020): the content set's source builds it, and the accessors below read the facts of
    /// a good from its entry. A good the catalog lacks reads as the stand-in facts the accessors state.
    /// </summary>
    public ContentCatalog Catalog { get; }

    /// <summary>
    /// The default transport priority of the good <paramref name="good"/>: the rank of its entry, each rank given once; the carrier's
    /// exchange lines up the good of the highest rank next (<c>docs/rules/goods.md</c>). 0 for no good and for a good the catalog lacks.
    /// </summary>
    public int TransportPriority(Good good) => Catalog.EntryOf(good)?.TransportRank ?? 0;

    /// <summary>
    /// Which byte of a building's fill a good counts in on its way: the counter of its entry, the one rule for every place that counts
    /// a good on its way (the request search and the storage's hand-out) and counts it back where it is lost (<c>docs/rules/removal.md</c>).
    /// A good the catalog lacks counts in the low byte.
    /// </summary>
    public FillCounter FillCounterOf(Good good) => Catalog.EntryOf(good)?.Counter ?? FillCounter.Low;

    /// <summary>
    /// Which byte of a building's fill the carrier's handover of <paramref name="good"/> counts in: the handover counter of its entry
    /// (the rule at <c>0x43c4ea</c>, <c>docs/rules/goods.md</c>). A good the catalog lacks counts in the low byte.
    /// </summary>
    public FillCounter HandoverCounterOf(Good good) => Catalog.EntryOf(good)?.HandoverCounter ?? FillCounter.Low;

    /// <summary>Whether a lost good of this kind counts against the total of resource 1, as its entry states; false for a good the catalog lacks.</summary>
    public bool CountsAgainstResource1(Good good) => Catalog.EntryOf(good)?.CountsAgainstResource1 ?? false;

    /// <summary>The number of carriers each length class of a road holds at most, as the importer read them.</summary>
    public ImmutableArray<byte> RoadCapacities => _roadCapacities;

    /// <summary>The number of carriers a road of the length class <paramref name="lengthClass"/> holds at most.</summary>
    public byte RoadCapacity(int lengthClass) => _roadCapacities[lengthClass];

    /// <summary>The rows of the free walk's direction choice as the importer read them.</summary>
    public ImmutableArray<byte> WalkRows => _walkRows;

    /// <summary>
    /// The direction, 0 east to 5 north, of the candidate <paramref name="candidate"/> (0 to 5, best first) of the walk row
    /// <paramref name="row"/>, which the heading class of the vector left to walk picks (<c>docs/rules/woodcutting.md</c>);
    /// -1 where the table holds no direction there.
    /// </summary>
    public int WalkDirection(int row, int candidate)
    {
        var code = _walkRows[row * WalkRowSize + candidate];

        return code is >= 4 and <= 24 && code % 4 == 0 ? code / 4 - 1 : -1;
    }

    /// <summary>
    /// The time in clock units of a step, by step shape: <c>4 + 9 * direction + (height of the node reached minus
    /// height of the node left)</c>, with the directions east 0 to north 5 (<c>docs/rules/settlers.md</c>). Index with
    /// <see cref="StepTime"/>.
    /// </summary>
    public ImmutableArray<ushort> StepTimes { get; }

    /// <summary>The time of the step shape <paramref name="shape"/>; a shape outside the table reads as 0.</summary>
    public ushort StepTime(int shape)
    {
        var index = shape - StepTimeFirst;

        return index >= 0 && index < StepTimes.Length ? StepTimes[index] : (ushort)0;
    }

    /// <summary>The speed table of the doors as the importer read it.</summary>
    public ImmutableArray<byte> DoorSpeeds => _doorSpeeds;

    /// <summary>The speed of the door of the building type <paramref name="buildingType"/>, in 32nds of a step time.</summary>
    public byte DoorSpeed(int buildingType) => _doorSpeeds[buildingType];

    /// <summary>
    /// The speed of the door of <paramref name="building"/> (the table is indexed by its type plus 0x20 while it is built), or
    /// of no building, the first entry, where there is none.
    /// </summary>
    public byte DoorSpeed(Building? building) =>
        _doorSpeeds[building is null ? 0 : building.Type.Code | (building.Constructing ? 0x20 : 0)];

    /// <summary>The size table as the importer read it: one byte for each building type, 0 a mine, 1 small, 2 large.</summary>
    public ImmutableArray<byte> BuildingSizes => _buildingSizes;

    /// <summary>
    /// How much room the building type <paramref name="type"/> takes (<c>0x42d307</c>, <c>docs/rules/build-sites.md</c>). A type
    /// outside the table, the castle included, reads as a mine, which no placement builds.
    /// </summary>
    public BuildingSize SizeOf(BuildingType type) =>
        type.Code is >= 0 and < BuildingTypeCount && _buildingSizes[type.Code] is 1 or 2
            ? (BuildingSize)_buildingSizes[type.Code]
            : BuildingSize.Mine;

    /// <summary>The needs table as the importer read it: planks then stones for each building type.</summary>
    public ImmutableArray<byte> BuildingNeedsTable => _buildingNeeds;

    /// <summary>
    /// The planks and stones the building type <paramref name="type"/> needs (<c>0x449f18</c>); none for a type outside the table.
    /// </summary>
    public BuildingNeeds NeedsOf(BuildingType type) =>
        type.Code is >= 0 and < BuildingTypeCount
            ? new BuildingNeeds { Planks = _buildingNeeds[2 * type.Code], Stones = _buildingNeeds[2 * type.Code + 1] }
            : new BuildingNeeds { Planks = 0, Stones = 0 };

    /// <summary>
    /// The request a good with no destination looks for (<c>0x464117</c>): its class and the request word of the flags that ask for
    /// it: the request of its entry; null for a good that goes to storage at once, and for a good the catalog lacks.
    /// </summary>
    public GoodClass? ClassOfGood(Good good) => Catalog.EntryOf(good)?.Request;

    /// <summary>
    /// The order a storage serves the requests in, by the draw the phase routine takes at each cycle (<c>0x42965c</c>): the draw taken
    /// to 0 to 7 picks the second table at 0, the third at 1, and the first at any other value.
    /// </summary>
    public ImmutableArray<DistributionStep> DistributionOrder(int draw) =>
        (draw & 7) switch
        {
            0 => _distribution[1],
            1 => _distribution[2],
            _ => _distribution[0],
        };

    /// <summary>
    /// The rank a building site's request for planks starts from: the high byte of the player word at <c>+0x148</c>, which the
    /// player setup of a new game writes (<c>0x445752</c>, <c>docs/rules/construction-materials.md</c>).
    /// </summary>
    public byte PlankRankBase { get; }

    /// <summary>
    /// The rank the mill's request for grain starts from: the high byte of the player word at <c>+0x15a</c>, which the player setup of a
    /// new game writes (<c>0x44579c</c>, <c>docs/rules/milling.md</c>). The mill's handler (<c>0x42e2a0</c>) shifts it right by the goods
    /// the mill holds.
    /// </summary>
    public byte MillRankBase { get; }

    /// <summary>
    /// What a finished building of <paramref name="type"/> asks for as its worker (the handlers of the jump table <c>0x42cd1d</c>); null for
    /// a type whose handler asks for none of this kind, the castle included.
    /// </summary>
    public WorkerRequest? WorkerOf(BuildingType type) =>
        type.Code is >= 0 and < BuildingTypeCount ? _workers[type.Code] : null;

    /// <summary>
    /// Whether the material the builder takes in turn <paramref name="index"/> of a building of <paramref name="type"/> is a stone, not a
    /// plank (the order words at <c>0x43f0ba</c>, one for each type: a set bit means a stone, <c>docs/rules/construction.md</c>).
    /// </summary>
    public bool StoneAt(BuildingType type, int index) =>
        type.Code is >= 0 and < BuildingTypeCount
        && index is >= 0 and < 16
        && (_materialOrder[type.Code] >> index & 1) != 0;

    /// <summary>
    /// What a blow of the builder adds to the building's progress word (<c>0x43f0fa</c>, two words for each type): the first before the word
    /// reaches <c>0x8000</c>, the second from there on; 0 for a type outside the table.
    /// </summary>
    public ushort BlowStep(BuildingType type, bool late) =>
        type.Code is >= 0 and < BuildingTypeCount ? _blowSteps[2 * type.Code + (late ? 1 : 0)] : (ushort)0;

    /// <summary>The scan window table as the importer read it.</summary>
    public ImmutableArray<ushort> ScanWindows => _scanWindows;

    /// <summary>
    /// The number of buildings the retry scan clears in one pass on a map of the size value <paramref name="sizeValue"/>.
    /// </summary>
    public ushort ScanWindow(int sizeValue) => _scanWindows[sizeValue];
}

/// <summary>Which byte of a building's fill a lost good counts against.</summary>
public enum FillCounter
{
    /// <summary>The low byte of the fill (the byte at 8).</summary>
    Low,

    /// <summary>The high byte of the fill (the byte at 9).</summary>
    High,

    /// <summary>Neither: the good counts against no building.</summary>
    None,
}

/// <summary>
/// What a job's handler adds to the figure byte to make the figure word: for a figure byte below 0x80 (<see cref="Walk"/>,
/// or <see cref="Carrying"/> on the way back with the log) and for one from 0x80 (<see cref="Cutting"/>).
/// </summary>
public readonly record struct FigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add for a figure byte below 0x80 on the way back with the log.</summary>
    public required ushort Carrying { get; init; }

    /// <summary>The add for a figure byte from 0x80: the cut.</summary>
    public required ushort Cutting { get; init; }
}

/// <summary>
/// What the sawmiller's handler adds to the figure byte: <see cref="Walk"/> for a byte below 0x80, <see cref="Carrying"/> in the
/// step that takes the plank out (state 5 with state 0xd next), and <see cref="Saw"/> for a byte from 0x80, the saw's frames.
/// </summary>
public readonly record struct SawmillerFigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add in the step that takes the plank out (state 5 with state 0xd next).</summary>
    public required ushort Carrying { get; init; }

    /// <summary>The add for a figure byte from 0x80: the saw's frames.</summary>
    public required ushort Saw { get; init; }
}

/// <summary>
/// What the figure handler of an indoor food job adds to the figure byte: <see cref="Walk"/> for a byte below 0x80, <see cref="Carrying"/> in
/// the step that takes the good out (state 5 with state 0xd next), and <see cref="Work"/> for a byte from 0x80, the building's frames. The
/// handlers of the miller (<c>0x4400d3</c>, flour), the pig farmer (<c>0x43ff56</c>, pig), the butcher (<c>0x43ff90</c>, meat), and the baker
/// (<c>0x440112</c>, bread) have this one shape, each with its own immediates.
/// </summary>
public readonly record struct FoodHutFigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add in the step that takes the good out (state 5 with state 0xd next).</summary>
    public required ushort Carrying { get; init; }

    /// <summary>The add for a figure byte from 0x80: the work's frames.</summary>
    public required ushort Work { get; init; }
}

/// <summary>
/// The program's numbers of the pig farm (<c>docs/rules/pig-farming.md</c>): the rank its request for grain starts from and the odds with which the pigs
/// it holds grow.
/// </summary>
public sealed record PigFarmRates
{
    /// <summary>
    /// The rank the pig farm's request for grain starts from: the high byte of the player word at <c>+0x158</c>, which the player setup
    /// of a new game writes (<c>0x44579c</c>). The handler (<c>0x42e0fa</c>) shifts it right by the goods the pig farm holds.
    /// </summary>
    public required byte RankBase { get; init; }

    /// <summary>
    /// The table at <c>0x433684</c> of eight words: for a pig farm that holds <c>n</c> pigs (0 to 7), at the index <c>n</c>, the draw
    /// below which another pig grows in each pause of the work (the draw is the generator's 16-bit word).
    /// </summary>
    public required ImmutableArray<ushort> Growth { get; init; }
}

/// <summary>
/// What the fisher's figure handler (<c>0x43fed8</c>) adds to the figure byte: for a byte below 0x80 <see cref="Walk"/>
/// (or <see cref="Carrying"/> on the way back with a fish), for one from 0x80 <see cref="High"/>
/// (<see cref="HighFacing"/> once he has caught a fish, byte 14 is 1).
/// </summary>
public readonly record struct FisherFigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add for a figure byte below 0x80 on the way back with a load.</summary>
    public required ushort Carrying { get; init; }

    /// <summary>The add for a figure byte from 0x80.</summary>
    public required ushort High { get; init; }

    /// <summary>The add for a figure byte from 0x80 where the settler's heading, byte 14, is 1.</summary>
    public required ushort HighFacing { get; init; }
}

/// <summary>
/// What the builder's figure handler (<c>0x43faf4</c>) adds to the figure byte: <see cref="Walk"/> for a byte below 0x80,
/// <see cref="Work"/> for one from 0x80 (the blows).
/// </summary>
public readonly record struct BuilderFigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add for a figure byte from 0x80: the blows.</summary>
    public required ushort Work { get; init; }
}

/// <summary>
/// What the farmer's figure handler (<c>0x44002b</c>) adds to the figure byte: <see cref="Walk"/> for a byte below 0x80,
/// <see cref="Carrying"/> on the way back with the grain (byte 13 0x80, byte 14 1, state 0x10), <see cref="Sow"/> for a byte from
/// 0x80 while he sows (byte 13 0), and <see cref="Harvest"/> while he harvests.
/// </summary>
public readonly record struct FarmerFigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add for a figure byte below 0x80 on the way back with the grain.</summary>
    public required ushort Carrying { get; init; }

    /// <summary>The add for a figure byte from 0x80 while he sows.</summary>
    public required ushort Sow { get; init; }

    /// <summary>The add for a figure byte from 0x80 while he harvests.</summary>
    public required ushort Harvest { get; init; }
}

/// <summary>
/// What the forester's figure handler (<c>0x43fd88</c>) adds to the figure byte: <see cref="Walk"/> for a byte below 0x80
/// (the walk, and the waiting and planting frames), <see cref="Work"/> for one from 0x80.
/// </summary>
public readonly record struct ForesterFigureAdds
{
    /// <summary>The add for a figure byte below 0x80: the walk, and the waiting and planting frames.</summary>
    public required ushort Walk { get; init; }

    /// <summary>The add for a figure byte from 0x80.</summary>
    public required ushort Work { get; init; }
}
