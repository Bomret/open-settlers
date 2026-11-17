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

    /// <summary>The transport priority, a rank from 1 to 26 that no other good shares.</summary>
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
    const int NoneKind = 2;

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

        var doors = new byte[RuleTables.DoorSpeedCount];
        doors[BuildingType.Castle.Code] = CastleDoor.Standing;
        doors[BuildingType.Castle.Code | 0x20] = CastleDoor.Constructing;
        var sizes = Enumerable.Repeat((byte)BuildingSize.Small, RuleTables.BuildingTypeCount).ToArray();
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        var workers = new WorkerRequest?[RuleTables.BuildingTypeCount];
        var materials = new ushort[RuleTables.BuildingTypeCount];
        var blows = new ushort[2 * RuleTables.BuildingTypeCount];
        foreach (var (type, building) in Buildings)
        {
            doors[type.Code] = building.StandingDoor;
            doors[type.Code | 0x20] = building.ConstructingDoor;
            if (type.Code >= RuleTables.BuildingTypeCount)
            {
                continue;
            }

            sizes[type.Code] = (byte)building.Size;
            needs[2 * type.Code] = (byte)building.Needs.Planks;
            needs[2 * type.Code + 1] = (byte)building.Needs.Stones;
            workers[type.Code] = building.Worker;
            for (var turn = 0; turn < building.Stones.Length; turn++)
            {
                materials[type.Code] |= (ushort)(building.Stones[turn] ? 1 << turn : 0);
            }

            // A blow adds the share of the half it is one of, rounded up so that the blows of a half reach it.
            blows[2 * type.Code] = (ushort)((Whole + building.FirstHalfBlows - 1) / building.FirstHalfBlows);
            blows[2 * type.Code + 1] = (ushort)((Whole + building.SecondHalfBlows - 1) / building.SecondHalfBlows);
        }

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

        var kinds = Enumerable.Repeat((byte)NoneKind, RuleTables.GoodKindCount).ToArray();
        var priorities = new byte[RuleTables.Goods];
        var classes = new GoodClass?[RuleTables.GoodClassCount];
        foreach (var good in Goods)
        {
            kinds[good.Good.Code] = good.Counts switch
            {
                FillCounter.Low => 0,
                FillCounter.High => 1,
                _ => NoneKind,
            };
            priorities[good.Good.Code - 1] = (byte)good.Priority;
            classes[good.Good.Code] = good.Request;
        }

        try
        {
            return new RuleTables(
                [.. ObjectClasses],
                [
                    .. Enumerable
                        .Range(0, RuleTables.StartStockRows)
                        .Select(row => Goods.Select(good => good.StartStock[row]).ToArray()),
                ],
                steps,
                doors,
                windows,
                walkRows: walk,
                goodKinds: kinds,
                roadCapacities: [.. RoadCapacities],
                transportPriorities: priorities,
                frameRows: [.. FrameRows],
                buildingSizes: sizes,
                buildingNeeds: needs,
                goodClasses: classes,
                distribution: [.. Distribution.Select(order => order.ToArray())],
                plankRankBase: PlankRank,
                millRankBase: MillRank,
                workers: workers,
                materialOrder: materials,
                blowSteps: blows,
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
