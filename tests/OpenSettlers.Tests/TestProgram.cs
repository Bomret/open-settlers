using System;
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
internal static class TestProgram
{
    private static RuleTables? _tables;

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
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            transportPriorities: transportPriorities
        );

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
