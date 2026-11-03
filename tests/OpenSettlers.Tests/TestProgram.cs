using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// The rule tables of the install's <c>Siedler1.exe</c> (INV-0001): the emulator values the tests compare with are
/// those of that program, so these tests need the install named by <c>SIEDLER_DIR</c> and are inconclusive without it.
/// </summary>
internal static class TestProgram
{
    private static ProgramTables? _tables;

    public static ProgramTables Tables()
    {
        if (_tables is not null)
        {
            return _tables;
        }

        var args = LaunchArgs.Parse([], Environment.GetEnvironmentVariable(LaunchArgs.GameVariable));
        if (!Install.TryOpen(args, out var install, out var problem))
        {
            Assert.Inconclusive($"The emulator values need the reference install: {problem}");
        }

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
