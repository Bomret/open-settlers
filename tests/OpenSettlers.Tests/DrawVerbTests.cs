using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The command line of the emulator verb draw (tools/GhidraDump/emulate.py, EmulateDraw.java): it exits non-zero and prints
// nothing, so no recording is written, where the install or its program is missing or is not the reference build, where a pass
// of a window draws no settler, or where the run reaches no window pass. A successful run prints the install line and the
// lines of the recording. The runs that need Ghidra and the install are inconclusive without them.
[TestClass]
public sealed class DrawVerbTests
{
    // The carrier game of the recordings (goods:, a log laid on the hut's flag before pass 5): the carrier is on its road at
    // pass 100, the first pass the view finds a settler in; no settler is out before pass 30.
    private const string Game = "goods:1:1234,5678,9ABC:0:18,20:16,20:19,21,343:16,20,2:102:0::-1:5,0,7";

    private string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "draw-verb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "GhidraDump", "emulate.py")))
        {
            dir =
                Path.GetDirectoryName(dir)
                ?? throw new InvalidOperationException("tools/GhidraDump/emulate.py not found");
        }

        return dir;
    }

    private static (int Exit, string Output) Emulate(string game, string command)
    {
        var info = new ProcessStartInfo("python3")
        {
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-I");
        info.ArgumentList.Add("tools/GhidraDump/emulate.py");
        info.ArgumentList.Add("--game");
        info.ArgumentList.Add(game);
        info.ArgumentList.Add(command);
        try
        {
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output.Result);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Inconclusive("python3 is not on the path");
            throw;
        }
    }

    // Ghidra on the path and the reference install: the runs that reach the emulator need both.
    private static string ReferenceInstall()
    {
        if (
            !(Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator)
                .Any(dir => File.Exists(Path.Combine(dir, "analyzeHeadless")))
        )
        {
            Assert.Inconclusive("analyzeHeadless is not on the path");
        }

        var install = TestProgram.OpenInstall();

        return install.Root;
    }

    [TestMethod]
    public void ARunWithNoInstallExitsNonZeroAndPrintsNothing()
    {
        var (exit, output) = Emulate(Path.Combine(_folder, "missing"), $"draw:100-101:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunOnAnInstallWithNoProgramExitsNonZeroAndPrintsNothing()
    {
        var (exit, output) = Emulate(_folder, $"draw:100-101:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunOnAProgramOfAnotherBuildExitsNonZeroAndPrintsNothing()
    {
        File.WriteAllText(Path.Combine(_folder, "Siedler1.exe"), "not the history edition");

        var (exit, output) = Emulate(_folder, $"draw:100-101:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunWhoseWindowHoldsAPassWithNoSettlerDrawExitsNonZeroAndPrintsNothing()
    {
        var install = ReferenceInstall();

        var (exit, output) = Emulate(install, $"draw:3-4:17,20:{Game}");

        Assert.AreNotEqual(0, exit, "no settler is out at pass 3");
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunWhoseWindowsAreBeyondItsLastPassReachesNoDrawPathAndExitsNonZero()
    {
        var install = ReferenceInstall();

        var (exit, output) = Emulate(install, $"draw:500-501:17,20:{Game}");

        Assert.AreNotEqual(0, exit);
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void ARunOnTheReferenceInstallPrintsTheInstallLineAndOneLineForEachPassOfItsWindow()
    {
        var install = ReferenceInstall();

        var (exit, output) = Emulate(install, $"draw:100-101:17,20:{Game}");

        Assert.AreEqual(0, exit);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        StringAssert.StartsWith(lines[0], "EMU draw install=History Edition ");
        CollectionAssert.AreEqual(
            new[] { "EMU draw pass=100 clock=808", "EMU draw pass=101 clock=816" },
            lines
                .Where(line => line.Contains(" pass="))
                .Select(line => string.Join(' ', line.Split(' ').Take(4)))
                .ToArray()
        );
        Assert.IsTrue(lines.All(line => !line.Contains(" pass=") || line.Contains(" d=")));
        StringAssert.StartsWith(lines[^1], "EMU draw end");
    }
}
