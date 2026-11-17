using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    const ulong FnvOffset = 0xcbf29ce484222325;
    const ulong FnvPrime = 0x100000001b3;
    const int ImageBase = 0x400000;
    const int SectionStart = 0x1e000;
    const int SectionSize = 0x260000;
    const int RawStart = 0x200;

    // A PE32 file with one section that holds the addresses 0x41e000 to 0x67e000 (the waiting carriers' offsets lie at 0x674923) at the file offset 0x200, with a made-up
    // default transport priority order: the routine's instructions give good g the rank 27 - g.
    static byte[] Executable(Action<byte[]> fill, int sectionAt = SectionStart)
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
                FileOffset(ProgramTablesImporter.TransportPriorityAddress)
                + ProgramTablesImporter.TransportPriorityStep * (good - 1);
            exe[at] = ProgramTablesImporter.MovAlOpcode;
            exe[at + 1] = (byte)(StartStock.Goods + 1 - good);
        }

        WriteFigureRoutines(exe);
        WriteConstructionTables(exe);
        WriteWorkerTables(exe);
        fill(exe);

        return exe;
    }

    // The tables of the materials that reach a building site, made up: the good class table (only the good 8 has a class), three orders
    // of the storage's hand-out of one step each that a word of -1 ends, and the player setup's routine that loads the word 0x9234 and
    // stores it at +0x148 of the player block.
    static void WriteConstructionTables(byte[] exe)
    {
        void Write(int address, params byte[] code) => code.CopyTo(exe, FileOffset(address));

        var classes = new byte[2 * ProgramTablesImporter.GoodSlots];
        Array.Fill(classes, (byte)0xff);
        classes[16] = 1;
        classes[17] = 0x42;
        Write(ProgramTablesImporter.GoodClassAddress, classes);
        for (var order = 0; order < ProgramTablesImporter.DistributionAddresses.Length; order++)
        {
            Write(
                ProgramTablesImporter.DistributionAddresses[order],
                [(byte)(order == 1 ? 0x44 : 0x42), 0, (byte)(order + 1), 0, (byte)(14 + 4 * order), 0, 0xff, 0xff]
            );
        }

        Write(
            ProgramTablesImporter.PlankRankAddress,
            [0x66, 0xb8, 0x34, 0x92, 0x8b, 0x5f, 0x30, 0x66, 0x89, 0x83, 0x48, 0x01, 0x00, 0x00]
        );
        Write(
            ProgramTablesImporter.MillRankAddress,
            [0x66, 0xb8, 0x56, 0x6d, 0x8b, 0x5f, 0x30, 0x66, 0x89, 0x83, 0x5a, 0x01, 0x00, 0x00]
        );
        Write(
            ProgramTablesImporter.PigRankAddress,
            [0x66, 0xb8, 0x57, 0x6e, 0x8b, 0x5f, 0x30, 0x66, 0x89, 0x83, 0x58, 0x01, 0x00, 0x00]
        );
    }

    // The handlers that ask for a worker, made up: each loads a job into [edi] and a tool word into [edi+0x18]; the builder's asks for
    // the job 7 and the tool word 0x30, the building of the type t for the job t + 10 and the tool word 2 * (t + 3) (none for the
    // forester). The builder's order of materials and steps of blows are made up: the order word of the type t has the bit t % 5 set,
    // and its steps are 0x100 * (2t + 1) and one more.
    static void WriteWorkerTables(byte[] exe)
    {
        void Write(int address, params byte[] code) => code.CopyTo(exe, FileOffset(address));

        void Request(int address, int job, int tool) =>
            Write(address, [0xb8, (byte)job, 0, 0, 0, 0x89, 0x07, 0xb8, (byte)tool, 0, 0, 0, 0x89, 0x47, 0x18]);

        Request(ProgramTablesImporter.BuilderRequestAddress, 7, 0x30);
        Request(ProgramTablesImporter.DiggerRequestAddress, 5, 0x24);
        foreach (var (type, address) in ProgramTablesImporter.WorkerRequestAddresses)
        {
            Request(address, type.Code + 10, type == BuildingType.Forester ? 0 : 2 * (type.Code + 3));
        }

        for (var type = 0; type < RuleTables.BuildingTypeCount; type++)
        {
            BitConverter
                .GetBytes((ushort)(1 << (type % 5)))
                .CopyTo(exe, FileOffset(ProgramTablesImporter.MaterialOrderAddress) + 2 * type);
            BitConverter
                .GetBytes((ushort)(0x100 * (2 * type + 1)))
                .CopyTo(exe, FileOffset(ProgramTablesImporter.BlowStepAddress) + 4 * type);
            BitConverter
                .GetBytes((ushort)(0x100 * (2 * type + 1) + 1))
                .CopyTo(exe, FileOffset(ProgramTablesImporter.BlowStepAddress) + 4 * type + 2);
        }
    }

    // The instructions of the draw routine and the woodcutter's handler that Read checks: the loads of the two tables,
    // the stub of job 5 that jumps to its handler, and the three adds to the figure word.
    static void WriteFigureRoutines(byte[] exe)
    {
        void Write(int address, params byte[] code) => code.CopyTo(exe, FileOffset(address));

        void Load(int at, int table) => Write(at, [0x8d, 0x35, .. BitConverter.GetBytes(table)]);

        Load(ProgramTablesImporter.FigureBaseLoadAddress, ProgramTablesImporter.FigureBaseAddress);
        Load(ProgramTablesImporter.FigureOffsetLoadAddress, ProgramTablesImporter.FigureOffsetAddress);
        var stub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Woodcutter.Number;
        Write(stub, [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.WoodcutterHandlerAddress - (stub + 5))]);
        void Add(int at, int word) => Write(at, [0x66, 0x81, 0x47, 0x08, (byte)word, (byte)(word >> 8)]);

        Add(ProgramTablesImporter.WoodcutterWalkAddress, 0x0b00);
        Add(ProgramTablesImporter.WoodcutterCarryingAddress, 0x1000);
        Add(ProgramTablesImporter.WoodcutterCuttingAddress, 0x0e80);
        var stonecutterStub =
            ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Stonecutter.Number;
        Write(
            stonecutterStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.StonecutterHandlerAddress - (stonecutterStub + 5))]
        );
        Add(ProgramTablesImporter.StonecutterWalkAddress, 0x0d00);
        Add(ProgramTablesImporter.StonecutterCarryingAddress, 0x1200);
        Add(ProgramTablesImporter.StonecutterStepBackAddress, 0x1200);
        Add(ProgramTablesImporter.StonecutterCuttingAddress, 0x1280);
        var hutStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Fisher.Number;
        Write(hutStub, [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.FisherHandlerAddress - (hutStub + 5))]);
        Add(ProgramTablesImporter.FisherWalkAddress, 0x2c00);
        Add(ProgramTablesImporter.FisherCarryingAddress, 0x2f00);
        Add(ProgramTablesImporter.FisherHighAddress, 0x2c80);
        Add(ProgramTablesImporter.FisherHighFacingAddress, 0x2d80);
        var builderStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Builder.Number;
        Write(
            builderStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.BuilderHandlerAddress - (builderStub + 5))]
        );
        Add(ProgramTablesImporter.BuilderWalkAddress, 0x1500);
        Add(ProgramTablesImporter.BuilderWorkAddress, 0x1580);
        var foresterStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Forester.Number;
        Write(
            foresterStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.ForesterHandlerAddress - (foresterStub + 5))]
        );
        Add(ProgramTablesImporter.ForesterWalkAddress, 0x0e00);
        Add(ProgramTablesImporter.ForesterWorkAddress, 0x1080);
        var farmerStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Farmer.Number;
        Write(
            farmerStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.FarmerHandlerAddress - (farmerStub + 5))]
        );
        Add(ProgramTablesImporter.FarmerWalkAddress, 0x3d00);
        Add(ProgramTablesImporter.FarmerCarryingAddress, 0x4000);
        Add(ProgramTablesImporter.FarmerSowAddress, 0x3d80);
        Add(ProgramTablesImporter.FarmerHarvestAddress, 0x3e80);
        var diggerStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Digger.Number;
        Write(
            diggerStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.DiggerHandlerAddress - (diggerStub + 5))]
        );
        Add(ProgramTablesImporter.DiggerWalkAddress, 0x1600);
        Add(ProgramTablesImporter.DiggerHighAddress, 0x1680);
        var sawmillerStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Sawmiller.Number;
        Write(
            sawmillerStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.SawmillerHandlerAddress - (sawmillerStub + 5))]
        );
        Add(ProgramTablesImporter.SawmillerWalkAddress, 0x1700);
        Add(ProgramTablesImporter.SawmillerCarryingAddress, 0x1780);
        Add(ProgramTablesImporter.SawmillerSawAddress, 0x1800);
        var millerStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Miller.Number;
        Write(
            millerStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.MillerHandlerAddress - (millerStub + 5))]
        );
        Add(ProgramTablesImporter.MillerWalkAddress, 0x1900);
        Add(ProgramTablesImporter.MillerCarryingAddress, 0x1980);
        Add(ProgramTablesImporter.MillerGrindAddress, 0x1a00);
        var bakerStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Baker.Number;
        Write(bakerStub, [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.BakerHandlerAddress - (bakerStub + 5))]);
        Add(ProgramTablesImporter.BakerWalkAddress, 0x1b00);
        Add(ProgramTablesImporter.BakerCarryingAddress, 0x1b80);
        Add(ProgramTablesImporter.BakerBakeAddress, 0x1c00);
        var butcherStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Butcher.Number;
        Write(
            butcherStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.ButcherHandlerAddress - (butcherStub + 5))]
        );
        Add(ProgramTablesImporter.ButcherWalkAddress, 0x1b00);
        Add(ProgramTablesImporter.ButcherCarryingAddress, 0x1b80);
        Add(ProgramTablesImporter.ButcherWorkAddress, 0x1c00);
        var pigFarmerStub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * PigFarmer.Number;
        Write(
            pigFarmerStub,
            [0xe9, .. BitConverter.GetBytes(ProgramTablesImporter.PigFarmerHandlerAddress - (pigFarmerStub + 5))]
        );
        Add(ProgramTablesImporter.PigFarmerWalkAddress, 0x1b00);
        Add(ProgramTablesImporter.PigFarmerCarryingAddress, 0x1b80);
        Add(ProgramTablesImporter.PigFarmerWorkAddress, 0x1c00);
        Write(ProgramTablesImporter.PigGrowthAddress, [.. Enumerable.Range(0, 16).Select(i => (byte)(i * 3))]);
    }

    static int FileOffset(int address) => RawStart + address - ImageBase - SectionStart;

    [TestMethod]
    public void TheTablesAreFoundByTheirAddressInTheSection()
    {
        var exe = Executable(bytes =>
        {
            for (var code = 0; code < RuleTables.ClassCount; code++)
            {
                bytes[FileOffset(ProgramTablesImporter.ClassTableAddress) + code] = (byte)(code * 3);
            }

            for (var i = 0; i < RuleTables.StartStockRows * StartStock.Goods; i++)
            {
                bytes[FileOffset(ProgramTablesImporter.StartStockAddress) + i] = (byte)(i + 1);
            }
        });

        var tables = ProgramTablesImporter.Import(exe);

        Assert.AreEqual(new ObjectClass { Value = 17 * 3 }, tables.ClassOf(new MapObject { Code = 17 }));
        Assert.AreEqual((byte)(2 * StartStock.Goods + 5 + 1), tables.StartStock[2][5]);
    }

    [TestMethod]
    public void AFileThatIsNoExecutableIsRefused()
    {
        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import([1, 2, 3]));
        Assert.AreEqual("Siedler1.exe is truncated.", refusal.Message);
    }

    [TestMethod]
    public void AnExecutableCutShortBeforeItsSectionsIsRefusedAsTruncated()
    {
        var exe = Executable(_ => { })[..0x60];

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        Assert.AreEqual("Siedler1.exe is truncated.", refusal.Message);
    }

    [TestMethod]
    public void ATableOutsideEverySectionIsRefused()
    {
        var exe = Executable(_ => { }, sectionAt: SectionStart + SectionSize);

        Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
    }

    [TestMethod]
    public void TablesOfTheWrongRowLengthsAreRefused()
    {
        var rows = Enumerable.Range(0, RuleTables.StartStockRows).Select(_ => new byte[StartStock.Goods]).ToArray();
        rows[2] = new byte[StartStock.Goods - 1];

        Assert.ThrowsExactly<ArgumentException>(() => new RuleTables(new ObjectClass[RuleTables.ClassCount], rows));
        Assert.ThrowsExactly<ArgumentException>(() => new RuleTables(new ObjectClass[RuleTables.ClassCount - 1], rows));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheTablesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        Assert.AreEqual(0xAAD8621C6BD7A8D0UL, Fnv([.. tables.Classes.Select(c => (byte)c.Value)]));
        Assert.AreEqual(0x5495C7981E661835UL, Fnv([.. tables.StartStock.SelectMany(row => row)]));
    }

    [TestMethod]
    public void TheTablesOfTheWalkAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (
                var shape = RuleTables.StepTimeFirst;
                shape < RuleTables.StepTimeFirst + RuleTables.StepTimeCount;
                shape++
            )
            {
                BitConverter
                    .GetBytes((ushort)(1000 + shape))
                    .CopyTo(bytes, FileOffset(ProgramTablesImporter.StepTimeAddress) + 2 * shape);
            }

            for (var type = 0; type < RuleTables.DoorSpeedCount; type++)
            {
                bytes[FileOffset(ProgramTablesImporter.DoorSpeedAddress) + type] = (byte)(type + 7);
            }

            for (var size = 0; size < RuleTables.ScanWindowCount; size++)
            {
                BitConverter
                    .GetBytes((ushort)(size * 5))
                    .CopyTo(bytes, FileOffset(ProgramTablesImporter.ScanWindowAddress) + 2 * size);
            }
        });

        var tables = ProgramTablesImporter.Import(exe);

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
    [TestCategory(Needs.Install)]
    public void TheTablesOfTheWalkOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:43f208:320 (the 160 words from the step shape -32), peek:43c92b:64, and peek:421d28:16.
        Assert.AreEqual(0x05BF5D4714B4FBADUL, Fnv([.. tables.StepTimes.SelectMany(BitConverter.GetBytes)]));
        Assert.AreEqual(0x993304FEA7F369C8UL, Fnv(tables.DoorSpeeds));
        Assert.AreEqual(0x5AC52FC0C8AECD88UL, Fnv([.. tables.ScanWindows.SelectMany(BitConverter.GetBytes)]));
    }

    [TestMethod]
    public void TheSizeAndNeedsTablesOfTheBuildingTypesAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (var type = 0; type < RuleTables.BuildingTypeCount; type++)
            {
                bytes[FileOffset(ProgramTablesImporter.BuildingSizeAddress) + type] = (byte)(type % 3);
                bytes[FileOffset(ProgramTablesImporter.BuildingNeedsAddress) + 2 * type] = (byte)(type + 1);
                bytes[FileOffset(ProgramTablesImporter.BuildingNeedsAddress) + 2 * type + 1] = (byte)(type + 2);
            }
        });

        var tables = ProgramTablesImporter.Import(exe);

        Assert.AreEqual(BuildingSize.Large, tables.SizeOf(new BuildingType { Code = 5 }));
        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(new BuildingType { Code = 7 }));
        Assert.AreEqual(new BuildingNeeds { Planks = 9, Stones = 10 }, tables.NeedsOf(new BuildingType { Code = 8 }));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheBuildingSizeAndNeedsTablesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:449f18:48 (planks and stones of the types 0 to 0x17) and peek:42d307:24 (their sizes).
        Assert.AreEqual(0x6649B1F9CA0A7982UL, Fnv(tables.BuildingNeedsTable));
        Assert.AreEqual(0xE5A6690EA07D4A89UL, Fnv(tables.BuildingSizes));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFourTypesOfTheSlicesTakeTheSizeAndNeedsOfTheOriginal()
    {
        var tables = TestProgram.Tables();

        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(BuildingType.Woodcutter));
        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(BuildingType.Stonecutter));
        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(BuildingType.Forester));
        Assert.AreEqual(BuildingSize.Large, tables.SizeOf(BuildingType.Sawmill));
        Assert.AreEqual(new BuildingNeeds { Planks = 2, Stones = 0 }, tables.NeedsOf(BuildingType.Woodcutter));
        Assert.AreEqual(new BuildingNeeds { Planks = 2, Stones = 0 }, tables.NeedsOf(BuildingType.Stonecutter));
        Assert.AreEqual(new BuildingNeeds { Planks = 2, Stones = 0 }, tables.NeedsOf(BuildingType.Forester));
        Assert.AreEqual(new BuildingNeeds { Planks = 3, Stones = 2 }, tables.NeedsOf(BuildingType.Sawmill));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFigureTablesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:4501a1:548 (274 words, 2 for each row 0 to 0x88) and peek:4503c5:1024 (512 words, 2 for each figure byte).
        Assert.AreEqual(0x7A4340847303DFB6UL, Fnv([.. tables.Figures.Bases.SelectMany(BitConverter.GetBytes)]));
        Assert.AreEqual(0x9074A7BD864B08BDUL, Fnv([.. tables.Figures.Offsets.SelectMany(BitConverter.GetBytes)]));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheCarriedGoodFiguresOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // The 27 words one for each good from 0x43f4b7 (the handler 0x43f8ae loads 0x43f4b7 and adds 2 * good, docs/rules/goods.md):
        // each word is the row of the figure bases in its high byte.
        CollectionAssert.AreEqual(
            new short[]
            {
                0x0000,
                0x3000,
                0x3500,
                0x3B00,
                0x4100,
                0x4600,
                0x4B00,
                0x1400,
                0x0700,
                0x5100,
                0x0800,
                0x1C00,
                0x1D00,
                0x1E00,
                0x1A00,
                0x1B00,
                0x6800,
                0x6D00,
                0x6500,
                0x6700,
                0x6B00,
                0x6A00,
                0x6600,
                0x6900,
                0x6C00,
                0x5700,
                0x5600,
            },
            tables.Figures.Carried.ToArray()
        );
    }

    [TestMethod]
    public void TheCarriedGoodFiguresAreFoundByTheirAddress()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.CarriedFigureAddress) + 15] = 0x2a);

        Assert.AreEqual(
            0x2a00,
            ProgramTablesImporter.Import(exe).Figures.Carried[7],
            "the word of good 7 at the address plus 2 * 7"
        );
    }

    [TestMethod]
    public void TheWalkRowsAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (var i = 0; i < RuleTables.WalkRowCount * RuleTables.WalkRowSize; i++)
            {
                bytes[FileOffset(ProgramTablesImporter.WalkRowAddress) + i] = (byte)(i * 7);
            }
        });

        var tables = ProgramTablesImporter.Import(exe);

        Assert.AreEqual(RuleTables.WalkRowCount * RuleTables.WalkRowSize, tables.WalkRows.Length);
        Assert.AreEqual((byte)(19 * 7), tables.WalkRows[19]);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheWalkRowsOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:4388ff:192 (24 rows of 8 bytes).
        Assert.AreEqual(0x6EFEA098351E0AF5UL, Fnv(tables.WalkRows));
    }

    [TestMethod]
    public void TheFrameRowsAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            bytes[FileOffset(ProgramTablesImporter.FrameRowAddress) + 10] = 0xff;
            bytes[FileOffset(ProgramTablesImporter.FrameRowAddress) + 11] = 2;
        });

        var tables = ProgramTablesImporter.Import(exe);

        Assert.AreEqual(RuleTables.FrameRowCount, tables.Figures.FrameRows.Length);
        Assert.AreEqual(-1, tables.Figures.FrameRow(10), "a signed byte for the frame 10");
        Assert.AreEqual(2, tables.Figures.FrameRow(11));
#pragma warning disable MSTEST0032 // The assertion pins the constant to its value.
        Assert.AreEqual(0x43f402, ProgramTablesImporter.FrameRowAddress);
#pragma warning restore MSTEST0032
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFrameRowsOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:43f402:181 (the bytes up to the carried goods' table at 0x43f4b7).
        Assert.AreEqual(0x02AE63A4FDE36C2DUL, Fnv([.. tables.Figures.FrameRows.Select(row => (byte)row)]));
    }

    [TestMethod]
    public void TheTablesOfTheRemovalAreFoundByTheirAddress()
    {
        var exe = Executable(bytes =>
        {
            for (var good = 0; good < ProgramTablesImporter.GoodSlots; good++)
            {
                bytes[FileOffset(ProgramTablesImporter.GoodKindAddress) + good] = (byte)(good % 3);
            }

            for (var length = 0; length < RuleTables.RoadCapacityCount; length++)
            {
                bytes[FileOffset(ProgramTablesImporter.RoadCapacityAddress) + length] = (byte)(length * 2);
            }
        });

        var tables = ProgramTablesImporter.Import(exe);

        // The kind byte of good g is g % 3: 0 counts low, 1 high, 2 in none.
        Assert.AreEqual(FillCounter.Low, tables.FillCounterOf(new Good { Code = 9 }));
        Assert.AreEqual(FillCounter.High, tables.FillCounterOf(new Good { Code = 10 }));
        Assert.AreEqual(FillCounter.None, tables.FillCounterOf(new Good { Code = 11 }));
        Assert.AreEqual((byte)(5 * 2), tables.RoadCapacities[5]);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheTablesOfTheRemovalOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();
        var kinds = ProgramTablesImporter.GoodKindBytes(TestProgram.OpenInstall().ReadAll(Install.Program));

        // peek:462d6d:32 (a byte for each of 32 goods) and peek:462acd:8 (a count for each of 8 length classes).
        Assert.AreEqual(0x1C7E05E97572CBCBUL, Fnv(kinds));
        Assert.AreEqual(0x6FC87DA442D34B9DUL, Fnv(tables.RoadCapacities));
        foreach (var entry in tables.Catalog.Goods)
        {
            var expected = kinds[entry.OriginalCode!.Value] switch
            {
                0 => FillCounter.Low,
                1 => FillCounter.High,
                _ => FillCounter.None,
            };
            Assert.AreEqual(expected, entry.Counter, $"the counter of {entry.Name}");
        }
    }

    [TestMethod]
    public void TheGoodClassesAndTheHandoutOrdersAreReadFromTheirAddresses()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(new GoodClass { Class = 1, Word = RequestWord.First }, tables.ClassOfGood(Good.Plank));
        Assert.IsNull(tables.ClassOfGood(Good.Stone), "a class byte of 0xff sends the good to storage at once");
        // The synthetic orders hold one step each: order 0 the class 1 of the first word and the good 8 (stock offset 14), order 1
        // the class 2 of the second word and the good 10 (offset 18), order 2 the class 3 of the first word and the good 12 (offset 22).
        CollectionAssert.AreEqual(
            new[] { "First 1: 8" },
            Steps(tables.DistributionOrder(2)),
            "any other draw picks the first order"
        );
        CollectionAssert.AreEqual(
            new[] { "Second 2: 10" },
            Steps(tables.DistributionOrder(0)),
            "the draw 0 picks the second order"
        );
        CollectionAssert.AreEqual(
            new[] { "First 3: 12" },
            Steps(tables.DistributionOrder(1)),
            "the draw 1 picks the third"
        );
        CollectionAssert.AreEqual(
            Steps(tables.DistributionOrder(0)),
            Steps(tables.DistributionOrder(8)),
            "the draw is taken to 0 to 7"
        );
    }

    // A step as text: its request word, its class, and the goods it hands out.
    static string[] Steps(IEnumerable<DistributionStep> order) =>
        [.. order.Select(step => $"{step.Class.Word} {step.Class.Class}: {string.Join(',', step.Goods)}")];

    [TestMethod]
    public void AFoodStepOfTheTableNamesTheThreeFoodGoods()
    {
        var exe = Executable(bytes =>
        {
            bytes[FileOffset(ProgramTablesImporter.DistributionAddresses[0]) + 4] = 0xff;
            bytes[FileOffset(ProgramTablesImporter.DistributionAddresses[0]) + 5] = 0xff;
        });

        var tables = ProgramTablesImporter.Import(exe);

        CollectionAssert.AreEqual(new[] { "First 1: 1,3,6" }, Steps(tables.DistributionOrder(2)));
    }

    [TestMethod]
    public void AnOrderWhoseStepNamesNoRequestWordIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.DistributionAddresses[0])] = 0x43);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "hand-out");
    }

    [TestMethod]
    public void ThePlankRankStartIsTheHighByteOfTheWordThePlayerSetupStores()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(0x92, tables.PlankRankBase);
    }

    [TestMethod]
    public void TheMillRankStartIsTheHighByteOfTheWordThePlayerSetupStoresAtTheMillsOffset()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(0x6d, tables.MillRankBase);
    }

    [TestMethod]
    public void APlayerSetupThatDoesNotStoreItsWordAtTheMillsOffsetIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.MillRankAddress) + 10] = 0x58);

        Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
    }

    [TestMethod]
    public void ThePigRankStartAndTheGrowthTableAreReadFromThePlayerSetupAndTheTableAtTheEndOfTheWork()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        var rates = tables.Get<PigFarmRates>(new PigFarmer());
        Assert.AreEqual(0x6e, rates.RankBase, "the high byte of the word stored at the pig farm's offset");
        CollectionAssert.AreEqual(
            new ushort[] { 0x0300, 0x0906, 0x0f0c, 0x1512, 0x1b18, 0x211e, 0x2724, 0x2d2a },
            rates.Growth.ToArray(),
            "the eight words of the table"
        );
    }

    [TestMethod]
    public void APlayerSetupThatDoesNotStoreItsWordAtThePigFarmsOffsetIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.PigRankAddress) + 10] = 0x5a);

        Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
    }

    [TestMethod]
    public void TheJobAndToolEachBuildingAsksForAreReadFromItsHandler()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(7),
                Tool = new Good { Code = 0x18 },
            },
            tables.Get<WorkerRequest>(new Builder())
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(12),
                Tool = new Good { Code = 5 },
            },
            tables.WorkerOf(BuildingType.Woodcutter)
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(14),
                Tool = new Good { Code = 7 },
            },
            tables.WorkerOf(BuildingType.Stonecutter)
        );
        Assert.AreEqual(
            new WorkerRequest { Job = Job.Of(19), Tool = Good.None },
            tables.WorkerOf(BuildingType.Forester)
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(27),
                Tool = new Good { Code = 20 },
            },
            tables.WorkerOf(BuildingType.Sawmill)
        );
        Assert.IsNull(tables.WorkerOf(BuildingType.Castle), "the castle's handler asks for no worker of this kind");
    }

    [TestMethod]
    public void EachFoodBuildingAsksForTheJobAndToolItsHandlerLoads()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        foreach (
            var type in new[]
            {
                BuildingType.Fisher,
                BuildingType.Farm,
                BuildingType.Baker,
                BuildingType.Mill,
                BuildingType.PigFarm,
                BuildingType.Butcher,
            }
        )
        {
            Assert.AreEqual(
                new WorkerRequest
                {
                    Job = Job.Of(type.Code + 10),
                    Tool = new Good { Code = type.Code + 3 },
                },
                tables.WorkerOf(type),
                $"type {type.Code:x}"
            );
        }
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFoodBuildingsAskForTheWorkersOfTheOriginalsHandlers()
    {
        var tables = TestProgram.Tables();

        (BuildingType Type, int Job, int Tool, BuildingSize Size, int Planks, int Stones)[] expected =
        [
            (BuildingType.Fisher, 11, 18, BuildingSize.Small, 2, 0),
            (BuildingType.Farm, 14, 20, BuildingSize.Large, 4, 1),
            (BuildingType.Butcher, 13, 19, BuildingSize.Large, 2, 1),
            (BuildingType.PigFarm, 12, 0, BuildingSize.Large, 4, 1),
            (BuildingType.Mill, 15, 0, BuildingSize.Small, 3, 1),
            (BuildingType.Baker, 16, 0, BuildingSize.Large, 2, 1),
        ];
        foreach (var (type, job, tool, size, planks, stones) in expected)
        {
            Assert.AreEqual(
                new WorkerRequest
                {
                    Job = Job.Of(job),
                    Tool = new Good { Code = tool },
                },
                tables.WorkerOf(type),
                $"type {type.Code:x}"
            );
            Assert.AreEqual(size, tables.SizeOf(type), $"type {type.Code:x} size");
            Assert.AreEqual(new BuildingNeeds { Planks = planks, Stones = stones }, tables.NeedsOf(type));
        }
    }

    [TestMethod]
    public void AHandlerThatDoesNotLoadItsJobIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.BuilderRequestAddress)] = 0x31);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "worker");
    }

    [TestMethod]
    public void TheBuildersOrderOfMaterialsAndStepsOfBlowsAreReadForEachType()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        // The made-up order word of type t has the bit t % 5 set: a set bit means a stone.
        Assert.IsTrue(tables.StoneAt(BuildingType.Woodcutter, 2));
        Assert.IsFalse(tables.StoneAt(BuildingType.Woodcutter, 0));
        Assert.IsTrue(tables.StoneAt(BuildingType.Forester, 4));
        // The made-up steps of type t are 0x100 * (2t + 1) before the progress word's top bit is set and one more after.
        Assert.AreEqual(0x500, tables.BlowStep(BuildingType.Woodcutter, late: false));
        Assert.AreEqual(0x501, tables.BlowStep(BuildingType.Woodcutter, late: true));
        Assert.AreEqual(0x1300, tables.BlowStep(BuildingType.Forester, late: false));
    }

    [TestMethod]
    public void TheBuildersFigureAddsAreReadFromItsHandlerAndItsStubIsChecked()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));
        Assert.AreEqual(
            new BuilderFigureAdds { Walk = 0x1500, Work = 0x1580 },
            tables.Get<BuilderFigureAdds>(new Builder())
        );

        var stub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Builder.Number;
        var exe = Executable(bytes => bytes[FileOffset(stub)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "builder");
    }

    [TestMethod]
    public void TheDiggersRequestAndFigureAddsAreReadFromTheHandlersAndHisStubIsChecked()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        // The made-up handler loads the job 5 and the tool word 0x24: the request is what the handler says, not the digger's own numbers.
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(5),
                Tool = new Good { Code = 18 },
            },
            tables.Get<WorkerRequest>(new Digger())
        );
        Assert.AreEqual(
            new FigureAdds
            {
                Walk = 0x1600,
                Carrying = 0x1600,
                Cutting = 0x1680,
            },
            tables.Get<FigureAdds>(new Digger())
        );

        var stub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Digger.Number;
        var exe = Executable(bytes => bytes[FileOffset(stub)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "digger");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheDiggersRequestAndFigureAddsOfTheInstallMatchTheEmulator()
    {
        // peek:42d52a:15 (mov eax, 2; mov [edi], eax; mov eax, 0x20; mov [edi+0x18], eax: the tool word halved is the good 16), and
        // peek:43fa87:100 (the handler: add word [edi+8], 0x380 at 0x43fade and 0x300 at 0x43fae9).
        var tables = TestProgram.Tables();

        Assert.AreEqual(
            new WorkerRequest
            {
                Job = new Digger(),
                Tool = new Good { Code = 16 },
            },
            tables.Get<WorkerRequest>(new Digger())
        );
        Assert.AreEqual(0x300, tables.Get<FigureAdds>(new Digger()).Walk);
        Assert.AreEqual(0x380, tables.Get<FigureAdds>(new Digger()).Cutting);
    }

    [TestMethod]
    public void TheSawmillersFigureAddsAreReadFromHisHandlerAndHisStubIsChecked()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new SawmillerFigureAdds
            {
                Walk = 0x1700,
                Carrying = 0x1780,
                Saw = 0x1800,
            },
            tables.Get<SawmillerFigureAdds>(new Sawmiller())
        );

        var stub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Sawmiller.Number;
        var exe = Executable(bytes => bytes[FileOffset(stub)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "sawmiller");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheSawmillersFigureAddsOfTheInstallMatchTheProgram()
    {
        // The handler of the stub of job 6 (0x43fc21, found by objdump of the program's instructions): add word [edi+8], 0x1580 at 0x43fca5
        // for a figure byte from 0x80, 0x1700 at 0x43fcc2 for the state 5 that lays the plank down next (byte 15 is 0xd), and 0xc00 at 0x43fccd
        // for the walk; the draw run of the recording (settler-draws.txt, sawmiller) draws with them.
        Assert.AreEqual(
            new SawmillerFigureAdds
            {
                Walk = 0xc00,
                Carrying = 0x1700,
                Saw = 0x1580,
            },
            TestProgram.Tables().Get<SawmillerFigureAdds>(new Sawmiller())
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheMillersFigureAddsAndTheMillsRankStartOfTheInstallMatchTheProgram()
    {
        // The handler of the stub of job 15 (0x4400d3, read from the program's instructions): add word [edi+8], 0x4380 at 0x4400df for a
        // figure byte from 0x80, 0x4500 at 0x4400fc for the state 5 that lays the flour down next (byte 15 is 0xd), and 0x4300 at 0x440107
        // for the walk; the draw run of the recording (settler-draws.txt, miller) draws with them. The player setup 0x44579c stores
        // 0x7fee at +0x15a, whose high byte the mill's handler shifts for the rank of its request for grain.
        var tables = TestProgram.Tables();
        Assert.AreEqual(
            new FoodHutFigureAdds
            {
                Walk = 0x4300,
                Carrying = 0x4500,
                Work = 0x4380,
            },
            tables.Get<FoodHutFigureAdds>(new Miller())
        );
        Assert.AreEqual(0x7f, tables.MillRankBase);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheBakersFigureAddsOfTheInstallMatchTheProgram()
    {
        // The handler of the stub of job 16 (0x440112, read from the program's instructions): add word [edi+8], 0x4880 at 0x44011e for a
        // figure byte from 0x80, 0x4a00 at 0x44013b for the state 5 that lays the bread down next (byte 15 is 0xd), and 0x4800 at 0x440146
        // for the walk; the draw run of the recording (settler-draws.txt, baker) draws with them.
        Assert.AreEqual(
            new FoodHutFigureAdds
            {
                Walk = 0x4800,
                Carrying = 0x4a00,
                Work = 0x4880,
            },
            TestProgram.Tables().Get<FoodHutFigureAdds>(new Baker())
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ThePigFarmersFigureAddsRankStartAndGrowthTableOfTheInstallMatchTheProgram()
    {
        // The handler of the stub of job 12 (0x43ff56, read from the program's instructions): add word [edi+8], 0x3280 at 0x43ff5d for a
        // figure byte from 0x80, 0x3400 at 0x43ff7a for the state 5 that lays the pig down next (byte 15 is 0xd), and 0x3200 at 0x43ff85
        // for the walk. The player setup 0x44579c stores 0xffdc at +0x158, whose high byte the pig farm's handler shifts for the rank of its
        // request for grain. The table at 0x433684 (peek:433684:16) holds eight words, the draw below which a pig grows at each held count.
        var tables = TestProgram.Tables();
        Assert.AreEqual(
            new FoodHutFigureAdds
            {
                Walk = 0x3200,
                Carrying = 0x3400,
                Work = 0x3280,
            },
            tables.Get<FoodHutFigureAdds>(new PigFarmer())
        );
        var rates = tables.Get<PigFarmRates>(new PigFarmer());
        Assert.AreEqual(0xff, rates.RankBase);
        CollectionAssert.AreEqual(
            new ushort[] { 0, 6000, 8000, 10000, 11000, 12000, 13000, 14000 },
            rates.Growth.ToArray()
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheButchersFigureAddsOfTheInstallMatchTheProgram()
    {
        // The handler of the stub of job 13 (0x43ff90, read from the program's instructions): add word [edi+8], 0x3780 at 0x43fff8 for a
        // figure byte from 0x80, 0x3a00 at 0x440015 for the state 5 that lays the meat down next (byte 15 is 0xd), and 0x3700 at 0x440020
        // for the walk; the draw run of the recording (settler-draws.txt, butcher) draws with them.
        Assert.AreEqual(
            new FoodHutFigureAdds
            {
                Walk = 0x3700,
                Carrying = 0x3a00,
                Work = 0x3780,
            },
            TestProgram.Tables().Get<FoodHutFigureAdds>(new Butcher())
        );
    }

    [TestMethod]
    public void TheBakersFigureAddsAreReadFromHisHandlerAndHisStubIsChecked()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new FoodHutFigureAdds
            {
                Walk = 0x1b00,
                Carrying = 0x1b80,
                Work = 0x1c00,
            },
            tables.Get<FoodHutFigureAdds>(new Baker())
        );

        var stub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Baker.Number;
        var exe = Executable(bytes => bytes[FileOffset(stub)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "baker");
    }

    [TestMethod]
    public void TheButchersFigureAddsAreReadFromHisHandlerAndHisStubIsChecked()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new FoodHutFigureAdds
            {
                Walk = 0x1b00,
                Carrying = 0x1b80,
                Work = 0x1c00,
            },
            tables.Get<FoodHutFigureAdds>(new Butcher())
        );

        var stub = ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Butcher.Number;
        var exe = Executable(bytes => bytes[FileOffset(stub)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "butcher");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFillByteOfEveryGoodWithAClassIsTheByteOfTheRequestWordThatAsksForIt()
    {
        // peek:462d6d:32 (the kind table) and peek:464117:64 (the class table): one rule counts a good on its way, back, and in.
        var tables = TestProgram.Tables();
        for (var code = 1; code < RuleTables.Goods; code++)
        {
            var good = new Good { Code = code };
            var expected = tables.ClassOfGood(good) is { } asked
                ? (asked.Word == RequestWord.First ? FillCounter.Low : FillCounter.High)
                : FillCounter.None;
            Assert.AreEqual(expected, tables.FillCounterOf(good), $"the fill byte of the good {code}");
        }
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheBuildersFigureAddsOfTheInstallMatchTheEmulator()
    {
        // peek:43faf4:120 (the handler: add word [edi+8], 0x580 at 0x43fb55 and 0x500 at 0x43fb60).
        Assert.AreEqual(
            new BuilderFigureAdds { Walk = 0x500, Work = 0x580 },
            TestProgram.Tables().Get<BuilderFigureAdds>(new Builder())
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheWorkerRequestsAndTheBuildersTablesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:42d5fc:15, peek:42d952:15, peek:42d994:15, peek:42d9d6:15, peek:42da18:15 (the handlers' job and tool words; the tool
        // word halved is the good whose stock word lies at 4 plus the word in the storage item), peek:43f0ba:48 (the order words), and
        // peek:43f0fa:96 (the steps).
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = new Builder(),
                Tool = new Good { Code = 17 },
            },
            tables.Get<WorkerRequest>(new Builder())
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = new Woodcutter(),
                Tool = new Good { Code = 21 },
            },
            tables.WorkerOf(BuildingType.Woodcutter)
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(7),
                Tool = new Good { Code = 23 },
            },
            tables.WorkerOf(BuildingType.Stonecutter)
        );
        Assert.AreEqual(
            new WorkerRequest { Job = Job.Of(8), Tool = Good.None },
            tables.WorkerOf(BuildingType.Forester)
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = Job.Of(6),
                Tool = new Good { Code = 22 },
            },
            tables.WorkerOf(BuildingType.Sawmill)
        );
        Assert.IsFalse(tables.StoneAt(BuildingType.Woodcutter, 0));
        Assert.IsFalse(tables.StoneAt(BuildingType.Sawmill, 0));
        Assert.IsFalse(tables.StoneAt(BuildingType.Sawmill, 1));
        Assert.IsTrue(tables.StoneAt(BuildingType.Sawmill, 2));
        Assert.IsTrue(tables.StoneAt(BuildingType.Sawmill, 3));
        Assert.AreEqual(0x1000, tables.BlowStep(BuildingType.Forester, late: false));
        Assert.AreEqual(0x1000, tables.BlowStep(BuildingType.Forester, late: true));
        Assert.AreEqual(0x0800, tables.BlowStep(BuildingType.Sawmill, late: false));
        Assert.AreEqual(0x0556, tables.BlowStep(BuildingType.Sawmill, late: true));
    }

    [TestMethod]
    public void APlayerSetupThatDoesNotStoreItsWordAtTheRankOffsetIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.PlankRankAddress) + 10] = 0x4a);

        Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheGoodClassesHandoutOrdersAndPlankRankStartOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // peek:464117:64, peek:42a313:150 (the three orders of 12 steps), and peek:445752:16 (mov ax, 0xffdc, stored at +0x148).
        Assert.AreEqual(new GoodClass { Class = 1, Word = RequestWord.First }, tables.ClassOfGood(Good.Plank));
        Assert.AreEqual(new GoodClass { Class = 4, Word = RequestWord.Second }, tables.ClassOfGood(Good.Stone));
        var first = tables.DistributionOrder(2);
        var second = tables.DistributionOrder(0);
        var third = tables.DistributionOrder(1);
        CollectionAssert.AreEqual(new[] { 12, 12, 12 }, new[] { first.Length, second.Length, third.Length });
        CollectionAssert.AreEqual(new[] { "First 1: 8", "Second 4: 10" }, Steps(first.Take(2)));
        CollectionAssert.AreEqual(new[] { "Second 4: 10" }, Steps(second.Take(1)));
        CollectionAssert.AreEqual(new[] { "First 0: 1,3,6" }, Steps(third.Take(1)));
        Assert.AreEqual(0xff, tables.PlankRankBase);
        // The whole class table (the class and request word of each of the 32 goods, the log's included) and all 36 steps of the three
        // orders, as text (the draws 0, 1, and 2 in turn): the digest is of that text built from the same peeks, decoded as the reader does.
        var table = ProgramTablesImporter.GoodClassTable(TestProgram.OpenInstall().ReadAll(Install.Program));
        var classes = table.Select(c => c is { } asked ? $"{asked.Word} {asked.Class}" : "storage");
        foreach (var entry in tables.Catalog.Goods)
        {
            Assert.AreEqual(table[entry.OriginalCode!.Value], entry.Request, $"the request of {entry.Name}");
        }

        var orders = Enumerable.Range(0, 3).SelectMany(draw => Steps(tables.DistributionOrder(draw)));
        Assert.AreEqual(
            0xC2431101D4476AF0UL,
            Fnv(Encoding.UTF8.GetBytes(string.Join('|', classes) + "#" + string.Join('|', orders))),
            "the class table and the three orders"
        );
    }

    [TestMethod]
    public void TheTransportPrioritiesAreTheRanksTheDefaultOrderRoutineWrites()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(26, tables.TransportPriority(Good.Fish), "good 1");
        Assert.AreEqual(1, tables.TransportPriority(new Good { Code = 26, Name = "shield" }), "good 26");
    }

    [TestMethod]
    public void TheCatalogHoldsTheOriginalsTwentySixGoodsWithTheirCodesNamesAndEvidence()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(26, tables.Catalog.Goods.Length);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 26).ToArray(),
            tables.Catalog.Goods.Select(good => good.OriginalCode!.Value).Order().ToArray()
        );
        Assert.AreEqual("log", tables.Catalog.EntryOf(new Good { Code = 7 })!.Name);
        Assert.AreEqual("iron-ore", tables.Catalog.EntryOf(new Good { Code = 11 })!.Name);
        Assert.IsTrue(
            tables.Catalog.Goods.All(good => !string.IsNullOrWhiteSpace(good.NameEvidence)),
            "each name cites evidence"
        );
        CollectionAssert.AreEqual(
            tables.Catalog.Goods.Select(good => good.Name).Order(StringComparer.Ordinal).ToArray(),
            tables.Catalog.Goods.Select(good => good.Name).ToArray(),
            "ordinal order of the stable names"
        );
    }

    [TestMethod]
    public void TheNameOfEachGoodInTheCatalogIsTheNameTablesNameUntilTheNameTableLetsGo()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        foreach (var entry in tables.Catalog.Goods)
        {
            Assert.AreEqual(OpenSettlers.Simulation.NameTable.GoodName(entry.OriginalCode!.Value), entry.Name);
        }
    }

    // The handover counter and resource-1 counting the domain derived from a good's number before the entries stated them
    // (0x43c4ea, 0x462c8d): the oracle the importer's facts are compared with.
    static FillCounter OldHandover(int code) =>
        (code >= 9 ? code != 13 : code == 7) ? FillCounter.High : FillCounter.Low;

    [TestMethod]
    public void TheHandoverCounterAndResource1CountingOfEveryOriginalGoodAreThoseTheProgramsRoutinesTest()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        foreach (var code in Enumerable.Range(1, 26))
        {
            var good = new Good { Code = code };

            Assert.AreEqual(OldHandover(code), tables.HandoverCounterOf(good), $"the handover of the good {code}");
            Assert.AreEqual(code is 14 or 15, tables.CountsAgainstResource1(good), $"resource 1 of the good {code}");
        }
    }

    [TestMethod]
    public void ADistributionStepThatNamesAGoodCodeWithNoEntryIsRefused()
    {
        var exe = Executable(bytes =>
            BitConverter
                .GetBytes((short)(2 * 30))
                .CopyTo(bytes, FileOffset(ProgramTablesImporter.DistributionAddresses[0]) + 4)
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "good 31");
        StringAssert.Contains(refusal.Message, "no entry");
    }

    [TestMethod]
    public void AWorkersToolThatNamesAGoodCodeWithNoEntryIsRefused()
    {
        var address = ProgramTablesImporter.WorkerRequestAddresses[0].Address;
        var exe = Executable(bytes => BitConverter.GetBytes(2 * 40).CopyTo(bytes, FileOffset(address) + 8));

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "good 40");
        StringAssert.Contains(refusal.Message, "no entry");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryRuleOfTheInstallReadsTheValueItReadBeforeForEachOfTheGoods()
    {
        var tables = TestProgram.Tables();
        var exe = TestProgram.OpenInstall().ReadAll(Install.Program);
        var kinds = ProgramTablesImporter.GoodKindBytes(exe);
        var classes = ProgramTablesImporter.GoodClassTable(exe);

        Assert.AreEqual(26, tables.Catalog.Goods.Length);
        foreach (var code in Enumerable.Range(1, 26))
        {
            var good = new Good { Code = code };

            Assert.AreEqual(classes[code], tables.ClassOfGood(good), $"the request of the good {code}");
            Assert.AreEqual(
                kinds[code] switch
                {
                    0 => FillCounter.Low,
                    1 => FillCounter.High,
                    _ => FillCounter.None,
                },
                tables.FillCounterOf(good),
                $"the counter of the good {code}"
            );
            Assert.AreEqual(OldHandover(code), tables.HandoverCounterOf(good), $"the handover of the good {code}");
            Assert.AreEqual(code is 14 or 15, tables.CountsAgainstResource1(good), $"resource 1 of the good {code}");
        }
    }

    [TestMethod]
    public void ADefaultOrderRoutineWithoutItsInstructionsIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[
                FileOffset(ProgramTablesImporter.TransportPriorityAddress)
                    + ProgramTablesImporter.TransportPriorityStep * 4
            ] = 0x90
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "transport priority table");
    }

    [TestMethod]
    public void ADefaultOrderWhoseRanksAreNotOneTo26EachOnceIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[
                FileOffset(ProgramTablesImporter.TransportPriorityAddress)
                    + ProgramTablesImporter.TransportPriorityStep * 4
                    + 1
            ] = 26
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "transport priority table");
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheTransportPrioritiesOfTheInstallMatchTheEmulator()
    {
        var tables = TestProgram.Tables();

        // The bytes 1 to 26 of the prio= line of the goods and remove verbs, which run 0x445570 at the game's start.
        Assert.AreEqual(
            0xBBF1449BF71E9134UL,
            Fnv(Enumerable.Range(1, 26).Select(code => (byte)tables.TransportPriority(new Good { Code = code })))
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFigureTablesHoldTheWordsTheDrawRoutineReadsUnaligned()
    {
        var tables = TestProgram.Tables();

        // 0x44fff3 reads a pair of words at 4 times the high byte of the figure word in the bases (0x4501a1, odd) and at 4
        // times its low byte in the offsets (0x4503c5, odd): the woodcutter's rows 0x0b (walk), 0x10 (way back with the log),
        // and 0x0f (felling), the carrier's row 0, and the offsets of the figure bytes 9 and 0.
        var bases = tables.Figures.Bases;
        var offsets = tables.Figures.Offsets;
        CollectionAssert.AreEqual(new short[] { 0, 0 }, bases.AsSpan()[..2].ToArray());
        CollectionAssert.AreEqual(new short[] { 48, 54 }, bases.AsSpan()[(2 * 0x0b)..(2 * 0x0b + 2)].ToArray());
        CollectionAssert.AreEqual(new short[] { 272, -1 }, bases.AsSpan()[(2 * 0x0f)..(2 * 0x0f + 2)].ToArray());
        CollectionAssert.AreEqual(new short[] { 48, 60 }, bases.AsSpan()[(2 * 0x10)..(2 * 0x10 + 2)].ToArray());
        CollectionAssert.AreEqual(new short[] { 9, 1 }, offsets.AsSpan()[18..20].ToArray());
        CollectionAssert.AreEqual(new short[] { 0, 0 }, offsets.AsSpan()[..2].ToArray());
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheWoodcuttersFigureWordsOfTheInstallAreThoseOfHisDrawHandler()
    {
        var adds = TestProgram.Tables().Get<FigureAdds>(new Woodcutter());

        // 0x43fb6b, the handler of job 5: add word [edi+8], imm16 at 0x43fc16 (walk), 0x43fc0b (way back with the log), 0x43fbe5 (figure byte from 0x80).
        Assert.AreEqual(
            new FigureAdds
            {
                Walk = 0x0b00,
                Carrying = 0x1000,
                Cutting = 0x0e80,
            },
            adds
        );
    }

    [TestMethod]
    public void TheWoodcuttersFigureWordsAreTheImmediatesOfHisHandler()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new FigureAdds
            {
                Walk = 0x0b00,
                Carrying = 0x1000,
                Cutting = 0x0e80,
            },
            tables.Get<FigureAdds>(new Woodcutter())
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheStonecuttersFigureWordsOfTheInstallAreThoseOfHisDrawHandler()
    {
        var adds = TestProgram.Tables().Get<FigureAdds>(new Stonecutter());

        // 0x43fcd8, the handler of job 7: add word [edi+8], imm16 at 0x43fd7d (walk), 0x43fd55 (way back with the stone), 0x43fd2f
        // (figure byte from 0x80).
        Assert.AreEqual(
            new FigureAdds
            {
                Walk = 0x0d00,
                Carrying = 0x1200,
                Cutting = 0x1280,
            },
            adds
        );
    }

    [TestMethod]
    public void TheStonecuttersFigureWordsAreTheImmediatesOfHisHandler()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new FigureAdds
            {
                Walk = 0x0d00,
                Carrying = 0x1200,
                Cutting = 0x1280,
            },
            tables.Get<FigureAdds>(new Stonecutter())
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheForestersFigureWordsOfTheInstallAreThoseOfHisDrawHandler()
    {
        var adds = TestProgram.Tables().Get<ForesterFigureAdds>(new Forester());

        // 0x43fd88, the handler of job 8: add word [edi+8], imm16 at 0x43fdea (walk) and 0x43fddf (figure byte from 0x80).
        Assert.AreEqual(new ForesterFigureAdds { Walk = 0x0e00, Work = 0x1080 }, adds);
    }

    [TestMethod]
    public void TheForestersFigureWordsAreTheImmediatesOfHisHandler()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new ForesterFigureAdds { Walk = 0x0e00, Work = 0x1080 },
            tables.Get<ForesterFigureAdds>(new Forester())
        );
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheFarmersFigureWordsOfTheInstallAreThoseOfHisDrawHandler()
    {
        var adds = TestProgram.Tables().Get<FarmerFigureAdds>(new Farmer());

        // 0x44002b, the handler of job 14: add word [edi+8], imm16 at 0x4400c8 (walk), 0x4400bd (the way back with the grain), 0x44003c
        // (sowing, figure byte from 0x80), and 0x440097 (harvest).
        Assert.AreEqual(
            new FarmerFigureAdds
            {
                Walk = 0x3d00,
                Carrying = 0x4000,
                Sow = 0x3d80,
                Harvest = 0x3e80,
            },
            adds
        );
    }

    [TestMethod]
    public void TheFarmersFigureWordsAreTheImmediatesOfHisHandler()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new FarmerFigureAdds
            {
                Walk = 0x3d00,
                Carrying = 0x4000,
                Sow = 0x3d80,
                Harvest = 0x3e80,
            },
            tables.Get<FarmerFigureAdds>(new Farmer())
        );
    }

    [TestMethod]
    public void AForestersStubThatJumpsElsewhereIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[
                FileOffset(ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Forester.Number)
                    + 1
            ] ^= 1
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "forester");
    }

    [TestMethod]
    public void AStonecuttersStepBackThatAddsOtherThanHisWayBackIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.StonecutterStepBackAddress) + 5] ^= 1);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "figure");
    }

    [TestMethod]
    public void TheFigureWordsOfTheFisherAreTheImmediatesOfHisHandler()
    {
        var tables = ProgramTablesImporter.Import(Executable(_ => { }));

        Assert.AreEqual(
            new FisherFigureAdds
            {
                Walk = 0x2c00,
                Carrying = 0x2f00,
                High = 0x2c80,
                HighFacing = 0x2d80,
            },
            tables.Get<FisherFigureAdds>(new Fisher())
        );
    }

    [TestMethod]
    public void AStubOfTheFisherThatJumpsElsewhereIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[
                FileOffset(ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Fisher.Number) + 1
            ] ^= 1
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "fisher");
    }

    [TestMethod]
    public void AJobWhoseDataNoModuleReadsIsAnErrorOfTheLoad()
    {
        var exe = Executable(_ => { });
        var modules = JobTablesRegistry.Modules().Where(module => module.Job is not Builder).ToArray();

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe, modules));
        StringAssert.Contains(refusal.Message, "no tables module read");
        StringAssert.Contains(refusal.Message, "Builder");
    }

    [TestMethod]
    public void AHandlerOtherThanTheJobsIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[
                FileOffset(ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Woodcutter.Number)
                    + 1
            ] ^= 1
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "figure");
        StringAssert.Contains(refusal.Message, "woodcutter");
    }

    [TestMethod]
    public void AHandlerWithoutItsAddInstructionIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.WoodcutterCarryingAddress)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "figure");
    }

    [TestMethod]
    public void ADrawRoutineThatLoadsOtherTablesIsRefused()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.FigureOffsetLoadAddress) + 2] ^= 1);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "figure");
    }

    // The file signatures of a PE32 executable: "MZ" at the file's start and "PE\0\0" (0x4550 as a little-endian word) at the
    // offset e_lfanew that the word at 0x3c holds (the Microsoft PE format specification).
    [TestMethod]
    public void ALongFileWithAWrongMzSignatureIsRefusedAsNoExecutable()
    {
        var exe = Executable(bytes => bytes[1] = (byte)'z');

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        Assert.AreEqual("Siedler1.exe is no PE executable.", refusal.Message);
    }

    [TestMethod]
    public void ALongFileWithAWrongPeSignatureIsRefusedAsNoExecutable()
    {
        var exe = Executable(bytes => bytes[0x40 + 2] = (byte)'X');

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        Assert.AreEqual("Siedler1.exe is no PE executable.", refusal.Message);
    }

    // The hand-out order of a storage ends at a negative word and holds at most one step for each of the 26 goods in each of the two
    // request words: 2 * 26 = 52 steps (hand arithmetic). A list of 53 valid steps never ends, so step 52 is the first one refused.
    [TestMethod]
    public void AnOrderListWithNoTerminatingWordIsRefusedAtItsFiftyThirdStep()
    {
        var exe = Executable(bytes =>
        {
            for (var step = 0; step < 53; step++)
            {
                byte[] code = [0x42, 0, 1, 0, 2, 0];
                code.CopyTo(bytes, FileOffset(ProgramTablesImporter.DistributionAddresses[0]) + 6 * step);
            }
        });

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        Assert.AreEqual(
            "Siedler1.exe holds no order of the storage's hand-out at 0x42a313: step 52 is no step.",
            refusal.Message
        );
    }

    // The routine at 0x445752 starts with mov ax, imm16 (0x66 0xb8, docs/rules/construction-materials.md); another first byte is another
    // routine, and the message names the address and what the code no longer does.
    [TestMethod]
    public void APlayerSetupThatDoesNotLoadItsWordIsRefusedAtTheFirstInstruction()
    {
        var exe = Executable(bytes => bytes[FileOffset(ProgramTablesImporter.PlankRankAddress)] = 0x90);

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        Assert.AreEqual(
            "Siedler1.exe holds no figure routine as expected: the code at 0x445752 no longer loads the word of the player's default.",
            refusal.Message
        );
    }

    [TestMethod]
    public void AStonecuttersStubThatJumpsElsewhereIsRefused()
    {
        var exe = Executable(bytes =>
            bytes[
                FileOffset(
                    ProgramTablesImporter.JobStubAddress + ProgramTablesImporter.JobStubSize * Stonecutter.Number
                ) + 1
            ] ^= 1
        );

        var refusal = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import(exe));
        StringAssert.Contains(refusal.Message, "no longer jumps to the stonecutter's handler");
    }

    // Made-up waiting-carrier tables: phase word i is 0x100 * i + 7, figure word i is 0x8800 + i, and offset byte k is 3 * k (mod 256).
    // Hand arithmetic: phase 15 = 0x0f07; figure 127 = 0x887f; offset 127 = 381 mod 256 = 125; offset 43 = 129, a signed byte of -127.
    [TestMethod]
    public void TheWaitingCarriersTablesAreReadFromAMadeUpExecutable()
    {
        var exe = Executable(bytes =>
        {
            for (var i = 0; i < RuleTables.IdlePhaseCount; i++)
            {
                BitConverter
                    .GetBytes((short)(0x100 * i + 7))
                    .CopyTo(bytes, FileOffset(ProgramTablesImporter.IdlePhaseAddress) + 2 * i);
            }

            for (var i = 0; i < RuleTables.IdleFigureCount; i++)
            {
                BitConverter
                    .GetBytes(unchecked((short)(0x8800 + i)))
                    .CopyTo(bytes, FileOffset(ProgramTablesImporter.IdleFigureAddress) + 2 * i);
            }

            for (var k = 0; k < RuleTables.IdleOffsetCount; k++)
            {
                bytes[FileOffset(ProgramTablesImporter.IdleOffsetAddress) + k] = (byte)(3 * k);
            }
        });

        var tables = ProgramTablesImporter.Import(exe);

        var figures = tables.Figures;
        Assert.AreEqual(RuleTables.IdlePhaseCount, figures.IdlePhases.Length);
        Assert.AreEqual((short)7, figures.IdlePhases[0]);
        Assert.AreEqual((short)0x0f07, figures.IdlePhases[15]);
        Assert.AreEqual(RuleTables.IdleFigureCount, figures.IdleFigures.Length);
        Assert.AreEqual(unchecked((short)0x8800), figures.IdleFigures[0]);
        Assert.AreEqual(unchecked((short)0x887f), figures.IdleFigures[127]);
        Assert.AreEqual(RuleTables.IdleOffsetCount, figures.IdleOffsets.Length);
        Assert.AreEqual((sbyte)0, figures.IdleOffsets[0]);
        Assert.AreEqual((sbyte)-127, figures.IdleOffsets[43]);
        Assert.AreEqual((sbyte)125, figures.IdleOffsets[127]);
    }

    static ulong Fnv(IEnumerable<byte> bytes) => bytes.Aggregate(FnvOffset, (hash, value) => (hash ^ value) * FnvPrime);

    // The waiting carriers' tables of the reference build (0x44d87d, 0x44d89d, 0x674923): the first phase word, the first
    // figure word (row 0x88), and the offsets of a node whose road runs north and south (directions 2 and 5, bits 0x24).
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheWaitingCarriersTablesAreReadFromTheProgram()
    {
        var tables = TestProgram.Tables();

        var figures = tables.Figures;
        Assert.AreEqual(0x0240, figures.IdlePhases[0]);
        Assert.AreEqual(unchecked((short)0x8800), figures.IdleFigures[0]);
        Assert.AreEqual((sbyte)2, figures.IdleOffsets[2 * 0x24]);
        Assert.AreEqual((sbyte)1, figures.IdleOffsets[2 * 0x24 + 1]);
        // The row 0x88 of the figure bases, the last before the figure offsets: the base 192 and no second figure.
        CollectionAssert.AreEqual(
            new short[] { 192, -1 },
            figures.Bases.AsSpan()[(2 * 0x88)..(2 * 0x88 + 2)].ToArray()
        );
    }
}
