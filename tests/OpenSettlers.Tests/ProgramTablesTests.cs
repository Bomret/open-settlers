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
    private const int SectionSize = 0x50000;
    private const int RawStart = 0x200;

    // A PE32 file with one section that holds the addresses 0x41e000 to 0x46e000 at the file offset 0x200, with a made-up
    // default transport priority order: the routine's instructions give good g the rank 27 - g.
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
        for (var good = 1; good <= StartStock.Goods; good++)
        {
            var at =
                FileOffset(ProgramTables.TransportPriorityAddress) + ProgramTables.TransportPriorityStep * (good - 1);
            exe[at] = ProgramTables.MovAlOpcode;
            exe[at + 1] = (byte)(StartStock.Goods + 1 - good);
        }

        WriteFigureRoutines(exe);
        fill(exe);

        return exe;
    }

    // The instructions of the draw routine and the woodcutter's handler that Read checks: the loads of the two tables,
    // the stub of job 5 that jumps to its handler, and the three adds to the figure word.
    private static void WriteFigureRoutines(byte[] exe)
    {
        void Write(int address, params byte[] code) => code.CopyTo(exe, FileOffset(address));

        void Load(int at, int table) => Write(at, [0x8d, 0x35, .. BitConverter.GetBytes(table)]);

        Load(ProgramTables.FigureBaseLoadAddress, ProgramTables.FigureBaseAddress);
        Load(ProgramTables.FigureOffsetLoadAddress, ProgramTables.FigureOffsetAddress);
        var stub = ProgramTables.JobStubAddress + ProgramTables.JobStubSize * Settler.WoodcutterJob;
        Write(
            stub,
            [0xe9, .. BitConverter.GetBytes(ProgramTables.WoodcutterHandlerAddress - (stub + 5))]
        );
        void Add(int at, int word) => Write(at, [0x66, 0x81, 0x47, 0x08, (byte)word, (byte)(word >> 8)]);

        Add(ProgramTables.WoodcutterWalkAddress, 0x0b00);
        Add(ProgramTables.WoodcutterCarryingAddress, 0x1000);
        Add(ProgramTables.WoodcutterCuttingAddress, 0x0e80);
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
        var exe = Executable(_ => { }, sectionAt: SectionStart + SectionSize);

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

    [TestMethod]
    public void TheCarriedGoodFiguresOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:43f4b8:54, the 27 words one for each good (inferred: the program's load names 0x43f4b7, see docs/rules/goods.md).
        CollectionAssert.AreEqual(
            new short[]
            {
                0x00,
                0x30,
                0x35,
                0x3B,
                0x41,
                0x46,
                0x4B,
                0x14,
                0x07,
                0x51,
                0x08,
                0x1C,
                0x1D,
                0x1E,
                0x1A,
                0x1B,
                0x68,
                0x6D,
                0x65,
                0x67,
                0x6B,
                0x6A,
                0x66,
                0x69,
                0x6C,
                0x57,
                0x56,
            },
            tables.CarriedFigures
        );
    }

    [TestMethod]
    public void TheCarriedGoodFiguresAreFoundByTheirAddress()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTables.CarriedFigureAddress) + 14] = 0x2a);

        Assert.AreEqual(0x2a, ProgramTables.Read(exe).CarriedFigures[7]);
    }

    [TestMethod]
    public void TheWalkRowsAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (var i = 0; i < ProgramTables.WalkRowCount * ProgramTables.WalkRowSize; i++)
            {
                bytes[FileOffset(ProgramTables.WalkRowAddress) + i] = (byte)(i * 7);
            }
        });

        var tables = ProgramTables.Read(exe);

        Assert.AreEqual(ProgramTables.WalkRowCount * ProgramTables.WalkRowSize, tables.WalkRows.Length);
        Assert.AreEqual((byte)(19 * 7), tables.WalkRows[19]);
    }

    [TestMethod]
    public void TheWalkRowsOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:4388ff:192 (24 rows of 8 bytes).
        Assert.AreEqual(0x6EFEA098351E0AF5UL, Fnv(tables.WalkRows));
    }

    [TestMethod]
    public void TheTablesOfTheRemovalAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (var good = 0; good < ProgramTables.GoodKindCount; good++)
            {
                bytes[FileOffset(ProgramTables.GoodKindAddress) + good] = (byte)(good + 3);
            }

            for (var length = 0; length < ProgramTables.RoadCapacityCount; length++)
            {
                bytes[FileOffset(ProgramTables.RoadCapacityAddress) + length] = (byte)(length * 2);
            }
        });

        var tables = ProgramTables.Read(exe);

        Assert.AreEqual((byte)(9 + 3), tables.GoodKinds[9]);
        Assert.AreEqual((byte)(5 * 2), tables.RoadCapacities[5]);
    }

    [TestMethod]
    public void TheTablesOfTheRemovalOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:462d6d:32 (a byte for each of 32 goods) and peek:462acd:8 (a count for each of 8 length classes).
        Assert.AreEqual(0x1C7E05E97572CBCBUL, Fnv(tables.GoodKinds));
        Assert.AreEqual(0x6FC87DA442D34B9DUL, Fnv(tables.RoadCapacities));
    }

    [TestMethod]
    public void TheTransportPrioritiesAreTheRanksTheDefaultOrderRoutineWrites()
    {
        var tables = ProgramTables.Read(Executable(_ => { }));

        Assert.AreEqual(26, tables.TransportPriorities[0], "good 1");
        Assert.AreEqual(1, tables.TransportPriorities[25], "good 26");
    }

    [TestMethod]
    public void ADefaultOrderRoutineWithoutItsInstructionsIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[FileOffset(ProgramTables.TransportPriorityAddress) + ProgramTables.TransportPriorityStep * 4] = 0x90
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read(exe));
        StringAssert.Contains(refusal.Message, "transport priority table");
    }

    [TestMethod]
    public void ADefaultOrderWhoseRanksAreNotOneTo26EachOnceIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[FileOffset(ProgramTables.TransportPriorityAddress) + ProgramTables.TransportPriorityStep * 4 + 1] = 26
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read(exe));
        StringAssert.Contains(refusal.Message, "transport priority table");
    }

    [TestMethod]
    public void TheTransportPrioritiesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // The bytes 1 to 26 of the prio= line of the goods and remove verbs, which run 0x445570 at the game's start.
        Assert.AreEqual(0xBBF1449BF71E9134UL, Fnv(tables.TransportPriorities));
    }

    [TestMethod]
    public void TheFigureTablesHoldTheWordsTheDrawRoutineReadsUnaligned()
    {
        var tables = TestProgram.Tables();

        // 0x44fff3 reads a pair of words at 4 times the high byte of the figure word in the bases (0x4501a1, odd) and at 4
        // times its low byte in the offsets (0x4503c5, odd): the woodcutter's rows 0x0b (walk), 0x10 (way back with the log),
        // and 0x0f (felling), the carrier's row 0, and the offsets of the figure bytes 9 and 0.
        CollectionAssert.AreEqual(new short[] { 0, 0 }, tables.FigureBases[..2]);
        CollectionAssert.AreEqual(new short[] { 48, 54 }, tables.FigureBases[(2 * 0x0b)..(2 * 0x0b + 2)]);
        CollectionAssert.AreEqual(new short[] { 272, -1 }, tables.FigureBases[(2 * 0x0f)..(2 * 0x0f + 2)]);
        CollectionAssert.AreEqual(new short[] { 48, 60 }, tables.FigureBases[(2 * 0x10)..(2 * 0x10 + 2)]);
        CollectionAssert.AreEqual(new short[] { 9, 1 }, tables.FigureOffsets[18..20]);
        CollectionAssert.AreEqual(new short[] { 0, 0 }, tables.FigureOffsets[..2]);
    }

    [TestMethod]
    public void TheWoodcuttersFigureWordsOfTheInstallAreThoseOfHisDrawHandler()
    {
        var adds = TestProgram.Tables().WoodcutterFigureAdds;

        // 0x43fb6b, the handler of job 5: add word [edi+8], imm16 at 0x43fc16 (walk), 0x43fc0b (way back with the log), 0x43fbe5 (figure byte from 0x80).
        Assert.AreEqual(new FigureAdds(0x0b00, 0x1000, 0x0e80), adds);
    }

    [TestMethod]
    public void TheWoodcuttersFigureWordsAreTheImmediatesOfHisHandler()
    {
        var tables = ProgramTables.Read(Executable(_ => { }));

        Assert.AreEqual(new FigureAdds(0x0b00, 0x1000, 0x0e80), tables.WoodcutterFigureAdds);
    }

    [TestMethod]
    public void AHandlerOtherThanTheJobsIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[FileOffset(ProgramTables.JobStubAddress + ProgramTables.JobStubSize * Settler.WoodcutterJob) + 1] ^= 1
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read(exe));
        StringAssert.Contains(refusal.Message, "figure");
    }

    [TestMethod]
    public void AHandlerWithoutItsAddInstructionIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTables.WoodcutterCarryingAddress)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read(exe));
        StringAssert.Contains(refusal.Message, "figure");
    }

    [TestMethod]
    public void ADrawRoutineThatLoadsOtherTablesIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTables.FigureOffsetLoadAddress) + 2] ^= 1);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTables.Read(exe));
        StringAssert.Contains(refusal.Message, "figure");
    }

    private static ulong Fnv(byte[] bytes) => bytes.Aggregate(FnvOffset, (hash, value) => (hash ^ value) * FnvPrime);
}
