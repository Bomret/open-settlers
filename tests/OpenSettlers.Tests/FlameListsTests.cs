using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The flame lists of the original's burning draw, read from a program image built in code (docs/rules/removal.md, "The burning
// draw"): a header of 25 word offsets from 0x44e114, each list 3-byte flames of a size byte and a signed x and y, ended by a byte
// of 0x80 or more.
[TestClass]
public sealed class FlameListsTests
{
    const int ImageBase = 0x400000;
    const int SectionStart = 0x4e000;
    const int RawStart = 0x200;

    // A PE32 file with one section that holds the addresses from 0x44e000 on, `size` bytes of it, at the file offset 0x200.
    internal static byte[] Executable(Action<byte[]> fill, int size = 0x400)
    {
        var exe = new byte[RawStart + size];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        BitConverter.GetBytes(0x40).CopyTo(exe, 0x3c);
        BitConverter.GetBytes(0x4550).CopyTo(exe, 0x40);
        BitConverter.GetBytes((ushort)1).CopyTo(exe, 0x40 + 6);
        BitConverter.GetBytes((ushort)0xe0).CopyTo(exe, 0x40 + 20);
        BitConverter.GetBytes(ImageBase).CopyTo(exe, 0x40 + 24 + 28);
        var header = 0x40 + 24 + 0xe0;
        BitConverter.GetBytes(SectionStart).CopyTo(exe, header + 12);
        BitConverter.GetBytes(size).CopyTo(exe, header + 16);
        BitConverter.GetBytes(RawStart).CopyTo(exe, header + 20);
        fill(exe);

        return exe;
    }

    internal static int At(int address) => address - ImageBase - SectionStart + RawStart;

    // Every list holds one small flame at (0, 0), but list 2 holds a small flame at (-3, 5) and a large one at (4, -12), and
    // list 0 a large one at (1, -2). Types 7 and 8 point to one list.
    internal static void Lists(byte[] exe)
    {
        var header = At(FlameLists.Address);
        var common = 50;
        for (var list = 0; list < FlameLists.ListCount; list++)
        {
            BitConverter.GetBytes((short)common).CopyTo(exe, header + 2 * list);
        }

        exe[header + common + 3] = 0xff;
        BitConverter.GetBytes((short)60).CopyTo(exe, header);
        new byte[] { 8, 1, 0xfe, 0xff }.CopyTo(exe, header + 60);
        BitConverter.GetBytes((short)70).CopyTo(exe, header + 2 * 2);
        new byte[] { 0, 0xfd, 5, 8, 4, 0xf4, 0x80 }.CopyTo(exe, header + 70);
        BitConverter.GetBytes((short)80).CopyTo(exe, header + 2 * 7);
        BitConverter.GetBytes((short)80).CopyTo(exe, header + 2 * 8);
        new byte[] { 8, 2, 3, 0xff }.CopyTo(exe, header + 80);
    }

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
