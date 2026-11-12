using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The argument and import handling of tools/GhidraDump/emulate.py and tools/WindowsCheck/digest.py, run as processes. None of
// these cases reaches the install or Ghidra: a stand-in analyzeHeadless records its arguments.
[TestClass]
public sealed class ToolArgumentTests
{
    private string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tool-args-" + Guid.NewGuid().ToString("N"));
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

    private static (int Exit, string Stdout, string Stderr) Python(
        string workingDirectory,
        string[] arguments,
        string? path = null
    )
    {
        var info = new ProcessStartInfo("python3")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-I");
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment.Remove("SIEDLER_DIR");
        if (path is not null)
        {
            info.Environment["PATH"] = path + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        }

        try
        {
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            return (process.ExitCode, stdout.Result, stderr.Result);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Inconclusive("python3 is not on the path");
            throw;
        }
    }

    private static string Emulate() => Path.Combine(RepoRoot(), "tools", "GhidraDump", "emulate.py");

    private static string Digest() => Path.Combine(RepoRoot(), "tools", "WindowsCheck", "digest.py");

    private static void AssertUsage((int Exit, string Stdout, string Stderr) run)
    {
        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "emulate.py:");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void GameGivenLastIsRefusedWithUsage() => AssertUsage(Python(_folder, [Emulate(), "--game"]));

    [TestMethod]
    public void ProjectGivenLastIsRefusedWithUsage() => AssertUsage(Python(_folder, [Emulate(), "--project"]));

    [TestMethod]
    public void EmptyGameIsRefusedWithUsage() => AssertUsage(Python(_folder, [Emulate(), "--game", "", "rng:0,0,0"]));

    [TestMethod]
    public void EmptyProjectIsRefusedWithUsage() =>
        AssertUsage(Python(_folder, [Emulate(), "--game", _folder, "--project", "", "rng:0,0,0"]));

    [TestMethod]
    public void EmptyProjectDeletesNothing()
    {
        foreach (var name in new[] { "Emu.gpr", "program.sha256" })
        {
            File.WriteAllText(Path.Combine(_folder, name), "keep");
        }

        Directory.CreateDirectory(Path.Combine(_folder, "Emu.rep"));
        var game = Path.Combine(_folder, "game");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "Siedler1.exe"), [1, 2, 3]);
        var bin = Path.Combine(_folder, "bin");
        Directory.CreateDirectory(bin);
        var headless = Path.Combine(bin, "analyzeHeadless");
        File.WriteAllText(headless, "#!/bin/sh\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(headless, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var run = Python(_folder, [Emulate(), "--game", game, "--project", "", "rng:0,0,0"], bin);

        AssertUsage(run);
        Assert.IsTrue(File.Exists(Path.Combine(_folder, "Emu.gpr")));
        Assert.IsTrue(File.Exists(Path.Combine(_folder, "program.sha256")));
        Assert.IsTrue(Directory.Exists(Path.Combine(_folder, "Emu.rep")));
    }

    [TestMethod]
    public void ScriptsAreFoundFromOutsideTheRepositoryRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the stand-in analyzeHeadless is a shell script");

            return;
        }

        var bin = Path.Combine(_folder, "bin");
        var game = Path.Combine(_folder, "game");
        var record = Path.Combine(_folder, "args.txt");
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "Siedler1.exe"), [1, 2, 3]);
        var headless = Path.Combine(bin, "analyzeHeadless");
        File.WriteAllText(headless, $"#!/bin/sh\nprintf '%s\\n' \"$@\" > '{record}'\nexit 1\n");
        File.SetUnixFileMode(headless, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        Python(_folder, [Emulate(), "--game", game, "rng:0,0,0"], bin);

        var arguments = File.ReadAllLines(record);
        var scripts = arguments[Array.IndexOf(arguments, "-scriptPath") + 1];
        Assert.IsTrue(File.Exists(Path.Combine(scripts, "EmulateMapStart.java")), scripts);
    }

    [TestMethod]
    public void ImportingDigestPrintsNothingAndDoesNotExit()
    {
        var run = Python(
            _folder,
            ["-c", $"import sys; sys.path.insert(0, r'{Path.GetDirectoryName(Digest())}'); import digest; print('imported')"]
        );

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("imported", run.Stdout.Trim());
    }

    [TestMethod]
    public void DigestPrintsItsRowForTheRightArguments()
    {
        var dump = Path.Combine(_folder, "dump.bin");
        File.WriteAllBytes(dump, new byte[32 * 8 * 32]);

        var run = Python(_folder, [Digest(), dump, "1", "6359,a0d2,b4fa", "7590,f842,468d", "5"]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        StringAssert.StartsWith(run.Stdout, "Windows run | 1 6359,A0D2,B4FA CountResource1 7590,F842,468D ");
        StringAssert.EndsWith(run.Stdout.Trim(), " total=5");
    }

    [TestMethod]
    public void DigestRefusesASizeThatIsNotANumber()
    {
        var run = Python(_folder, [Digest(), Path.Combine(_folder, "none"), "big", "1,2,3", "4,5,6", "7"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "size");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }
}
