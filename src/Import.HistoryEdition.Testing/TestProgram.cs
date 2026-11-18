using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition.Testing;

/// <summary>
/// The rule tables of the install's <c>Siedler1.exe</c> (INV-0001): the emulator values the tests compare with are
/// those of that program, so <see cref="Tables"/> needs the install named by <c>SIEDLER_DIR</c> and the test is
/// inconclusive without it.
/// </summary>
public static class TestProgram
{
    /// <summary>The variable that names the install.</summary>
    const string GameVariable = "SIEDLER_DIR";

    static RuleTables? _tables;

    /// <summary>
    /// The reference install named by <c>SIEDLER_DIR</c>; the test is inconclusive without it, and where its program
    /// holds no rule tables.
    /// </summary>
    public static Install OpenInstall()
    {
        var game = Environment.GetEnvironmentVariable(GameVariable);
        if (string.IsNullOrEmpty(game))
        {
            Assert.Inconclusive($"The emulator values need the reference install: no install given, set {GameVariable}");
        }

        if (!Install.TryOpen(game, out var install, out var problem))
        {
            Assert.Inconclusive($"The emulator values need the reference install: {problem}");
        }

        if (_tables is null)
        {
            try
            {
                _tables = new HistoryEditionRuleTables(install).Read();
            }
            catch (InvalidDataException e)
            {
                Assert.Inconclusive($"The install's program holds no rule tables: {e.Message}");
            }
        }

        return install;
    }

    /// <summary>The rule tables of the install's program.</summary>
    public static RuleTables Tables()
    {
        if (_tables is null)
        {
            OpenInstall();
        }

        return _tables!;
    }
}
