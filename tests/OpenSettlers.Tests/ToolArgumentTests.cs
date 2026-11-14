using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The argument and import handling of tools/GhidraDump/emulate.py and tools/WindowsCheck/digest.py, run as processes. None of
// these cases reaches the install or Ghidra: a stand-in analyzeHeadless records its arguments.
[TestClass]
public sealed class ToolArgumentTests
{
    string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tool-args-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    static string Emulate() => PythonTool.InRepo("tools", "GhidraDump", "emulate.py");

    static string Digest() => PythonTool.InRepo("tools", "WindowsCheck", "digest.py");

    static void AssertUsage(ToolRun run)
    {
        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "emulate.py:");
        StringAssert.Contains(run.Stderr, "Usage: python3 -I emulate.py");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    [DataRow("--game")]
    [DataRow("--project")]
    [DataRow("--game", "", "rng:0,0,0")]
    [DataRow("--game", "<folder>", "--project", "", "rng:0,0,0")]
    public void AFlagWithoutAFolderIsRefusedWithUsage(params string[] arguments) =>
        AssertUsage(
            PythonTool.Run(
                _folder,
                [Emulate(), .. arguments.Select(argument => argument == "<folder>" ? _folder : argument)]
            )
        );

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

        var run = PythonTool.Run(_folder, [Emulate(), "--game", game, "--project", "", "rng:0,0,0"], bin);

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

        PythonTool.Run(_folder, [Emulate(), "--game", game, "rng:0,0,0"], bin);

        var arguments = File.ReadAllLines(record);
        var scripts = arguments[Array.IndexOf(arguments, "-scriptPath") + 1];
        Assert.IsTrue(File.Exists(Path.Combine(scripts, "EmulateMapStart.java")), scripts);
    }

    [TestMethod]
    public void ImportingDigestPrintsNothingAndDoesNotExit()
    {
        // A leftover gitignored folder from an earlier import without -B is no failure of this run.
        var bytecode = Path.Combine(Path.GetDirectoryName(Digest())!, "__pycache__");
        var existed = Directory.Exists(bytecode);

        var run = PythonTool.Run(
            _folder,
            [
                "-c",
                $"import sys; sys.path.insert(0, r'{Path.GetDirectoryName(Digest())}'); import digest; print('imported')",
            ]
        );

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("imported", run.Stdout.Trim());
        Assert.IsTrue(existed || !Directory.Exists(bytecode), "a test run wrote bytecode into the source tree");
    }

    [TestMethod]
    public void DigestPrintsItsRowForTheRightArguments()
    {
        var dump = Path.Combine(_folder, "dump.bin");
        File.WriteAllBytes(dump, new byte[32 * 8 * 32]);

        var run = PythonTool.Run(_folder, [Digest(), dump, "1", "6359,a0d2,b4fa", "7590,f842,468d", "5"]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        StringAssert.StartsWith(run.Stdout, "Windows run | 1 6359,A0D2,B4FA CountResource1 7590,F842,468D ");
        StringAssert.EndsWith(run.Stdout.Trim(), " total=5");
    }

    [TestMethod]
    public void DigestRefusesASizeThatIsNotANumber()
    {
        var run = PythonTool.Run(_folder, [Digest(), Path.Combine(_folder, "none"), "big", "1,2,3", "4,5,6", "7"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "size");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    string Game(string programName = "Siedler1.exe")
    {
        var game = Path.Combine(_folder, "game");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, programName), [1, 2, 3]);

        return game;
    }

    string RecordingHeadless(string tail) =>
        PythonTool.StandInHeadless(
            Path.Combine(_folder, "bin"),
            $"printf '%s\\n' \"$@\" > '{Path.Combine(_folder, "args.txt")}'\n{tail}\n"
        );

    string[] RecordedArguments() => File.ReadAllLines(Path.Combine(_folder, "args.txt"));

    // emulate.py: its own refusals, in the order it checks them (the docstring and the code of tools/GhidraDump/emulate.py).

    [TestMethod]
    [DataRow("draw:1-1:0,0:a", "rng:0,0,0")]
    [DataRow("rng:0,0,0", "draw:1-1:0,0:a")]
    public void ADrawCommandMixedWithANonDrawCommandIsRefused(params string[] commands)
    {
        var run = PythonTool.Run(_folder, [Emulate(), "--game", Game(), .. commands]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "emulate.py: draw: runs alone");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void NoCommandIsRefusedWithUsage() => AssertUsage(PythonTool.Run(_folder, [Emulate(), "--game", Game()]));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NoInstallFolderIsRefusedWithTheWayToGiveOne(bool missingFolder)
    {
        string[] game = missingFolder ? ["--game", Path.Combine(_folder, "missing")] : [];

        var run = PythonTool.Run(_folder, [Emulate(), .. game, "rng:0,0,0"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "emulate.py: no install folder: pass --game <dir> or set SIEDLER_DIR");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void AMissingAnalyzeHeadlessIsRefusedWithTheWayToGetIt()
    {
        var run = PythonTool.Run(_folder, [Emulate(), "--game", Game(), "rng:0,0,0"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "emulate.py: analyzeHeadless is not on PATH");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void AGhidraThatExitsNonZeroFailsTheRunAndShowsItsOutput()
    {
        var bin = RecordingHeadless("echo 'INFO  EmulateMapStart.java> EMU ok'\necho STANDIN-LOG\nexit 3");

        var run = PythonTool.Run(_folder, [Emulate(), "--game", Game(), "rng:0,0,0"], bin);

        Assert.AreEqual(1, run.Exit);
        StringAssert.Contains(run.Stderr, "STANDIN-LOG");
    }

    [TestMethod]
    public void AGhidraThatPrintsNoEmuLineFailsTheRunAndShowsItsOutput()
    {
        var bin = RecordingHeadless("echo 'INFO  EmulateMapStart.java> no result'\necho STANDIN-LOG");

        var run = PythonTool.Run(_folder, [Emulate(), "--game", Game(), "rng:0,0,0"], bin);

        Assert.AreEqual(1, run.Exit);
        Assert.IsFalse(run.Stdout.Contains("EMU "), "no EMU line, no recording");
        StringAssert.Contains(run.Stderr, "STANDIN-LOG");
    }

    [TestMethod]
    [DataRow("Siedler1.exe")]
    [DataRow("siedler1.exe")]
    [DataRow("SIEDLER1.EXE")]
    public void TheProgramIsFoundWhateverTheCaseOfItsName(string name)
    {
        var game = Game(name);
        var bin = RecordingHeadless("echo 'INFO  EmulateMapStart.java> EMU ok'");

        var run = PythonTool.Run(_folder, [Emulate(), "--game", game, "rng:0,0,0"], bin);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("EMU ok", run.Stdout.Trim());
        var arguments = RecordedArguments();
        Assert.AreEqual(Path.Combine(game, name), arguments[Array.IndexOf(arguments, "-import") + 1]);
    }

    [TestMethod]
    public void AFolderWithNoProgramIsRefusedWithTheFolderNamed()
    {
        var game = Game("other.exe");

        var run = PythonTool.Run(_folder, [Emulate(), "--game", game, "rng:0,0,0"], RecordingHeadless("exit 0"));

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, $"emulate.py: no Siedler1.exe in {game}");
    }

    [TestMethod]
    public void WithoutGameTheInstallComesFromSiedlerDir()
    {
        var game = Game();
        var bin = RecordingHeadless("echo 'INFO  EmulateMapStart.java> EMU ok'");

        var run = PythonTool.Run(_folder, [Emulate(), "rng:0,0,0"], bin, siedlerDir: game);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var arguments = RecordedArguments();
        Assert.AreEqual(Path.Combine(game, "Siedler1.exe"), arguments[Array.IndexOf(arguments, "-import") + 1]);
    }

    [TestMethod]
    public void TheGameFlagOverridesSiedlerDir()
    {
        var game = Game();
        var bin = RecordingHeadless("echo 'INFO  EmulateMapStart.java> EMU ok'");

        var run = PythonTool.Run(
            _folder,
            [Emulate(), "--game", game, "rng:0,0,0"],
            bin,
            siedlerDir: Path.Combine(_folder, "elsewhere")
        );

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var arguments = RecordedArguments();
        Assert.AreEqual(Path.Combine(game, "Siedler1.exe"), arguments[Array.IndexOf(arguments, "-import") + 1]);
    }

    // digest.py. The expected digests are FNV-1a 64 over each layer in row-major order, computed outside this repository's
    // code with a separate implementation (node BigInt, offsets from docs/rules/map-form.md as digest.py's docstring cites
    // them): a at y * 8 * width + x * 4 and b = a + 4 * width; flags a, height a + 1, terrain a + 2, lake b, object a + 3,
    // resource b. The dump's byte i is i * 7 + (i >> 8) * 13 + 3 modulo 256, so every offset holds a different byte.

    [TestMethod]
    [DataRow(
        1,
        "D871A1A96E8660A5 9A4348E0C1E647A5 CC99F4A73EF061E5 2911A162A38DEBA5 EDE936C6C7367E65 2911A162A38DEBA5"
    )]
    [DataRow(
        2,
        "9FE64BD2B22CC125 283F8B0F1C7A5925 8FE3EB50C76206A5 ABD3870F8BA5C525 B8F752FB97BB7325 ABD3870F8BA5C525"
    )]
    [DataRow(
        3,
        "290AC421349AF725 FE2EA6DC437C2325 40A8F022F3459A25 B062C3B1E8299725 5969DEBA62D56325 B062C3B1E8299725"
    )]
    public void DigestReadsEachLayerFromItsOffsetInRowMajorOrder(int size, string digests)
    {
        var (width, height) = size switch
        {
            1 => (32, 32),
            2 => (64, 32),
            _ => (64, 64),
        };
        var bytes = new byte[height * 8 * width];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i * 7 + (i >> 8) * 13 + 3);
        }

        var dump = Path.Combine(_folder, "dump.bin");
        File.WriteAllBytes(dump, bytes);

        var run = PythonTool.Run(_folder, [Digest(), dump, size.ToString(), "6359,a0d2,b4fa", "7590,f842,468d", "5"]);

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual(
            $"Windows run | {size} 6359,A0D2,B4FA CountResource1 7590,F842,468D {digests} total=5",
            run.Stdout.Trim()
        );
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(4)]
    [DataRow(6)]
    public void DigestWithTheWrongNumberOfArgumentsPrintsItsUsage(int count)
    {
        var run = PythonTool.Run(_folder, [Digest(), .. Enumerable.Repeat("x", count)]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "Usage: python3 -I digest.py <dump-file> <size> <seed> <rng> <total>");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("4")]
    public void DigestRefusesASizeOutsideOneToThree(string size)
    {
        var run = PythonTool.Run(_folder, [Digest(), Path.Combine(_folder, "none"), size, "1,2,3", "4,5,6", "7"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "the size must be 1, 2, or 3");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void DigestRefusesADumpOfTheWrongLengthWithTheLengthItNeeds()
    {
        var dump = Path.Combine(_folder, "dump.bin");
        File.WriteAllBytes(dump, [1, 2, 3]);

        var run = PythonTool.Run(_folder, [Digest(), dump, "1", "1,2,3", "4,5,6", "7"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, "size 1 needs 8192 bytes, the dump has 3");
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }

    [TestMethod]
    public void DigestRefusesADumpItCannotReadWithTheFileNamed()
    {
        var dump = Path.Combine(_folder, "missing.bin");

        var run = PythonTool.Run(_folder, [Digest(), dump, "1", "1,2,3", "4,5,6", "7"]);

        Assert.AreNotEqual(0, run.Exit);
        StringAssert.Contains(run.Stderr, dump);
        Assert.IsFalse(run.Stderr.Contains("Traceback"), run.Stderr);
    }
}
