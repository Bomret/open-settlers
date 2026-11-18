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
    /// original's data use the install's tables. The transport priorities are all 0.
    /// </summary>
    public static RuleTables Made() => Made(null);

    /// <summary>The tables of <see cref="Made()"/> with made-up transport priorities (index good - 1), or all 0 for none.</summary>
    public static RuleTables Made(byte[]? transportPriorities) =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            MadeStartStock(),
            transportPriorities: transportPriorities,
            workers: MadeWorkers(),
            jobData: MadeJobData()
        );

    /// <summary>The start stock rows of <see cref="Made()"/>: each count is the row number times 10 plus the good's index.</summary>
    public static byte[][] MadeStartStock() =>
        [
            .. Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, RuleTables.Goods).Select(good => (byte)(row * 10 + good)).ToArray()),
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
        var workers = new WorkerRequest?[RuleTables.BuildingTypeCount];
        workers[BuildingType.Woodcutter.Code] = new WorkerRequest
        {
            Job = new Woodcutter(),
            Tool = new Good { Code = 1 },
        };
        workers[BuildingType.Forester.Code] = new WorkerRequest { Job = new Forester(), Tool = Good.None };
        if (stonecutter)
        {
            workers[BuildingType.Stonecutter.Code] = new WorkerRequest { Job = new Stonecutter(), Tool = Good.None };
        }

        if (mill)
        {
            workers[BuildingType.Mill.Code] = new WorkerRequest { Job = new Miller(), Tool = Good.None };
        }

        if (fisher)
        {
            workers[BuildingType.Fisher.Code] = new WorkerRequest { Job = new Fisher(), Tool = Good.None };
        }

        if (baker)
        {
            workers[BuildingType.Baker.Code] = new WorkerRequest { Job = new Baker(), Tool = Good.None };
        }

        if (pigFarm)
        {
            workers[BuildingType.PigFarm.Code] = new WorkerRequest { Job = new PigFarmer(), Tool = Good.None };
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
        IEnumerable<(Job Job, object Data)>? jobData = null
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
            [.. Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount)],
            scanWindows ?? [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            walkRows: rows,
            transportPriorities: transportPriorities,
            buildingSizes: buildingSizes,
            buildingNeeds: buildingNeeds,
            goodKinds: goodKinds ?? KindsOf(goodClasses),
            goodClasses: goodClasses,
            distribution: distribution,
            plankRankBase: plankRankBase,
            millRankBase: millRankBase,
            workers: workers ?? MadeWorkers(stonecutter: true, fisher: true),
            materialOrder: materialOrder,
            blowSteps: blowSteps,
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
                .. new byte[RuleTables.GoodKindCount - classes.Length],
            ];
}
