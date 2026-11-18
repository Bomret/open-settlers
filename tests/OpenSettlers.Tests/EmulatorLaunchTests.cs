using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The commands of one launch of the emulator harness (tools/GhidraDump/emulate.py) are independent (INV-0018): a command that
// runs after others in one launch prints the lines it prints in a launch of its own. The runs need Ghidra and the install, and
// are inconclusive without them.
[TestClass]
[TestCategory(Needs.Install)]
[TestCategory(Needs.Ghidra)]
public sealed class EmulatorLaunchTests
{
    const string Seed = "1234,5678,9ABC";

    static string[] Emulate(params string[] commands)
    {
        if (
            !(Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator)
                .Any(dir => File.Exists(Path.Combine(dir, "analyzeHeadless")))
        )
        {
            Assert.Inconclusive("analyzeHeadless is not on the path");
        }

        var run = PythonTool.Run(
            Path.GetTempPath(),
            [
                PythonTool.InRepo("tools", "GhidraDump", "emulate.py"),
                "--game",
                TestProgram.OpenInstall().Root,
                .. commands,
            ],
            realHeadless: true
        );
        Assert.AreEqual(0, run.Exit, run.Stderr);

        return run.Stdout.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    // A supply line without its label (verb, size, seed, castle, level, rate), as settler-supply.txt holds it.
    static string Unlabelled(string line) => string.Join(' ', line.Split(' ').Skip(7));

    // The game rate-0-weapons of settler-supply.txt, recorded in a launch of its own: its start line and its lines before the
    // pass `passes`.
    static string[] Recorded(int passes) =>
        [
            .. File.ReadAllLines("settler-supply.txt")
                .SkipWhile(line => !line.StartsWith("# rate-0-weapons ", StringComparison.Ordinal))
                .Skip(1)
                .TakeWhile(line => !line.StartsWith("# ", StringComparison.Ordinal))
                .Where(line =>
                    line.StartsWith("pass=start ", StringComparison.Ordinal)
                    || int.Parse(line.Split(' ')[0][5..], CultureInfo.InvariantCulture) < passes
                ),
        ];

    // A command that restores the cached map generation after an earlier command restored it and ran passes on it: the first
    // command generates the map, the second restores it and runs 400 passes, the third restores it again. The third prints the
    // recording's generator words at pass 375 only where the second's passes left the cached generation as the first made it.
    [TestMethod]
    public void ACommandThatRestoresTheCachedGenerationAfterAnotherDidPrintsItsOwnLaunchLines()
    {
        var lines = Emulate(
            $"castle:1:{Seed}:0:18,20",
            $"supply:1:{Seed}:0:18,20:40:400",
            $"supply:1:{Seed}:0:18,20:0:376"
        );

        CollectionAssert.AreEqual(
            Recorded(376),
            lines
                .Where(line => line.StartsWith("EMU supply ") && line.Contains(" rate=0 "))
                .Select(Unlabelled)
                .ToArray()
        );
    }
}
