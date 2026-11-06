using System;
using System.IO;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The tables of the original's program data that the rules read: the object class table, the start stock
/// table, and the others below. They are read from the install's <c>Siedler1.exe</c> at run time and never restated in source (INV-0001).
/// <see cref="Read"/> finds each by its address in the program, through the section headers of the executable.
/// </summary>
public sealed class ProgramTables
{
    /// <summary>The address of the object class table: 128 bytes, one for each object code (<c>0x41e640</c>).</summary>
    public const int ClassTableAddress = 0x41e640;

    /// <summary>The number of object codes, and the size of the class table.</summary>
    public const int ClassCount = 128;

    /// <summary>The address of the start stock table: 5 rows of 26 bytes (<c>0x442e8a</c>).</summary>
    public const int StartStockAddress = 0x442e8a;

    /// <summary>The number of rows of the start stock table (the levels 0, 10, 20, 30, and 40).</summary>
    public const int StartStockRows = 5;

    /// <summary>The address of the step time table: 16-bit words, one for each step shape (<c>0x43f248</c>).</summary>
    public const int StepTimeAddress = 0x43f248;

    /// <summary>
    /// The first index of <see cref="StepTimes"/> the table holds: a step into a node much lower than its start
    /// reads the program bytes before the table, as the original does.
    /// </summary>
    public const int StepTimeFirst = -32;

    /// <summary>The number of words of <see cref="StepTimes"/> (the indices <see cref="StepTimeFirst"/> to 127).</summary>
    public const int StepTimeCount = 160;

    /// <summary>The address of the door speed table: one byte for each building type (<c>0x43c92b</c>).</summary>
    public const int DoorSpeedAddress = 0x43c92b;

    /// <summary>The number of building types the door speed table covers.</summary>
    public const int DoorSpeedCount = 64;

    /// <summary>The address of the table of scan windows: 16-bit words, one for each map size (<c>0x421d28</c>).</summary>
    public const int ScanWindowAddress = 0x421d28;

    /// <summary>
    /// The address of the figure bases (<c>0x4501a1</c>, 256 words): two 16-bit words for each value of the high byte of
    /// the figure word, the base of the figure and the base of the second figure (negative for none). The address is odd:
    /// the draw routine loads it at <see cref="FigureBaseLoadAddress"/> and reads the words unaligned.
    /// </summary>
    public const int FigureBaseAddress = 0x4501a1;

    /// <summary>
    /// The address of the figure offsets (<c>0x4503c5</c>, 512 words): two 16-bit words for each value of the low byte of
    /// the figure word, what it adds to the figure and to the second figure. The address is odd, as the bases' is.
    /// </summary>
    public const int FigureOffsetAddress = 0x4503c5;

    /// <summary>The <c>lea esi, imm32</c> in the draw routine (<c>0x44fff3</c>) that loads <see cref="FigureBaseAddress"/>.</summary>
    public const int FigureBaseLoadAddress = 0x4500fb;

    /// <summary>The <c>lea esi, imm32</c> in the draw routine that loads <see cref="FigureOffsetAddress"/>.</summary>
    public const int FigureOffsetLoadAddress = 0x450128;

    /// <summary>
    /// The jump stubs of the figure handlers (<c>0x43f744</c>): 8 bytes for each job, <c>jmp rel32</c> to the job's
    /// handler, which adds the job's constant to the figure byte to make the figure word.
    /// </summary>
    public const int JobStubAddress = 0x43f744;

    /// <summary>The bytes of the stub of one job.</summary>
    public const int JobStubSize = 8;

    /// <summary>The handler of the woodcutter's figure (<c>0x43fb6b</c>, the target of the stub of job 5).</summary>
    public const int WoodcutterHandlerAddress = 0x43fb6b;

    /// <summary>The <c>add word [edi+8], imm16</c> of the handler for a figure byte from 0x80 (the felling).</summary>
    public const int WoodcutterCuttingAddress = 0x43fbe5;

    /// <summary>The <c>add word [edi+8], imm16</c> of the handler for the way back with the log (state 0x10, byte 13 0x80, byte 14 1).</summary>
    public const int WoodcutterCarryingAddress = 0x43fc0b;

    /// <summary>The <c>add word [edi+8], imm16</c> of the handler for any other figure byte below 0x80 (the walk).</summary>
    public const int WoodcutterWalkAddress = 0x43fc16;

    /// <summary>The bytes of <c>add word [edi+8], imm16</c> before the immediate.</summary>
    private static readonly byte[] AddFigureWord = [0x66, 0x81, 0x47, 0x08];

    /// <summary>
    /// The address of the carried goods' figure words: 27 words, one for each good (<c>0x43f4b7</c>). The carrier's draw
    /// handler (<c>0x43f8ae</c>) loads this address at <c>0x43f904</c> and adds the word at 2 * good to the figure word
    /// (<c>docs/rules/goods.md</c>). The high byte of a word is the row of the figure bases, so the log's <c>0x1400</c>
    /// puts the figure byte in the row <c>0x14</c> (confirmed by the draw run of <c>settler-draws.txt</c>, the carrier carrying a log).
    /// </summary>
    public const int CarriedFigureAddress = 0x43f4b7;

    /// <summary>The number of goods the carried figure table covers.</summary>
    public const int CarriedFigureCount = 27;

    /// <summary>
    /// The address of the frame rows: one signed byte for each frame of a settler (<c>0x43f402</c>), 181 of them up to the carried
    /// goods' table. The record builder (<c>0x43f537</c>) adds the byte of the settler's frame to the row of the view whose list
    /// takes the settler's record, so the figure is drawn before or after the settlers of the row above or below
    /// (<c>docs/rules/settlers.md</c>).
    /// </summary>
    public const int FrameRowAddress = 0x43f402;

    /// <summary>The number of frames the frame row table covers.</summary>
    public const int FrameRowCount = CarriedFigureAddress - FrameRowAddress;

    /// <summary>The number of words of <see cref="ScanWindows"/>.</summary>
    public const int ScanWindowCount = 8;

    /// <summary>The address of the table that gives a lost good the byte of its destination's fill it counts against (<c>0x462d6d</c>).</summary>
    public const int GoodKindAddress = 0x462d6d;

    /// <summary>The number of goods the kind table covers.</summary>
    public const int GoodKindCount = 32;

    /// <summary>The address of the table of the carriers a road of each length class holds at most (<c>0x462acd</c>).</summary>
    public const int RoadCapacityAddress = 0x462acd;

    /// <summary>The number of length classes of a road.</summary>
    public const int RoadCapacityCount = 8;

    /// <summary>The address of the walk rows: 8 bytes for each row of the free walk's direction choice (<c>0x4388ff</c>).</summary>
    public const int WalkRowAddress = 0x4388ff;

    /// <summary>The number of rows the free walk's direction choice reads (a row for each value of its heading class, 0 to 23).</summary>
    public const int WalkRowCount = 24;

    /// <summary>The bytes of one walk row: the directions the walker tries in order, each as 4 times its number plus 4.</summary>
    public const int WalkRowSize = 8;

    /// <summary>
    /// The address of the routine that writes the default transport priorities into a player's block at a new game
    /// (<c>0x445570</c>, called by <c>0x42055e</c>): one <c>mov al, imm8</c> and its store for each good 1 to 26.
    /// </summary>
    public const int TransportPriorityAddress = 0x445570;

    /// <summary>The bytes of the routine for one good: the rank of good g is the immediate at <see cref="TransportPriorityAddress"/> + 8 * (g - 1) + 1.</summary>
    public const int TransportPriorityStep = 8;

    /// <summary>The opcode of <c>mov al, imm8</c>, which starts each good's step of the routine.</summary>
    public const byte MovAlOpcode = 0xb0;

    /// <summary>
    /// Creates tables from their contents; <see cref="Read"/> creates them from the program. The tables of the
    /// settlers' walk (<paramref name="stepTimes"/>, <paramref name="doorSpeeds"/>, <paramref name="scanWindows"/>) are
    /// empty where a test needs none; the walk then reads them as zeros.
    /// </summary>
    public ProgramTables(
        byte[] classes,
        byte[][] startStock,
        ushort[]? stepTimes = null,
        byte[]? doorSpeeds = null,
        ushort[]? scanWindows = null,
        short[]? figureBases = null,
        short[]? figureOffsets = null,
        byte[]? walkRows = null,
        short[]? carriedFigures = null,
        byte[]? goodKinds = null,
        byte[]? roadCapacities = null,
        byte[]? transportPriorities = null,
        FigureAdds? woodcutterFigureAdds = null,
        sbyte[]? frameRows = null
    )
    {
        if (
            classes.Length != ClassCount
            || startStock.Length != StartStockRows
            || startStock.Any(row => row.Length != Simulation.StartStock.Goods)
        )
        {
            throw new ArgumentException("A class table holds 128 bytes and a start stock table 5 rows of 26.");
        }

        Classes = classes;
        StartStock = startStock;
        StepTimes = stepTimes ?? new ushort[StepTimeCount];
        DoorSpeeds = doorSpeeds ?? new byte[DoorSpeedCount];
        ScanWindows = scanWindows ?? new ushort[ScanWindowCount];
        FigureBases = figureBases ?? new short[256];
        FigureOffsets = figureOffsets ?? new short[512];
        WalkRows = walkRows ?? new byte[WalkRowCount * WalkRowSize];
        CarriedFigures = carriedFigures ?? new short[CarriedFigureCount];
        GoodKinds = goodKinds ?? new byte[GoodKindCount];
        RoadCapacities = roadCapacities ?? new byte[RoadCapacityCount];
        TransportPriorities = transportPriorities ?? new byte[Simulation.StartStock.Goods];
        WoodcutterFigureAdds = woodcutterFigureAdds ?? default;
        FrameRows = frameRows ?? new sbyte[FrameRowCount];
    }

    /// <summary>
    /// The default transport priority of each good 1 to 26 (index good - 1): its rank, 1 to 26, each once; the carrier's
    /// exchange lines up the good of the highest rank next (<c>docs/rules/goods.md</c>). All 0 where a test needs none.
    /// </summary>
    public byte[] TransportPriorities { get; }

    /// <summary>
    /// For each good which byte of a building's fill a lost good counts against: 0 the byte at 8, 1 the byte at 9, 0xff none
    /// (<c>0x462c8d</c>, <c>docs/rules/removal.md</c>).
    /// </summary>
    public byte[] GoodKinds { get; }

    /// <summary>For each length class of a road (<c>0x4454e7</c>) the number of carriers the road holds at most (<c>0x462acd</c>).</summary>
    public byte[] RoadCapacities { get; }

    /// <summary>
    /// The rows of the free walk's direction choice, <see cref="WalkRowSize"/> bytes each: the directions to try, best
    /// first, each as the offset of the step in the program's list of neighbours (4 east, 8 south-east, 12 south, 16 west,
    /// 20 north-west, 24 north). A row is picked by the heading class of the vector left to walk
    /// (<c>docs/rules/woodcutting.md</c>).
    /// </summary>
    public byte[] WalkRows { get; }

    /// <summary>For each good a word that a carrier carrying it adds to its figure byte: the row in the high byte (<c>0x43f8ae</c>).</summary>
    public short[] CarriedFigures { get; }

    /// <summary>For each high byte of the figure word 2 words: the figure base and the second figure base (negative: none).</summary>
    public short[] FigureBases { get; }

    /// <summary>For each low byte of the figure word 2 words: what it adds to the figure and to the second figure.</summary>
    public short[] FigureOffsets { get; }

    /// <summary>What the woodcutter's handler adds to the figure byte to make the figure word.</summary>
    public FigureAdds WoodcutterFigureAdds { get; }

    /// <summary>For each frame of a settler what the record builder adds to the row of its record's list (<see cref="FrameRowAddress"/>).</summary>
    public sbyte[] FrameRows { get; }

    /// <summary>
    /// The time in clock units of a step, by step shape: <c>4 + 9 * direction + (height of the node reached minus
    /// height of the node left)</c>, with the directions east 0 to north 5 (<c>docs/rules/settlers.md</c>). Index with
    /// <see cref="StepTime"/>.
    /// </summary>
    public ushort[] StepTimes { get; }

    /// <summary>The time of the step shape <paramref name="shape"/>; a shape outside the table reads as 0.</summary>
    public ushort StepTime(int shape)
    {
        var index = shape - StepTimeFirst;

        return index >= 0 && index < StepTimes.Length ? StepTimes[index] : (ushort)0;
    }

    /// <summary>The speed of the door of each building type, in 32nds of a step time.</summary>
    public byte[] DoorSpeeds { get; }

    /// <summary>
    /// The number of buildings the retry scan clears in one pass, by map size value (<c>0x428728</c>, set by
    /// <c>0x42195c</c> from the table at <c>0x421d28</c>).
    /// </summary>
    public ushort[] ScanWindows { get; }

    /// <summary>The class of each object code: 1 and 2 are the trees and stones, 3 a flag, 4 to 6 buildings.</summary>
    public byte[] Classes { get; }

    /// <summary>The start stock rows, each <see cref="Simulation.StartStock.Goods"/> counts.</summary>
    public byte[][] StartStock { get; }

    /// <summary>
    /// Reads the tables from the bytes of <c>Siedler1.exe</c>. Throws <see cref="InvalidDataException"/> when the
    /// bytes are no executable or a table lies outside the data the file holds.
    /// </summary>
    public static ProgramTables Read(byte[] exe)
    {
        var classes = Slice(exe, ClassTableAddress, ClassCount);
        var startStock = new byte[StartStockRows][];
        for (var row = 0; row < StartStockRows; row++)
        {
            startStock[row] = Slice(
                exe,
                StartStockAddress + row * Simulation.StartStock.Goods,
                Simulation.StartStock.Goods
            );
        }

        var steps = Slice(exe, StepTimeAddress + 2 * StepTimeFirst, 2 * StepTimeCount);
        var windows = Slice(exe, ScanWindowAddress, 2 * ScanWindowCount);

        return new ProgramTables(
            classes,
            startStock,
            [.. Enumerable.Range(0, StepTimeCount).Select(i => BitConverter.ToUInt16(steps, 2 * i))],
            Slice(exe, DoorSpeedAddress, DoorSpeedCount),
            [.. Enumerable.Range(0, ScanWindowCount).Select(i => BitConverter.ToUInt16(windows, 2 * i))],
            Words(Slice(exe, FigureBaseAddress, 512)),
            Words(Slice(exe, FigureOffsetAddress, 1024)),
            Slice(exe, WalkRowAddress, WalkRowCount * WalkRowSize),
            Words(Slice(exe, CarriedFigureAddress, 2 * CarriedFigureCount)),
            Slice(exe, GoodKindAddress, GoodKindCount),
            Slice(exe, RoadCapacityAddress, RoadCapacityCount),
            TransportPriorityRanks(exe),
            WoodcutterAdds(exe),
            [.. Slice(exe, FrameRowAddress, FrameRowCount).Select(row => (sbyte)row)]
        );
    }

    // The woodcutter's adds from the immediates of his handler. The draw routine must load the tables read here, and the
    // stub of the job must jump to the handler, else the program is not the one the rule was read from and the figures
    // would be wrong.
    private static FigureAdds WoodcutterAdds(byte[] exe)
    {
        Expect(
            exe,
            FigureBaseLoadAddress,
            [0x8d, 0x35, .. BitConverter.GetBytes(FigureBaseAddress)],
            "loads the figure bases"
        );
        Expect(
            exe,
            FigureOffsetLoadAddress,
            [0x8d, 0x35, .. BitConverter.GetBytes(FigureOffsetAddress)],
            "loads the figure offsets"
        );
        var stub = JobStubAddress + JobStubSize * Settler.WoodcutterJob;
        Expect(
            exe,
            stub,
            [0xe9, .. BitConverter.GetBytes(WoodcutterHandlerAddress - (stub + 5))],
            "jumps to the woodcutter's handler"
        );

        return new FigureAdds(
            Immediate(exe, WoodcutterWalkAddress),
            Immediate(exe, WoodcutterCarryingAddress),
            Immediate(exe, WoodcutterCuttingAddress)
        );
    }

    private static ushort Immediate(byte[] exe, int address)
    {
        Expect(exe, address, AddFigureWord, "adds to the figure word");

        return BitConverter.ToUInt16(Slice(exe, address + AddFigureWord.Length, 2));
    }

    private static void Expect(byte[] exe, int address, byte[] code, string what)
    {
        if (!Slice(exe, address, code.Length).SequenceEqual(code))
        {
            throw new InvalidDataException(
                $"Siedler1.exe holds no figure routine as expected: the code at 0x{address:x} no longer {what}."
            );
        }
    }

    // The rank of each good from the routine's instructions. Each good's step must start with mov al, imm8, and the ranks
    // must be 1 to 26 each once: else the routine is not where the table is expected, and the game would run with wrong
    // priorities.
    private static byte[] TransportPriorityRanks(byte[] exe)
    {
        var goods = Simulation.StartStock.Goods;
        var code = Slice(exe, TransportPriorityAddress, TransportPriorityStep * goods);
        var ranks = new byte[goods];
        for (var good = 0; good < goods; good++)
        {
            if (code[TransportPriorityStep * good] != MovAlOpcode)
            {
                throw new InvalidDataException(
                    $"Siedler1.exe holds no transport priority table at 0x{TransportPriorityAddress:x}: no mov al for good {good + 1}."
                );
            }

            ranks[good] = code[TransportPriorityStep * good + 1];
        }

        if (!ranks.Order().SequenceEqual(Enumerable.Range(1, goods).Select(rank => (byte)rank)))
        {
            throw new InvalidDataException(
                $"Siedler1.exe holds no transport priority table at 0x{TransportPriorityAddress:x}: the ranks are not 1 to {goods} each once."
            );
        }

        return ranks;
    }

    private static short[] Words(byte[] bytes) =>
        [.. Enumerable.Range(0, bytes.Length / 2).Select(i => BitConverter.ToInt16(bytes, 2 * i))];

    // The bytes at a program address, found through the section that holds it (PE32: e_lfanew at 0x3c, the file
    // header 4 bytes after it, the image base at +28 of the optional header, 40 bytes for each section header).
    private static byte[] Slice(byte[] exe, int address, int count)
    {
        try
        {
            var pe = BitConverter.ToInt32(exe, 0x3c);
            if (exe[0] != 'M' || exe[1] != 'Z' || BitConverter.ToInt32(exe, pe) != 0x4550)
            {
                throw new InvalidDataException("Siedler1.exe is no PE executable.");
            }

            var sections = BitConverter.ToUInt16(exe, pe + 6);
            var optional = pe + 24;
            var imageBase = BitConverter.ToInt32(exe, optional + 28);
            var rva = address - imageBase;
            for (var i = 0; i < sections; i++)
            {
                var header = optional + BitConverter.ToUInt16(exe, pe + 20) + 40 * i;
                var start = BitConverter.ToInt32(exe, header + 12);
                var rawSize = BitConverter.ToInt32(exe, header + 16);
                if (rva >= start && rva + count <= start + rawSize)
                {
                    return exe[(BitConverter.ToInt32(exe, header + 20) + rva - start)..][..count];
                }
            }
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("Siedler1.exe is truncated.", e);
        }

        throw new InvalidDataException($"Siedler1.exe holds no data at 0x{address:x}.");
    }
}

/// <summary>
/// What a job's handler adds to the figure byte to make the figure word: for a figure byte below 0x80 (<paramref name="Walk"/>,
/// or <paramref name="Carrying"/> on the way back with the log) and for one from 0x80 (<paramref name="Cutting"/>).
/// </summary>
public readonly record struct FigureAdds(ushort Walk, ushort Carrying, ushort Cutting);
