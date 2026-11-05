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
    private static ProgramTables? _tables;

    /// <summary>
    /// Tables of made-up values, no restatement of the original's: every object code has class 0, so no object
    /// blocks a site, and the start stock rows count up from the row number. With every class 0 no object is large, so
    /// a map generated with these tables keeps every object and is not the original's map; tests that compare with the
    /// original's data use <see cref="Tables"/>. The transport priorities are all 0.
    /// </summary>
    public static ProgramTables Made() => Made(null);

    /// <summary>The tables of <see cref="Made()"/> with made-up transport priorities (index good - 1), or all 0 for none.</summary>
    public static ProgramTables Made(byte[]? transportPriorities) =>
        new(
            new byte[ProgramTables.ClassCount],
            Enumerable
                .Range(0, ProgramTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            transportPriorities: transportPriorities
        );

    /// <summary>The reference install named by <c>SIEDLER_DIR</c>; the test is inconclusive without it.</summary>
    public static Install OpenInstall()
    {
        var args = LaunchArgs.Parse([], Environment.GetEnvironmentVariable(LaunchArgs.GameVariable));
        if (!Install.TryOpen(args, out var install, out var problem))
        {
            Assert.Inconclusive($"The emulator values need the reference install: {problem}");
        }

        return install;
    }

    public static ProgramTables Tables()
    {
        if (_tables is not null)
        {
            return _tables;
        }

        var install = OpenInstall();

        try
        {
            return _tables = ProgramTables.Read(install.ReadAll(Install.Program));
        }
        catch (InvalidDataException e)
        {
            Assert.Inconclusive($"The install's program holds no rule tables: {e.Message}");
            throw;
        }
    }
}
