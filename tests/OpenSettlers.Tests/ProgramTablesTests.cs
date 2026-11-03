using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The tables are found by address through the section headers of the executable. The synthetic executables are
// built here (INV-0001). The digests of the real tables are those of the emulator harness (ADR 0008):
// stockrows:x (the 5 rows of 26 bytes at 0x442e8a) and peek:41e640:128 (the class table), as the FNV-1a 64-bit
// digest of the bytes in order.
[TestClass]
public sealed class ProgramTablesTests
{
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;
    private const int ImageBase = 0x400000;
    private const int SectionStart = 0x1e000;
    private const int SectionSize = 0x40000;
    private const int RawStart = 0x200;

    // A PE32 file with one section that holds the addresses 0x41e000 to 0x44e000 at the file offset 0x200.
    private static byte[] Executable(Action<byte[]> fill, int sectionAt = SectionStart)
    {
        var exe = new byte[RawStart + SectionSize];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        BitConverter.GetBytes(0x40).CopyTo(exe, 0x3c);
        BitConverter.GetBytes(0x4550).CopyTo(exe, 0x40);
        BitConverter.GetBytes((ushort)1).CopyTo(exe, 0x40 + 6);
        BitConverter.GetBytes((ushort)0xe0).CopyTo(exe, 0x40 + 20);
        BitConverter.GetBytes(ImageBase).CopyTo(exe, 0x40 + 24 + 28);
        var header = 0x40 + 24 + 0xe0;
        BitConverter.GetBytes(sectionAt).CopyTo(exe, header + 12);
        BitConverter.GetBytes(SectionSize).CopyTo(exe, header + 16);
        BitConverter.GetBytes(RawStart).CopyTo(exe, header + 20);
        fill(exe);

        return exe;
    }

    private static int FileOffset(int address) => RawStart + address - ImageBase - SectionStart;

    [TestMethod]
    public void TheTablesAreFoundByTheirAddressInTheSection()
    {
        var exe = Executable(bytes =>
        {
            for (var code = 0; code < ProgramTables.ClassCount; code++)
            {
                bytes[FileOffset(ProgramTables.ClassTableAddress) + code] = (byte)(code * 3);
            }

            for (var i = 0; i < ProgramTables.StartStockRows * StartStock.Goods; i++)
            {
                bytes[FileOffset(ProgramTables.StartStockAddress) + i] = (byte)(i + 1);
            }
        });

        var tables = ProgramTables.Read(exe);

        Assert.AreEqual((byte)(17 * 3), tables.Classes[17]);
        Assert.AreEqual((byte)(2 * StartStock.Goods + 5 + 1), tables.StartStock[2][5]);
    }

    [TestMethod]
    public void AFileThatIsNoExecutableIsRefused()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read([1, 2, 3]));
    }

    [TestMethod]
    public void ATableOutsideEverySectionIsRefused()
    {
        var exe = Executable(_ => { }, sectionAt: SectionStart + 0x40000);

        Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read(exe));
    }

    [TestMethod]
    public void TablesOfTheWrongRowLengthsAreRefused()
    {
        var rows = Enumerable.Range(0, ProgramTables.StartStockRows).Select(_ => new byte[StartStock.Goods]).ToArray();
        rows[2] = new byte[StartStock.Goods - 1];

        Assert.ThrowsExactly<ArgumentException>(() => new ProgramTables(new byte[ProgramTables.ClassCount], rows));
        Assert.ThrowsExactly<ArgumentException>(() => new ProgramTables(new byte[ProgramTables.ClassCount - 1], rows));
    }

    [TestMethod]
    public void TheTablesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        Assert.AreEqual(0xAAD8621C6BD7A8D0UL, Fnv(tables.Classes));
        Assert.AreEqual(0x5495C7981E661835UL, Fnv(tables.StartStock.SelectMany(row => row).ToArray()));
    }

    [TestMethod]
    public void TheTablesOfTheWalkAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (
                var shape = ProgramTables.StepTimeFirst;
                shape < ProgramTables.StepTimeFirst + ProgramTables.StepTimeCount;
                shape++
            )
            {
                BitConverter
                    .GetBytes((ushort)(1000 + shape))
                    .CopyTo(bytes, FileOffset(ProgramTables.StepTimeAddress) + 2 * shape);
            }

            for (var type = 0; type < ProgramTables.DoorSpeedCount; type++)
            {
                bytes[FileOffset(ProgramTables.DoorSpeedAddress) + type] = (byte)(type + 7);
            }

            for (var size = 0; size < ProgramTables.ScanWindowCount; size++)
            {
                BitConverter
                    .GetBytes((ushort)(size * 5))
                    .CopyTo(bytes, FileOffset(ProgramTables.ScanWindowAddress) + 2 * size);
            }
        });

        var tables = ProgramTables.Read(exe);

        Assert.AreEqual((ushort)1013, tables.StepTime(13));
        Assert.AreEqual(
            (ushort)(1000 - 32),
            tables.StepTime(-32),
            "a step shape below the table reads the bytes before it"
        );
        Assert.AreEqual((ushort)0, tables.StepTime(500), "a shape outside the table reads as 0");
        Assert.AreEqual((byte)(24 + 7), tables.DoorSpeeds[24]);
        Assert.AreEqual((ushort)(3 * 5), tables.ScanWindows[3]);
    }

    [TestMethod]
    public void TheTablesOfTheWalkOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:43f208:320 (the 160 words from the step shape -32), peek:43c92b:64, and peek:421d28:16.
        Assert.AreEqual(0x05BF5D4714B4FBADUL, Fnv(tables.StepTimes.SelectMany(BitConverter.GetBytes).ToArray()));
        Assert.AreEqual(0x993304FEA7F369C8UL, Fnv(tables.DoorSpeeds));
        Assert.AreEqual(0x5AC52FC0C8AECD88UL, Fnv(tables.ScanWindows.SelectMany(BitConverter.GetBytes).ToArray()));
    }

    [TestMethod]
    public void TheFigureTablesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:4501a1:512 (256 words, 8 for each job) and peek:4503c5:1024 (512 words, 2 for each figure byte).
        Assert.AreEqual(0x25D56F6459E7D367UL, Fnv(tables.FigureBases.SelectMany(BitConverter.GetBytes).ToArray()));
        Assert.AreEqual(0x9074A7BD864B08BDUL, Fnv(tables.FigureOffsets.SelectMany(BitConverter.GetBytes).ToArray()));
    }

    private static ulong Fnv(byte[] bytes) => bytes.Aggregate(FnvOffset, (hash, value) => (hash ^ value) * FnvPrime);
}
