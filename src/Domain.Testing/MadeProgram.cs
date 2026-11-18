using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Domain.Testing;

/// <summary>
/// Rule tables a test builds in code, for the tests that check the game's own logic and hold no original value. The
/// tables of the install's <c>Siedler1.exe</c> (INV-0001) are <c>TestProgram.Tables</c>.
/// </summary>
public static class MadeProgram
{
    /// <summary>The number of jobs: the job of each number 0 to 31.</summary>
    const int JobCount = 32;

    /// <summary>The direction each heading class (0x437072) takes first: a step that cuts the vector the class describes.</summary>
    static readonly byte[] FirstDirections = [20, 20, 20, 20, 16, 12, 12, 8, 8, 4, 4, 24];

    /// <summary>
    /// Tables of made-up values, no restatement of the original's: every object code has class 0, so no object
    /// blocks a site, and the start stock rows count up from the row number. With every class 0 no object is large, so
    /// a map generated with these tables keeps every object and is not the original's map; tests that compare with the
    /// original's data use the install's tables. The catalog holds the 26 goods of <see cref="TestCatalog"/>, each ranked by its position.
    /// </summary>
    public static RuleTables Made() => Made(null);

    /// <summary>
    /// The tables of <see cref="Made()"/> with made-up transport priorities (index good - 1; a good of rank 0 has no entry), or the position as the rank
    /// for none.
    /// </summary>
    public static RuleTables Made(byte[]? transportPriorities) =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            MadeStartStock(),
            catalog: transportPriorities is null
                ? TestCatalog.StandIn(TestCatalog.Buildings(workers: MadeWorkers()))
                : TestCatalog.Of(ranks: transportPriorities, buildings: TestCatalog.Buildings(workers: MadeWorkers())),
            jobData: MadeJobData()
        );

    /// <summary>
    /// Made-up tables for the woodcutter's own logic: every step takes 100 clock units, the doors have one speed, every object
    /// class is 0 but the flag's, and the walk's direction rows hold, as their first direction, a step that takes a vector
    /// nearer to 0. The workers are those of <see cref="MadeWorkers"/> unless <paramref name="workers"/> gives others.
    /// </summary>
    public static RuleTables WoodcuttingTables(WorkerRequest?[]? workers = null)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var rows = new byte[RuleTables.WalkRowCount * RuleTables.WalkRowSize];
        for (var row = 0; row < RuleTables.WalkRowCount; row++)
        {
            // The six directions, the heading class's own first and the others after it.
            var first = row < 12 ? (byte)4 : FirstDirections[row - 12];
            var order = new[] { first }.Concat(new byte[] { 4, 8, 12, 16, 20, 24 }.Where(code => code != first));
            order.ToArray().CopyTo(rows, row * RuleTables.WalkRowSize);
        }

        return new RuleTables(
            classes,
            MadeStartStock(),
            [.. Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount)],
            [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            walkRows: rows,
            catalog: TestCatalog.WithDoors(workers ?? MadeWorkers()),
            noBuildingDoor: 8
        );
    }

    /// <summary>
    /// Tables for the tests of removal: the object class of the flag 3 and that of the building 4, the log the only good with a
    /// kind, every step 100 clock units, and every road holding <paramref name="capacity"/> goods.
    /// </summary>
    public static RuleTables RemovalTables(byte capacity = 8, byte[]? transportPriorities = null)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;
        var kinds = Enumerable.Repeat((byte)0xff, TestCatalog.Slots).ToArray();
        kinds[TestCatalog.CodeOf(Good.Log)] = 1;

        return new RuleTables(
            classes,
            MadeStartStock(),
            [.. Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount)],
            [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            roadCapacities: [.. Enumerable.Repeat(capacity, RuleTables.RoadCapacityCount)],
            catalog: TestCatalog.Of(transportPriorities, kinds: kinds, buildings: TestCatalog.Buildings(door: 8)),
            noBuildingDoor: 8
        );
    }

    /// <summary>
    /// Tables for the tests of the history:the start stock rows of <see cref="Made()"/>, the object class of the flag (code 1)
    /// 3 and that of the building (code 2) 4, every step 100 clock units, and the workers of <see cref="MadeWorkers"/>.
    /// </summary>
    public static RuleTables HistoryTables()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new RuleTables(
            classes,
            MadeStartStock(),
            [.. Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount)],
            [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            catalog: TestCatalog.WithDoors(MadeWorkers()),
            noBuildingDoor: 8
        );
    }

    /// <summary>
    /// Tables of the start stock rows of <see cref="Made()"/> and nothing else but the object class of the flag (code 1),
    /// which is 3 as a road's end needs: every other object class is 0.
    /// </summary>
    public static RuleTables FlagClassTables()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;

        return new RuleTables(classes, MadeStartStock(), catalog: TestCatalog.StandIn());
    }

    /// <summary>The start stock rows of <see cref="Made()"/>: each count is the row number times 10 plus the good's index.</summary>
    public static IReadOnlyDictionary<Good, byte>[] MadeStartStock() =>
        [
            .. Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row =>
                    (IReadOnlyDictionary<Good, byte>)
                        TestCatalog
                            .Names.Select((_, index) => (Good: TestCatalog.GoodOf(index + 1), Index: index))
                            .ToDictionary(entry => entry.Good, entry => (byte)(row * 10 + entry.Index))
                ),
        ];

    /// <summary>The job of each number 0 to 31.</summary>
    public static IEnumerable<Job> Jobs() => Enumerable.Range(0, JobCount).Select(Job.Of);

    /// <summary>
    /// Made-up data for every record each job type declares (<see cref="Job.Data"/>): the builder and the digger ask for a settler of
    /// their own job with no tool, and every other record is all zeros, which draws the settler in the carrier's row. A test that gives
    /// data of its own builds on this list, whose later entries for one job and type win.
    /// </summary>
    public static IReadOnlyList<(Job Job, object Data)> MadeJobData() =>
        [
            .. Jobs()
                .SelectMany(job =>
                    job.Data.Select(type =>
                        (
                            job,
                            type == typeof(WorkerRequest)
                                ? new WorkerRequest { Job = job, Tool = Good.None }
                                : Activator.CreateInstance(type)!
                        )
                    )
                ),
        ];

    /// <summary>
    /// The workers made-up tables ask for: the woodcutter's hut a woodcutter with a made-up tool, the forester's building a forester
    /// with no tool, with <paramref name="stonecutter"/> the stonecutter's building a stonecutter with no tool, with
    /// <paramref name="mill"/> the mill a miller with no tool, with <paramref name="fisher"/> the fisher's building a fisher with no tool, with
    /// <paramref name="baker"/> the bakery a baker with no tool, and with <paramref name="pigFarm"/> the pig farm a pig farmer with no tool.
    /// </summary>
    public static WorkerRequest?[] MadeWorkers(
        bool stonecutter = false,
        bool mill = false,
        bool fisher = false,
        bool baker = false,
        bool pigFarm = false
    )
    {
        var workers = new WorkerRequest?[TestCatalog.BuildingSlots];
        workers[TestCatalog.CodeOf(BuildingType.Woodcutter)] = new WorkerRequest
        {
            Job = new Woodcutter(),
            Tool = TestCatalog.GoodOf(1),
        };
        workers[TestCatalog.CodeOf(BuildingType.Forester)] = new WorkerRequest
        {
            Job = new Forester(),
            Tool = Good.None,
        };
        if (stonecutter)
        {
            workers[TestCatalog.CodeOf(BuildingType.Stonecutter)] = new WorkerRequest
            {
                Job = new Stonecutter(),
                Tool = Good.None,
            };
        }

        if (mill)
        {
            workers[TestCatalog.CodeOf(BuildingType.Mill)] = new WorkerRequest { Job = new Miller(), Tool = Good.None };
        }

        if (fisher)
        {
            workers[TestCatalog.CodeOf(BuildingType.Fisher)] = new WorkerRequest
            {
                Job = new Fisher(),
                Tool = Good.None,
            };
        }

        if (baker)
        {
            workers[TestCatalog.CodeOf(BuildingType.Baker)] = new WorkerRequest { Job = new Baker(), Tool = Good.None };
        }

        if (pigFarm)
        {
            workers[TestCatalog.CodeOf(BuildingType.PigFarm)] = new WorkerRequest
            {
                Job = new PigFarmer(),
                Tool = Good.None,
            };
        }

        return workers;
    }

    /// <summary>
    /// Made-up tables as the hut and carrier tests have them: every step takes 100 clock units, the flag's object class is 3,
    /// and the walk's direction rows hold the heading class's own first direction first. The object class of the building's
    /// code (2) is 0 unless <paramref name="buildingClass"/> names one: the removal click (0x44b6e6) takes a building for class 4.
    /// </summary>
    public static RuleTables MadeTables(
        byte buildingClass = 0,
        byte[]? transportPriorities = null,
        byte[]? buildingSizes = null,
        byte[]? buildingNeeds = null,
        GoodClass?[]? goodClasses = null,
        DistributionStep[][]? distribution = null,
        byte plankRankBase = 0,
        byte millRankBase = 0,
        WorkerRequest?[]? workers = null,
        WorkerRequest? builderRequest = null,
        ushort[]? materialOrder = null,
        ushort[]? blowSteps = null,
        ushort stepTime = 100,
        WorkerRequest? diggerRequest = null,
        ushort[]? scanWindows = null,
        byte[]? goodKinds = null,
        IEnumerable<(Job Job, object Data)>? jobData = null,
        ContentCatalog? catalog = null,
        byte[]? roadCapacities = null
    )
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        classes[2] = buildingClass;
        var rows = new byte[RuleTables.WalkRowCount * RuleTables.WalkRowSize];
        for (var row = 0; row < RuleTables.WalkRowCount; row++)
        {
            var first = row < 12 ? (byte)4 : FirstDirections[row - 12];
            new[] { first }
                .Concat(new byte[] { 4, 8, 12, 16, 20, 24 }.Where(code => code != first))
                .ToArray()
                .CopyTo(rows, row * RuleTables.WalkRowSize);
        }

        return new RuleTables(
            classes,
            MadeStartStock(),
            [.. Enumerable.Repeat(stepTime, RuleTables.StepTimeCount)],
            scanWindows ?? [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            walkRows: rows,
            roadCapacities: roadCapacities,
            // The goods of the catalog a test gives, or the made-up ones, with building types of the made-up facts of the arguments.
            catalog: new ContentCatalog(
                (
                    catalog
                    ?? (
                        transportPriorities is null && goodClasses is null && goodKinds is null
                            ? TestCatalog.StandIn()
                            : TestCatalog.Of(transportPriorities, goodClasses, goodKinds ?? KindsOf(goodClasses))
                    )
                ).Goods,
                TestCatalog.Buildings(
                    8,
                    buildingSizes,
                    buildingNeeds,
                    workers ?? MadeWorkers(stonecutter: true, fisher: true),
                    materialOrder,
                    blowSteps
                ),
                TestCatalog.Jobs()
            ),
            noBuildingDoor: 8,
            distribution: distribution,
            plankRankBase: plankRankBase,
            millRankBase: millRankBase,
            // A request a test gives replaces the made-up one of the same job.
            jobData:
            [
                (new Builder(), builderRequest ?? new WorkerRequest { Job = new Builder(), Tool = Good.None }),
                (new Digger(), diggerRequest ?? new WorkerRequest { Job = new Digger(), Tool = Good.None }),
                .. jobData ?? [],
            ]
        );
    }

    // The made-up kind table of a made-up class table: a good counts in the byte of the word that asks for it (the first word's low
    // byte, the second's high), and a good that no word asks for counts in none, as the program's table does.
    static byte[]? KindsOf(GoodClass?[]? classes) =>
        classes is null
            ? null
            :
            [
                .. classes.Select(asked => asked is { } c ? (byte)(c.Word == RequestWord.First ? 0 : 1) : (byte)0xff),
                .. new byte[TestCatalog.Slots - classes.Length],
            ];
}
