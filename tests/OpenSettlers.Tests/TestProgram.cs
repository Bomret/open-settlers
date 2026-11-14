using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// The rule tables of the install's <c>Siedler1.exe</c> (INV-0001): the emulator values the tests compare with are
/// those of that program, so <see cref="Tables"/> needs the install named by <c>SIEDLER_DIR</c> and the test is
/// inconclusive without it. <see cref="Made"/> holds tables a test builds in code, for the tests that check the
/// game's own logic and hold no original value.
/// </summary>
static class TestProgram
{
    static RuleTables? _tables;

    /// <summary>
    /// Tables of made-up values, no restatement of the original's: every object code has class 0, so no object
    /// blocks a site, and the start stock rows count up from the row number. With every class 0 no object is large, so
    /// a map generated with these tables keeps every object and is not the original's map; tests that compare with the
    /// original's data use <see cref="Tables"/>. The transport priorities are all 0.
    /// </summary>
    public static RuleTables Made() => Made(null);

    /// <summary>The tables of <see cref="Made()"/> with made-up transport priorities (index good - 1), or all 0 for none.</summary>
    public static RuleTables Made(byte[]? transportPriorities) =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            [
                .. Enumerable
                    .Range(0, RuleTables.StartStockRows)
                    .Select(row =>
                        Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray()
                    ),
            ],
            transportPriorities: transportPriorities,
            workers: MadeWorkers(),
            jobData: MadeJobData()
        );

    /// <summary>The job of each number 0 to 31.</summary>
    public static IEnumerable<Job> Jobs() => Enumerable.Range(0, ProgramTablesImporter.JobNumbers).Select(Job.Of);

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
    /// with no tool, and with <paramref name="stonecutter"/> the stonecutter's building a stonecutter with no tool.
    /// </summary>
    public static WorkerRequest?[] MadeWorkers(bool stonecutter = false)
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

        return workers;
    }

    /// <summary>
    /// The reference install named by <c>SIEDLER_DIR</c>; the test is inconclusive without it, and where its program
    /// holds no rule tables.
    /// </summary>
    public static Install OpenInstall()
    {
        var args = LaunchArgs.Parse([], Environment.GetEnvironmentVariable(LaunchArgs.GameVariable));
        if (!Install.TryOpen(args, out var install, out var problem))
        {
            Assert.Inconclusive($"The emulator values need the reference install: {problem}");
        }

        if (_tables is null)
        {
            try
            {
                _tables = ProgramTablesImporter.Import(install.ReadAll(Install.Program));
            }
            catch (InvalidDataException e)
            {
                Assert.Inconclusive($"The install's program holds no rule tables: {e.Message}");
            }
        }

        return install;
    }

    public static RuleTables Tables()
    {
        if (_tables is null)
        {
            OpenInstall();
        }

        return _tables!;
    }
}
