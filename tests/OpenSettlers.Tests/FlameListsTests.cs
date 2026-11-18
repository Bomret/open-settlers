using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static OpenSettlers.Testing.ProgramImage;
using static OpenSettlers.Tests.FlameListImage;

namespace OpenSettlers.Tests;

// The flame lists of the original's burning draw, read from a program image built in code (docs/rules/removal.md, "The burning
// draw"): a header of 25 word offsets from 0x44e114, each list 3-byte flames of a size byte and a signed x and y, ended by a byte
// of 0x80 or more.
[TestClass]
public sealed class FlameListsTests
{
    static Flame Small(int x, int y) =>
        new()
        {
            Size = FlameSize.Small,
            X = x,
            Y = y,
        };

    static Flame Large(int x, int y) =>
        new()
        {
            Size = FlameSize.Large,
            X = x,
            Y = y,
        };

    [TestMethod]
    public void EachTypeTakesTheListOfItsNumberWithSizesAndSignedOffsets()
    {
        var lists = FlameLists.Read(Executable(Lists));

        CollectionAssert.AreEqual(
            new[] { Small(-3, 5), Large(4, -12) },
            lists.Of(BuildingType.Woodcutter, earlySite: false).ToArray()
        );
        CollectionAssert.AreEqual(new[] { Small(0, 0) }, lists.Of(BuildingType.Fisher, earlySite: false).ToArray());
        CollectionAssert.AreEqual(
            new[] { Large(2, 3) },
            lists.Of(new BuildingType { Code = 7 }, earlySite: false).ToArray()
        );
        CollectionAssert.AreEqual(
            lists.Of(new BuildingType { Code = 7 }, earlySite: false).ToArray(),
            lists.Of(new BuildingType { Code = 8 }, earlySite: false).ToArray()
        );
    }

    [TestMethod]
    public void AnEarlySiteTakesList0WhateverItsType()
    {
        var lists = FlameLists.Read(Executable(Lists));

        CollectionAssert.AreEqual(new[] { Large(1, -2) }, lists.Of(BuildingType.Woodcutter, earlySite: true).ToArray());
        CollectionAssert.AreEqual(new[] { Large(1, -2) }, lists.Of(BuildingType.Sawmill, earlySite: true).ToArray());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(25)]
    public void ATypeOutside0To24GivesNoFlames(int code)
    {
        var lists = FlameLists.Read(Executable(Lists));

        Assert.IsEmpty(lists.Of(new BuildingType { Code = code }, earlySite: false));
    }

    [TestMethod]
    public void AHeaderOffsetPastTheImageFailsTheLoad()
    {
        var exe = Executable(exe =>
        {
            Lists(exe);
            BitConverter.GetBytes((short)0x7000).CopyTo(exe, At(FlameLists.Address) + 2 * 3);
        });

        Assert.Throws<InvalidDataException>(() => FlameLists.Read(exe));
    }

    [TestMethod]
    public void AnUnterminatedListThatRunsPastTheImageFailsTheLoad()
    {
        // The section ends 8 bytes after the last list starts, and none of those bytes ends it.
        var size = FlameLists.Address - ImageBase - SectionStart + 120 + 8;
        var exe = Executable(
            exe =>
            {
                Lists(exe);
                BitConverter.GetBytes((short)120).CopyTo(exe, At(FlameLists.Address) + 2 * 24);
            },
            size
        );

        Assert.Throws<InvalidDataException>(() => FlameLists.Read(exe));
    }

    [TestMethod]
    [DataRow((byte)4)]
    [DataRow((byte)0x7f)]
    public void ASizeByteNeither0Nor8FailsTheLoad(byte size)
    {
        var exe = Executable(exe =>
        {
            Lists(exe);
            exe[At(FlameLists.Address) + 70] = size;
        });

        Assert.Throws<InvalidDataException>(() => FlameLists.Read(exe));
    }

    [TestMethod]
    [DataRow(FlameSize.Small, 0, 456)]
    [DataRow(FlameSize.Small, 7, 463)]
    [DataRow(FlameSize.Large, 0, 464)]
    [DataRow(FlameSize.Large, 7, 471)]
    public void AFlameDrawsTheEntryOf136PlusItsSizeAndPhasePlus320(FlameSize size, int phase, int entry)
    {
        Assert.AreEqual(entry, FlameLists.Entry(size, phase));
    }
}
