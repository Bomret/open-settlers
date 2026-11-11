using System;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// The tables of the original's program data that the rules read, as typed values: the object classes, the start stock,
/// the step times, and the others below. They come from the install's <c>Siedler1.exe</c> and are never restated in
/// source (INV-0001): <c>ProgramTablesImporter</c> in the adapters builds them from the program's bytes, and a test
/// builds its own from made-up values. The rules read the typed accessors (<see cref="ClassOf"/>, <see cref="StepTime"/>,
/// <see cref="DoorSpeed"/>, and the others); the arrays that remain public are the raw tables the importer's tests check against the original and the figure tables
/// the map view draws from.
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

    /// <summary>The number of goods of the class table at <c>0x464117</c> (good numbers are 5 bits).</summary>
    public const int GoodClassCount = 32;

    /// <summary>The number of rows of the figure bases (the figure word's high byte, 0 to 0x88).</summary>
    public const int FigureBaseRowCount = 137;

    /// <summary>The job number of the settler of the type 1 hut.</summary>
    public const int TypeOneHutJob = 11;

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

    /// <summary>The number of goods the good kind table covers (good numbers are 5 bits).</summary>
    public const int GoodKindCount = 32;

    /// <summary>The number of length classes of a road.</summary>
    public const int RoadCapacityCount = 8;

    /// <summary>The number of rows of the free walk's direction choice.</summary>
    public const int WalkRowCount = 24;

    /// <summary>The number of candidates of a row of the free walk's direction choice, and its width in the program.</summary>
    public const int WalkRowSize = 8;

    private readonly ObjectClass[] _classes;
    private readonly byte[] _goodKinds;
    private readonly byte[] _roadCapacities;
    private readonly byte[] _walkRows;
    private readonly byte[] _transportPriorities;
    private readonly byte[] _doorSpeeds;
    private readonly ushort[] _scanWindows;
    private readonly byte[] _buildingSizes;
    private readonly byte[] _buildingNeeds;
    private readonly GoodClass?[] _goodClasses;
    private readonly DistributionStep[][] _distribution;
    private readonly WorkerRequest?[] _workers;
    private readonly ushort[] _materialOrder;
    private readonly ushort[] _blowSteps;

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
        byte[]? goodKinds = null,
        byte[]? roadCapacities = null,
        byte[]? transportPriorities = null,
        FigureAdds? woodcutterFigureAdds = null,
        sbyte[]? frameRows = null,
        HutFigureAdds? typeOneHutFigureAdds = null,
        short[]? idlePhases = null,
        short[]? idleFigures = null,
        sbyte[]? idleOffsets = null,
        byte[]? buildingSizes = null,
        byte[]? buildingNeeds = null,
        GoodClass?[]? goodClasses = null,
        DistributionStep[][]? distribution = null,
        byte plankRankBase = 0,
        WorkerRequest?[]? workers = null,
        WorkerRequest? builderRequest = null,
        ushort[]? materialOrder = null,
        ushort[]? blowSteps = null,
        BuilderFigureAdds? builderFigureAdds = null
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

        _classes = classes;
        StartStock = startStock;
        StepTimes = stepTimes ?? new ushort[StepTimeCount];
        _doorSpeeds = doorSpeeds ?? new byte[DoorSpeedCount];
        _scanWindows = scanWindows ?? new ushort[ScanWindowCount];
        FigureBases = figureBases ?? new short[2 * FigureBaseRowCount];
        FigureOffsets = figureOffsets ?? new short[512];
        _walkRows = walkRows ?? new byte[WalkRowCount * WalkRowSize];
        CarriedFigures = carriedFigures ?? new short[CarriedFigureCount];
        _goodKinds = goodKinds ?? new byte[GoodKindCount];
        _roadCapacities = roadCapacities ?? new byte[RoadCapacityCount];
        _transportPriorities = transportPriorities ?? new byte[Goods];
        WoodcutterFigureAdds = woodcutterFigureAdds ?? default;
        FrameRows = frameRows ?? new sbyte[FrameRowCount];
        TypeOneHutFigureAdds = typeOneHutFigureAdds ?? default;
        IdlePhases = idlePhases ?? new short[IdlePhaseCount];
        IdleFigures = idleFigures ?? new short[IdleFigureCount];
        IdleOffsets = idleOffsets ?? new sbyte[IdleOffsetCount];
        // stand-in: a table a test does not give makes every type small, so a test that places a building needs no table.
        _buildingSizes = buildingSizes ?? [.. Enumerable.Repeat((byte)1, BuildingTypeCount)];
        _buildingNeeds = buildingNeeds ?? new byte[2 * BuildingTypeCount];
        // stand-in: a table a test does not give sends every good to storage at once, and no storage hands any good out.
        _goodClasses = goodClasses ?? new GoodClass?[GoodClassCount];
        _distribution =
            distribution
            ??
            [
                [],
                [],
                [],
            ];
        PlankRankBase = plankRankBase;
        // stand-in: a table a test does not give makes only the woodcutter's hut ask for a worker, of the woodcutter's job and no tool,
        // and the builder a settler of the builder's job with no tool.
        _workers = workers ?? WoodcutterOnly();
        BuilderRequest = builderRequest ?? new WorkerRequest(Job.Builder, Good.None);
        // stand-in: a table a test does not give makes every material a plank and every blow add nothing.
        _materialOrder = materialOrder ?? new ushort[BuildingTypeCount];
        _blowSteps = blowSteps ?? new ushort[2 * BuildingTypeCount];
        BuilderFigureAdds = builderFigureAdds ?? default;
    }

    private static WorkerRequest?[] WoodcutterOnly()
    {
        var workers = new WorkerRequest?[BuildingTypeCount];
        workers[BuildingType.Woodcutter.Code] = new WorkerRequest(Job.Woodcutter, Good.None);

        return workers;
    }

    /// <summary>The class of each object code.</summary>
    public ObjectClass[] Classes => _classes;

    /// <summary>The class of the object <paramref name="mapObject"/>.</summary>
    public ObjectClass ClassOf(MapObject mapObject) => _classes[mapObject.Code];

    /// <summary>The start stock rows, each <see cref="Goods"/> counts.</summary>
    public byte[][] StartStock { get; }

    /// <summary>The waiting carriers' phases.</summary>
    public short[] IdlePhases { get; }

    /// <summary>The waiting carriers' figure words.</summary>
    public short[] IdleFigures { get; }

    /// <summary>The waiting carriers' offsets by road directions, x and y for each.</summary>
    public sbyte[] IdleOffsets { get; }

    /// <summary>The default transport priorities of the goods 1 to 26, as the importer read them (index good - 1).</summary>
    public byte[] TransportPriorities => _transportPriorities;

    /// <summary>
    /// The default transport priority of the good <paramref name="good"/>, 1 to 26: its rank, 1 to 26, each once; the
    /// carrier's exchange lines up the good of the highest rank next (<c>docs/rules/goods.md</c>). 0 for a number that is no
    /// good, and for every good where a test needs none.
    /// </summary>
    public int TransportPriority(Good good) => good.Code is >= 1 and <= Goods ? _transportPriorities[good.Code - 1] : 0;

    /// <summary>The byte counts of the good kind table as the importer read them.</summary>
    public byte[] GoodKinds => _goodKinds;

    /// <summary>
    /// Which byte of a building's fill a lost good counts against (<c>docs/rules/removal.md</c>). <paramref name="good"/> is a
    /// good, its number taken to 0 to 31 (the original masks it with 0x1f).
    /// </summary>
    public FillCounter FillCounterOf(Good good) =>
        _goodKinds[good.Code & 0x1f] switch
        {
            0 => FillCounter.Low,
            1 => FillCounter.High,
            _ => FillCounter.None,
        };

    /// <summary>The number of carriers each length class of a road holds at most, as the importer read them.</summary>
    public byte[] RoadCapacities => _roadCapacities;

    /// <summary>The number of carriers a road of the length class <paramref name="lengthClass"/> holds at most.</summary>
    public byte RoadCapacity(int lengthClass) => _roadCapacities[lengthClass];

    /// <summary>The rows of the free walk's direction choice as the importer read them.</summary>
    public byte[] WalkRows => _walkRows;

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

    /// <summary>For each good a word that a carrier carrying it adds to its figure byte: the row in the high byte.</summary>
    public short[] CarriedFigures { get; }

    /// <summary>For each high byte of the figure word 2 words: the figure base and the second figure base (negative: none).</summary>
    public short[] FigureBases { get; }

    /// <summary>For each low byte of the figure word 2 words: what it adds to the figure and to the second figure.</summary>
    public short[] FigureOffsets { get; }

    /// <summary>What the woodcutter's handler adds to the figure byte to make the figure word.</summary>
    public FigureAdds WoodcutterFigureAdds { get; }

    /// <summary>What the handler of the settler of the type 1 hut adds to the figure byte to make the figure word.</summary>
    public HutFigureAdds TypeOneHutFigureAdds { get; }

    /// <summary>For each frame of a settler what the record builder adds to the row of its record's list.</summary>
    public sbyte[] FrameRows { get; }

    /// <summary>
    /// The time in clock units of a step, by step shape: <c>4 + 9 * direction + (height of the node reached minus
    /// height of the node left)</c>, with the directions east 0 to north 5 (<c>docs/rules/settlers.md</c>). Index with
    /// <see cref="StepTime"/>.
    /// </summary>
    public ushort[] StepTimes { get; }

    /// <summary>The time of the step shape <paramref name="shape"/>; a shape outside the table reads as 0.</summary>
    public ushort StepTime(int shape)
    {
        var index = shape - StepTimeFirst;

        return index >= 0 && index < StepTimes.Length ? StepTimes[index] : (ushort)0;
    }

    /// <summary>The speed table of the doors as the importer read it.</summary>
    public byte[] DoorSpeeds => _doorSpeeds;

    /// <summary>The speed of the door of the building type <paramref name="buildingType"/>, in 32nds of a step time.</summary>
    public byte DoorSpeed(int buildingType) => _doorSpeeds[buildingType];

    /// <summary>
    /// The speed of the door of <paramref name="building"/> (the table is indexed by its type plus 0x20 while it is built), or
    /// of no building, the first entry, where there is none.
    /// </summary>
    public byte DoorSpeed(Building? building) =>
        _doorSpeeds[building is null ? 0 : building.Type.Code | (building.Constructing ? 0x20 : 0)];

    /// <summary>The size table as the importer read it: one byte for each building type, 0 a mine, 1 small, 2 large.</summary>
    public byte[] BuildingSizes => _buildingSizes;

    /// <summary>
    /// How much room the building type <paramref name="type"/> takes (<c>0x42d307</c>, <c>docs/rules/build-sites.md</c>). A type
    /// outside the table, the castle included, reads as a mine, which no placement builds.
    /// </summary>
    public BuildingSize SizeOf(BuildingType type) =>
        type.Code is >= 0 and < BuildingTypeCount && _buildingSizes[type.Code] is 1 or 2
            ? (BuildingSize)_buildingSizes[type.Code]
            : BuildingSize.Mine;

    /// <summary>The needs table as the importer read it: planks then stones for each building type.</summary>
    public byte[] BuildingNeedsTable => _buildingNeeds;

    /// <summary>
    /// The planks and stones the building type <paramref name="type"/> needs (<c>0x449f18</c>); none for a type outside the table.
    /// </summary>
    public BuildingNeeds NeedsOf(BuildingType type) =>
        type.Code is >= 0 and < BuildingTypeCount
            ? new BuildingNeeds(_buildingNeeds[2 * type.Code], _buildingNeeds[2 * type.Code + 1])
            : new BuildingNeeds(0, 0);

    /// <summary>
    /// The request a good with no destination looks for (<c>0x464117</c>): its class and the request word of the flags that ask for
    /// it; null for a good that goes to storage at once. The importer turns the program's table into these values.
    /// </summary>
    public GoodClass? ClassOfGood(Good good) => _goodClasses[good.Code & 0x1f];

    /// <summary>
    /// The order a storage serves the requests in, by the draw the phase routine takes at each cycle (<c>0x42965c</c>): the draw taken
    /// to 0 to 7 picks the second table at 0, the third at 1, and the first at any other value.
    /// </summary>
    public DistributionStep[] DistributionOrder(int draw) =>
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

    /// <summary>What the builder's figure handler adds to the figure byte (<c>0x43faf4</c>); all 0 draws him in the carrier's row.</summary>
    public BuilderFigureAdds BuilderFigureAdds { get; }

    /// <summary>
    /// What a building site asks for as its builder (<c>0x42d5fc</c>): the builder's job and the tool good a jobless settler takes from the
    /// stock to become one.
    /// </summary>
    public WorkerRequest BuilderRequest { get; }

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
    public ushort[] ScanWindows => _scanWindows;

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
/// What a job's handler adds to the figure byte to make the figure word: for a figure byte below 0x80 (<paramref name="Walk"/>,
/// or <paramref name="Carrying"/> on the way back with the log) and for one from 0x80 (<paramref name="Cutting"/>).
/// </summary>
public readonly record struct FigureAdds(ushort Walk, ushort Carrying, ushort Cutting);

/// <summary>
/// What the handler of the settler of the type 1 hut adds to the figure byte: for a byte below 0x80 <paramref name="Walk"/>
/// (or <paramref name="Carrying"/> on the way back with a load), for one from 0x80 <paramref name="High"/>
/// (<paramref name="HighFacing"/> where the settler's heading, byte 14, is 1).
/// </summary>
public readonly record struct HutFigureAdds(ushort Walk, ushort Carrying, ushort High, ushort HighFacing);

/// <summary>
/// What the builder's figure handler (<c>0x43faf4</c>) adds to the figure byte: <paramref name="Walk"/> for a byte below 0x80,
/// <paramref name="Work"/> for one from 0x80 (the blows).
/// </summary>
public readonly record struct BuilderFigureAdds(ushort Walk, ushort Work);
