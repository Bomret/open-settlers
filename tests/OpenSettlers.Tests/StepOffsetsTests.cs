using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The step-offset table of entry 2 of the graphics archive (docs/rules/settlers.md, "Drawing the walk"): the placement of a
// walking settler reads the dx and dy of each step and nothing else of the entry. The entry of SettlerFiguresTests.Sequences:
// the frame 7 holds the triples (3, 1, 2), (4, -1, 0), (5, 0, 0); the frame 8 holds (0x81, 0, 0).
[TestClass]
public sealed class StepOffsetsTests
{
    [TestMethod]
    public void TheTimeLeftShiftedRightByThreePicksTheStepOfTheFramesSequence()
    {
        var steps = new StepOffsets(SettlerFiguresTests.Sequences());

        Assert.AreEqual(new StepOffset { Dx = 1, Dy = 2 }, steps.At(7, 0));
        Assert.AreEqual(new StepOffset { Dx = 1, Dy = 2 }, steps.At(7, 7));
        Assert.AreEqual(new StepOffset { Dx = -1, Dy = 0 }, steps.At(7, 8));
        Assert.AreEqual(new StepOffset { Dx = 0, Dy = 0 }, steps.At(7, 16));
    }

    [TestMethod]
    public void ADxAndDyAreSignedBytes()
    {
        var steps = new StepOffsets(SettlerFiguresTests.Sequences());

        Assert.AreEqual(-1, steps.At(7, 8)!.Value.Dx);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(9)]
    [DataRow(200)]
    public void AFrameWithNoSequenceHasNoStep(int frame)
    {
        Assert.IsNull(new StepOffsets(SettlerFiguresTests.Sequences()).At(frame, 0));
    }

    [TestMethod]
    public void ATimeLeftPastTheEndOfTheEntryHasNoStep()
    {
        Assert.IsNull(new StepOffsets(SettlerFiguresTests.Sequences()).At(8, 8000));
    }

    [TestMethod]
    public void AnEntryTooShortForItsTableHasNoStep()
    {
        Assert.IsNull(new StepOffsets([]).At(7, 0));
    }

    const string Archive = "loca/SPAD.PA";

    static Install Open(FakeInstall fake)
    {
        Assert.IsTrue(
            Install.TryOpen(OpenSettlers.Launch.LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _)
        );

        return install;
    }

    [TestMethod]
    public void LoadingReadsEntryTwoOfTheArchive()
    {
        using var fake = new FakeInstall().Put(
            Archive,
            ArchiveBytes.Build(3, new Dictionary<int, byte[]> { [2] = SettlerFiguresTests.Sequences() })
        );

        var steps = StepOffsets.Load(Open(fake));

        Assert.AreEqual(new StepOffset { Dx = -1, Dy = 0 }, steps.At(7, 8));
    }

    [TestMethod]
    public void AnArchiveWithoutEntryTwoIsRefusedByEntryAndReason()
    {
        using var fake = new FakeInstall().Put(Archive, ArchiveBytes.Build(3));

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => StepOffsets.Load(Open(fake)));

        StringAssert.Contains(failure.Message, "the walk sequences (entry 2) of loca/SPAD.PA is unreadable");
        StringAssert.Contains(failure.Message, "the archive has no such entry");
    }

    [TestMethod]
    public void AnArchiveTooShortToHoldEntryTwoIsRefusedByEntry()
    {
        using var fake = new FakeInstall().Put(Archive, ArchiveBytes.Build(2));

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => StepOffsets.Load(Open(fake)));

        StringAssert.Contains(failure.Message, "the walk sequences (entry 2)");
    }

    [TestMethod]
    public void AnEntryThatReachesPastTheArchiveIsRefusedByEntry()
    {
        var bytes = ArchiveBytes.Build(3, new Dictionary<int, byte[]> { [2] = [1, 2, 3, 4] });
        ArchiveBytes.Put(bytes, 2 * 8, 4000);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => StepOffsets.From(ArchiveFile.Read(bytes)));

        StringAssert.Contains(failure.Message, "the walk sequences (entry 2) of loca/SPAD.PA is unreadable");
    }
}
