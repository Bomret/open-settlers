using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Import.HistoryEdition.Testing;

namespace OpenSettlers.Tests;

// The commands of one launch of the emulator harness (tools/GhidraDump/emulate.py) are independent (INV-0018): a command that
// runs after others in one launch prints the lines it prints in a launch of its own. A command that fails ends a launch that is
// not a draw batch. The runs need Ghidra and the install, and are inconclusive without them.
[TestClass]
[TestCategory(Needs.Install)]
[TestCategory(Needs.Ghidra)]
public sealed class EmulatorLaunchTests
{
    const string Seed = "1234,5678,9ABC";

    static string[] Emulate(params string[] commands) => EmulateIn(null, commands);

    // With a project folder, emulate.py keeps the project there (--project): the first launch imports, later ones reuse it.
    static string[] EmulateIn(string? project, params string[] commands)
    {
        var run = Launch(project, commands);
        Assert.AreEqual(0, run.Exit, run.Stderr);

        return run.Stdout.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    static ToolRun Launch(string? project, string[] commands)
    {
        if (
            !(Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator)
                .Any(dir => File.Exists(Path.Combine(dir, "analyzeHeadless")))
        )
        {
            Assert.Inconclusive("analyzeHeadless is not on the path");
        }

        return PythonTool.Run(
            Path.GetTempPath(),
            [
                PythonTool.InRepo("tools", "GhidraDump", "emulate.py"),
                "--game",
                TestProgram.InstallRoot(),
                .. (project is null ? Array.Empty<string>() : ["--project", project]),
                .. commands,
            ],
            realHeadless: true
        );
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

    // The rate-0 supply lines of a launch equal the recording's lines before the pass 376.
    static void AssertRecorded(string[] lines) =>
        CollectionAssert.AreEqual(
            Recorded(376),
            lines
                .Where(line => line.StartsWith("EMU supply ") && line.Contains(" rate=0 "))
                .Select(Unlabelled)
                .ToArray()
        );

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

        AssertRecorded(lines);
    }

    // A command of one verb after a command of another: construct runs the phase routine 0x42965c at the last phase of each
    // cycle, and supply does not. The supply game prints the recording's generator words at pass 375 only where it runs
    // without the phase routine, which draws from the generator.
    [TestMethod]
    public void ACommandAfterACommandOfAnotherVerbRunsWithItsOwnVerbsSettingsOnly()
    {
        AssertRecorded(Emulate($"construct:1:{Seed}:0:18,20::::0:1:c@18,20", $"supply:1:{Seed}:0:18,20:0:376"));
    }

    // A command that throws after a command that printed a line: the call fails, prints nothing, and names the command.
    [TestMethod]
    public void ACallWithACommandThatThrowsFailsAndNamesTheCommand()
    {
        var run = Launch(null, ["rng:0000,0000,0000", "nosuchverb:1", "rng:0001,0000,0000"]);

        Assert.AreNotEqual(0, run.Exit);
        Assert.AreEqual("", run.Stdout);
        StringAssert.Contains(run.Stderr, "command 2 (nosuchverb:1) did not end");
    }

    // The carrier game of DrawVerbTests: a goods: game whose carrier is on its road at pass 100.
    const string Draw = $"draw:100-101:17,20:goods:1:{Seed}:0:18,20:16,20:19,21,343:16,20,2:102:0::-1:5,0,7";

    // A draw batch runs each command in a script instance of its own. The test guards state carried between the instances of a
    // batch and the batch's framing of each command's lines. It does not guard the cached generation against a command that
    // changes it: a draw line records only the settlers near the camera.
    // ACommandThatRestoresTheCachedGenerationAfterAnotherDidPrintsItsOwnLaunchLines guards that.
    [TestMethod]
    public void EachCommandOfADrawBatchPrintsTheLinesOfASingleDrawCommand()
    {
        var single = Emulate(Draw);
        var batch = Emulate(Draw, Draw, Draw);

        for (var command = 1; command <= 3; command++)
        {
            var start = Array.IndexOf(batch, $"EMU draw command={command} ended");
            Assert.IsTrue(start > 0, $"command {command} ended");
            CollectionAssert.AreEqual(
                single[1..],
                batch[(start + 1)..].TakeWhile(line => !line.StartsWith("EMU draw command=")).ToArray(),
                $"command {command}"
            );
        }
    }

    // A kept project (--project) after an earlier launch ran passes against it: the second launch runs the script against the
    // imported program, and the supply game prints the recording only where no emulated write of the first launch persisted.
    [TestMethod]
    public void ACommandAgainstAKeptProjectPrintsTheLinesOfAFreshImport()
    {
        var project = Path.Combine(Path.GetTempPath(), "emulator-launch-" + Guid.NewGuid().ToString("N"));
        try
        {
            EmulateIn(project, $"supply:1:{Seed}:0:18,20:40:400");
            AssertRecorded(EmulateIn(project, $"supply:1:{Seed}:0:18,20:0:376"));
        }
        finally
        {
            if (Directory.Exists(project))
            {
                Directory.Delete(project, true);
            }
        }
    }
}
