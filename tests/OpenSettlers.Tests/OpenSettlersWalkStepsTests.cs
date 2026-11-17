using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The walk steps port of the basic content set (IWalkSteps): the checked-in file content/basic/rules/walk-steps.jsonc loads with no
// install, covers every frame and timer a settler of the game can hold, and a broken file is refused with its file and key.
[TestClass]
public sealed class OpenSettlersWalkStepsTests
{
    const string File = OpenSettlersWalkSteps.WalkStepsFile;

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output");
    }

    static string BasicFolder() => Path.Combine(RepositoryRoot(), "content", "basic");

    static OpenSettlersWalkSteps Checked() => OpenSettlersWalkSteps.Load(BasicFolder());

    // A copy of the checked-in walk step file in the temp folder; a test edits its text and reads.
    sealed class StepFolder : IDisposable
    {
        public StepFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "open-settlers-steps-" + Guid.NewGuid().ToString("N"));
            var target = Path.Combine(Root, File);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            System.IO.File.Copy(Path.Combine(BasicFolder(), File), target);
        }

        public string Root { get; }

        public void Replace(string find, string replacement)
        {
            var path = Path.Combine(Root, File);
            var text = System.IO.File.ReadAllText(path);
            Assert.Contains(find, text, $"the fixture edit {find} is not in {File}");
            System.IO.File.WriteAllText(path, text.Replace(find, replacement));
        }

        public void Write(string text) => System.IO.File.WriteAllText(Path.Combine(Root, File), text);

        public string Refusal() =>
            Assert.ThrowsExactly<InvalidDataException>(() => OpenSettlersWalkSteps.Load(Root)).Message;

        public void Dispose() => Directory.Delete(Root, true);
    }

    static void Refuses(string find, string replacement, string key, string reason)
    {
        using var folder = new StepFolder();
        folder.Replace(find, replacement);

        var message = folder.Refusal();

        Assert.StartsWith($"{File}: {key}: ", message);
        Assert.Contains(reason, message);
    }

    [TestMethod]
    public void TheCheckedInFileLoadsWithNoInstall() => Assert.IsNotNull(Checked());

    // A settler's frame is a byte and its timer 16 bits (Settler.Frame, Settler.Timer), so these are all the pairs the game can
    // hold, among them the frames the simulation sets and the timers its step times, door speeds, and work timers produce.
    [TestMethod]
    public void EveryFrameAndEveryTimerAGameCanHoldHasAStep()
    {
        var steps = Checked();

        for (var frame = 0; frame < 256; frame++)
        {
            for (var timeLeft = 0; timeLeft <= ushort.MaxValue; timeLeft++)
            {
                if (steps.At(frame, timeLeft) is null)
                {
                    Assert.Fail($"no step at frame {frame} with time left {timeLeft}");
                }
            }
        }
    }

    [TestMethod]
    public void AFrameOrATimeOutsideWhatTheGameHoldsHasNoStep()
    {
        var steps = Checked();

        Assert.IsNull(steps.At(-1, 0));
        Assert.IsNull(steps.At(256, 0));
        Assert.IsNull(steps.At(4, -1));
    }

    // Shape 4 is a flat step east (4 + 9 * 0 + 0); the settler stands on the node it reached and is drawn back toward the
    // node it left by 4 pixels for each 8 clock units of the flat step's 64 that are left.
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(7, 0)]
    [DataRow(8, -4)]
    [DataRow(32, -16)]
    [DataRow(64, -32)]
    [DataRow(127, -32)]
    [DataRow(5000, -32)]
    public void AFlatStepEastIsDrawnBackTowardTheNodeItLeft(int timeLeft, int dx)
    {
        var step = Checked().At(4, timeLeft);

        Assert.AreEqual(new StepOffset { Dx = dx, Dy = 0 }, step);
    }

    [TestMethod]
    [DataRow(13, 1, -16, -20, "south-east")]
    [DataRow(22, 2, 16, -20, "south")]
    [DataRow(31, 3, 32, 0, "west")]
    [DataRow(40, 4, 16, 20, "north-west")]
    [DataRow(49, 5, -16, 20, "north")]
    public void AFullStepIsDrawnOneNodeBackInItsDirection(int frame, int direction, int dx, int dy, string name)
    {
        // Frames 4 + 9 * direction + 0, and the same six directions again from 54 (4 + 9 * (direction + 6)).
        Assert.AreEqual(frame, 4 + 9 * direction, name);
        var again = 4 + 9 * (direction + 6);
        var expected = new StepOffset { Dx = dx, Dy = dy };

        Assert.AreEqual(expected, Checked().At(frame, 64), name);
        Assert.AreEqual(expected, Checked().At(again, 64), name);
    }

    [TestMethod]
    [DataRow(108)]
    [DataRow(0x74)]
    [DataRow(0x8c)]
    [DataRow(255)]
    public void AFrameOfAWorkingOrRestingSettlerStandsOnItsNode(int frame) =>
        Assert.AreEqual(new StepOffset { Dx = 0, Dy = 0 }, Checked().At(frame, 0x3ff));

    [TestMethod]
    public void AMissingFileIsRefusedWithItsName()
    {
        using var folder = new StepFolder();
        System.IO.File.Delete(Path.Combine(folder.Root, File));

        StringAssert.StartsWith(folder.Refusal(), $"{File}: ");
    }

    [TestMethod]
    public void MalformedTextIsRefusedWithTheFileAndTheLine()
    {
        using var folder = new StepFolder();
        folder.Write("{ \"sequences\": ");

        var message = folder.Refusal();

        Assert.StartsWith($"{File}: line ", message);
        Assert.Contains("not valid JSONC", message);
    }

    [TestMethod]
    public void AMissingKeyIsRefusedAtThatKey() =>
        Refuses("\"default\": \"still\",", "", "default", "missing");

    [TestMethod]
    public void AMissingSequenceIsRefusedWhereAFrameNamesIt() =>
        Refuses("\"east\": [[0, 8], [54, 62]],", "\"sprint\": [[0, 8], [54, 62]],", "frames.sprint", "unknown sequence");

    [TestMethod]
    public void ADefaultThatNamesNoSequenceIsRefused() =>
        Refuses("\"default\": \"still\"", "\"default\": \"standing\"", "default", "unknown sequence \"standing\"");

    [TestMethod]
    public void AShortSequenceIsRefusedAtItsKey()
    {
        using var folder = new StepFolder();
        folder.Replace(
            "\"west\": [\n      [0, 0], [4, 0], [8, 0], [12, 0], [16, 0], [20, 0], [24, 0], [28, 0],\n      [32, 0], [32, 0], [32, 0], [32, 0], [32, 0], [32, 0], [32, 0], [32, 0],\n    ],",
            "\"west\": [[0, 0], [4, 0]],"
        );

        var message = folder.Refusal();

        Assert.StartsWith($"{File}: sequences.west: ", message);
        Assert.Contains("holds 2 entries, expected 16", message);
    }

    [TestMethod]
    public void AnEntryThatIsNoPairIsRefusedAtItsIndex() =>
        Refuses("[-4, 0], [-8, 0]", "[-4, 0, 1], [-8, 0]", "sequences.east[1]", "holds 3 entries, expected 2");

    [TestMethod]
    public void ADuplicateSequenceIsRefusedAtItsKey() =>
        Refuses("\"still\": [", "\"east\": [[0, 0]],\n    \"still\": [", "sequences.east", "duplicate key");

    [TestMethod]
    public void ADuplicateKeyOfTheFileIsRefusedAtItsKey() =>
        Refuses("\"default\": \"still\",", "\"default\": \"still\", \"default\": \"still\",", "default", "duplicate key");

    [TestMethod]
    [DataRow("[-4, 0], [-8, 0]", "[-4, 0], [-128, 0]", "sequences.east[2][0]", "-128 is outside -127 to 127")]
    [DataRow("[-4, 0], [-8, 0]", "[-4, 0], [-8, 200]", "sequences.east[2][1]", "200 is outside -127 to 127")]
    [DataRow("[-4, 0], [-8, 0]", "[-4, 0], [-8.5, 0]", "sequences.east[2][0]", "not a whole number")]
    [DataRow("[-4, 0], [-8, 0]", "[-4, 0], [\"-8\", 0]", "sequences.east[2][0]", "not a whole number")]
    public void AValueOutOfRangeIsRefusedAtItsKey(string find, string replacement, string key, string reason) =>
        Refuses(find, replacement, key, reason);

    [TestMethod]
    [DataRow("[[0, 8], [54, 62]]", "[[0, 8], [54, 256]]", "frames.east[1][1]", "256 is outside 0 to 255")]
    [DataRow("[[0, 8], [54, 62]]", "[[8, 0], [54, 62]]", "frames.east[0]", "runs backwards")]
    // The third case lets the east list reach frame 9, which the south-east list, read after it, lists again.
    [DataRow("[[0, 8], [54, 62]]", "[[0, 9], [54, 62]]", "frames.south-east[0]", "frame 9 is listed twice")]
    public void AFrameOutOfRangeOrListedTwiceIsRefusedAtItsKey(string find, string replacement, string key, string reason) =>
        Refuses(find, replacement, key, reason);
}
