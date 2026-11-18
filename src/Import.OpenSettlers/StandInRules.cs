using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace OpenSettlers.Import.ContentSet;

/// <summary>The stand-in rules of one good: how it is counted, ranked, requested, and stocked.</summary>
sealed record StandInGood
{
    /// <summary>The good.</summary>
    public required Good Good { get; init; }

    /// <summary>Which byte of a building's fill the good counts in.</summary>
    public required FillCounter Counts { get; init; }

    /// <summary>Which byte of a building's fill the carrier's handover of the good counts in.</summary>
    public required FillCounter Handover { get; init; }

    /// <summary>Whether a lost good of this kind counts against the total of resource 1.</summary>
    public required bool CountsAgainstResource1 { get; init; }

    /// <summary>The transport priority, a rank from 1 up that no other good shares.</summary>
    public required int Priority { get; init; }

    /// <summary>The request the good looks for, or null for a good that goes to storage at once.</summary>
    public required GoodClass? Request { get; init; }

    /// <summary>The castle's count of the good at the start stock levels 0, 10, 20, 30, and 40.</summary>
    public required ImmutableArray<byte> StartStock { get; init; }
}

/// <summary>The stand-in rules of one building type.</summary>
sealed record StandInBuilding
{
    /// <summary>The room the building takes.</summary>
    public required BuildingSize Size { get; init; }

    /// <summary>The planks and stones a site needs.</summary>
    public required BuildingNeeds Needs { get; init; }

    /// <summary>The door speed of the finished building, 0 to 31.</summary>
    public required byte StandingDoor { get; init; }

    /// <summary>The door speed of the site, 0 to 31.</summary>
    public required byte ConstructingDoor { get; init; }

    /// <summary>The materials the builder takes in turn: true for a stone, false for a plank.</summary>
    public required ImmutableArray<bool> Stones { get; init; }

    /// <summary>The builder's blows in the first half of the work.</summary>
    public required int FirstHalfBlows { get; init; }

    /// <summary>The builder's blows in the second half of the work.</summary>
    public required int SecondHalfBlows { get; init; }

    /// <summary>The worker the finished building asks for, or null for none.</summary>
    public required WorkerRequest? Worker { get; init; }

    /// <summary>Whether the builder of the site takes a second figure.</summary>
    public required bool SecondFigure { get; init; }
}

/// <summary>
/// The stand-in rules of the basic content set (ADR 0018, <c>CONTEXT.md</c>): the parsed, readable form of the rule files, in
/// directions, sizes, materials, and speeds. <see cref="ToRuleTables"/> encodes it into the program's encodings that
/// <see cref="RuleTables"/> holds, so those encodings stay in this method (ADR 0013).
/// </summary>
sealed record StandInRules
{
    /// <summary>The number of rises a step time is given for: -4 to 4.</summary>
    public const int Rises = 9;

    /// <summary>The class of each object code.</summary>
    public required ImmutableArray<ObjectClass> ObjectClasses { get; init; }

    /// <summary>The time of a step by its rise, from -4 to 4.</summary>
    public required ImmutableArray<ushort> StepTimeByRise { get; init; }

    /// <summary>The scan window by map size value.</summary>
    public required IReadOnlyDictionary<int, ushort> ScanWindows { get; init; }

    /// <summary>The row offset of each frame of the frame table.</summary>
    public required ImmutableArray<sbyte> FrameRows { get; init; }

    /// <summary>The carriers each length class of a road holds.</summary>
    public required ImmutableArray<byte> RoadCapacities { get; init; }

    /// <summary>The directions of each walk row, best first.</summary>
    public required ImmutableArray<ImmutableArray<Direction>> WalkRows { get; init; }

    /// <summary>The 26 goods in the order of their numbers.</summary>
    public required ImmutableArray<StandInGood> Goods { get; init; }

    /// <summary>The three distribution orders.</summary>
    public required ImmutableArray<ImmutableArray<DistributionStep>> Distribution { get; init; }

    /// <summary>The rank a site's plank request starts from.</summary>
    public required byte PlankRank { get; init; }

    /// <summary>The rank the mill's grain request starts from.</summary>
    public required byte MillRank { get; init; }

    /// <summary>The rules of each building type the set names.</summary>
    public required IReadOnlyDictionary<BuildingType, StandInBuilding> Buildings { get; init; }

    /// <summary>The door speeds of the castle: standing, then constructing.</summary>
    public required (byte Standing, byte Constructing) CastleDoor { get; init; }

    /// <summary>The tool a builder is made with.</summary>
    public required Good BuilderTool { get; init; }

    /// <summary>The tool a digger is made with.</summary>
    public required Good DiggerTool { get; init; }

    /// <summary>The rank the pig farm's grain request starts from.</summary>
    public required byte PigRank { get; init; }

    /// <summary>The chance in percent that a pig grows, by the pigs the farm holds, 0 to 7.</summary>
    public required ImmutableArray<int> PigGrowthPercent { get; init; }

    const int Whole = 0x8000;

    /// <summary>
    /// Encodes the rules into <see cref="RuleTables"/>. Throws <see cref="InvalidDataException"/> where the tables refuse them.
    /// </summary>
    public RuleTables ToRuleTables()
    {
        var steps = new ushort[RuleTables.StepTimeCount];
        for (var at = 0; at < steps.Length; at++)
        {
            // The shapes repeat with the period 9: 4 + 9 * direction + rise.
            steps[at] = StepTimeByRise[((at + RuleTables.StepTimeFirst) % Rises + Rises) % Rises];
        }

        // The castle states only its door and that it is a storage: its site is built from no needs and it asks for no worker.
        var castle = new BuildingEntry
        {
            Name = BuildingType.Castle.Name,
            Size = BuildingSize.Mine,
            Needs = new BuildingNeeds { Planks = 0, Stones = 0 },
            StandingDoor = CastleDoor.Standing,
            ConstructingDoor = CastleDoor.Constructing,
            EarlyBlowStep = 0,
            LateBlowStep = 0,
            SecondFigure = false,
            IsStorage = true,
        };
        var buildings = Buildings
            .Select(entry => new BuildingEntry
            {
                Name = entry.Key.Name,
                Size = entry.Value.Size,
                Needs = entry.Value.Needs,
                StandingDoor = entry.Value.StandingDoor,
                ConstructingDoor = entry.Value.ConstructingDoor,
                Worker = entry.Value.Worker,
                Stones = entry.Value.Stones,
                // A blow adds the share of the half it is one of, rounded up so that the blows of a half reach it.
                EarlyBlowStep = (ushort)((Whole + entry.Value.FirstHalfBlows - 1) / entry.Value.FirstHalfBlows),
                LateBlowStep = (ushort)((Whole + entry.Value.SecondHalfBlows - 1) / entry.Value.SecondHalfBlows),
                SecondFigure = entry.Value.SecondFigure,
                // The castle is the set's one storage: the basic set draws no storehouse.
                IsStorage = false,
            })
            .Append(castle);

        var windows = new ushort[RuleTables.ScanWindowCount];
        foreach (var (size, window) in ScanWindows)
        {
            windows[size] = window;
        }

        var walk = new byte[RuleTables.WalkRowCount * RuleTables.WalkRowSize];
        for (var row = 0; row < WalkRows.Length; row++)
        {
            for (var candidate = 0; candidate < WalkRows[row].Length; candidate++)
            {
                walk[row * RuleTables.WalkRowSize + candidate] = (byte)(4 * (WalkRows[row][candidate].Value + 1));
            }
        }

        try
        {
            return new RuleTables(
                [.. ObjectClasses],
                [
                    .. Enumerable
                        .Range(0, RuleTables.StartStockRows)
                        .Select(row =>
                            (IReadOnlyDictionary<Good, byte>)
                                Goods.ToDictionary(good => good.Good, good => good.StartStock[row])
                        ),
                ],
                steps,
                windows,
                walkRows: walk,
                roadCapacities: [.. RoadCapacities],
                frameRows: [.. FrameRows],
                goods: new ContentCatalog(
                    Goods.Select(good => new GoodEntry
                    {
                        Name = good.Good.Name,
                        Request = good.Request,
                        Counter = good.Counts,
                        HandoverCounter = good.Handover,
                        CountsAgainstResource1 = good.CountsAgainstResource1,
                        TransportRank = good.Priority,
                    }),
                    buildings
                ),
                distribution: [.. Distribution.Select(order => order.ToArray())],
                plankRankBase: PlankRank,
                millRankBase: MillRank,
                jobData:
                [
                    (new Builder(), new WorkerRequest { Job = new Builder(), Tool = BuilderTool }),
                    (new Digger(), new WorkerRequest { Job = new Digger(), Tool = DiggerTool }),
                    (
                        new PigFarmer(),
                        new PigFarmRates
                        {
                            RankBase = PigRank,
                            // The odds are a share of the generator's 16-bit draw.
                            Growth =
                            [
                                .. PigGrowthPercent.Select(percent =>
                                    (ushort)Math.Min(ushort.MaxValue, percent * 65536 / 100)
                                ),
                            ],
                        }
                    ),
                ]
            );
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"the stand-in rules do not make rule tables: {e.Message}", e);
        }
    }
}
