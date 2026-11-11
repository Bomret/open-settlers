using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Builds the <see cref="RuleTables"/> from the bytes of the install's <c>Siedler1.exe</c> (INV-0001): the original's
/// addresses and packed bytes stay here (ADR 0013). <see cref="Import"/> finds each table by its address in the program,
/// through the section headers of the executable.
/// </summary>
public static class ProgramTablesImporter
{
    /// <summary>The address of the object class table: 128 bytes, one for each object code (<c>0x41e640</c>).</summary>
    public const int ClassTableAddress = 0x41e640;

    /// <summary>The address of the start stock table: 5 rows of 26 bytes (<c>0x442e8a</c>).</summary>
    public const int StartStockAddress = 0x442e8a;

    /// <summary>The address of the step time table: 16-bit words, one for each step shape (<c>0x43f248</c>).</summary>
    public const int StepTimeAddress = 0x43f248;

    /// <summary>The address of the door speed table: one byte for each building type (<c>0x43c92b</c>).</summary>
    public const int DoorSpeedAddress = 0x43c92b;

    /// <summary>The address of the building size table: one byte for each building type 0 to 0x17, 0 a mine, 1 small, 2 large (<c>0x42d307</c>).</summary>
    public const int BuildingSizeAddress = 0x42d307;

    /// <summary>The address of the needs table: the planks and the stones, one byte each, for each building type 0 to 0x17 (<c>0x449f18</c>).</summary>
    public const int BuildingNeedsAddress = 0x449f18;

    /// <summary>The address of the table of scan windows: 16-bit words, one for each map size (<c>0x421d28</c>).</summary>
    public const int ScanWindowAddress = 0x421d28;

    /// <summary>
    /// The address of the figure bases (<c>0x4501a1</c>, 274 words up to the figure offsets): two 16-bit words for each value
    /// of the high byte of the figure word up to 0x88, the base of the figure and the base of the second figure (negative for
    /// none). The address is odd: the draw routine loads it at <see cref="FigureBaseLoadAddress"/> and reads the words
    /// unaligned. The waiting carrier's figure words (<see cref="IdleFigureAddress"/>) use the row 0x88, the last.
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

    /// <summary>The handler of the stonecutter's figure (<c>0x43fcd8</c>, the target of the stub of job 7).</summary>
    public const int StonecutterHandlerAddress = 0x43fcd8;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for a figure byte from 0x80 (the stroke).</summary>
    public const int StonecutterCuttingAddress = 0x43fd2f;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for the way back with the stone (state 0x10, byte 13 0x80, byte 14 1).</summary>
    public const int StonecutterCarryingAddress = 0x43fd55;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for the step back off the heap (state 0x17, byte 13 2), which adds what the way back adds.</summary>
    public const int StonecutterStepBackAddress = 0x43fd72;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for any other figure byte below 0x80 (the walk).</summary>
    public const int StonecutterWalkAddress = 0x43fd7d;

    /// <summary>The handler of the forester's figure (<c>0x43fd88</c>, the target of the stub of job 8).</summary>
    public const int ForesterHandlerAddress = 0x43fd88;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for a figure byte from 0x80.</summary>
    public const int ForesterWorkAddress = 0x43fddf;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for a figure byte below 0x80 (the walk).</summary>
    public const int ForesterWalkAddress = 0x43fdea;

    /// <summary>The handler of the figure of the settler of the type 1 hut (<c>0x43fed8</c>, the target of the stub of job 11).</summary>
    public const int TypeOneHutHandlerAddress = 0x43fed8;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for a figure byte from 0x80 where the heading is not 1.</summary>
    public const int TypeOneHutHighAddress = 0x43ff0f;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for a figure byte from 0x80 where the heading is 1.</summary>
    public const int TypeOneHutHighFacingAddress = 0x43ff1a;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for the way back with a load (state 0x10, byte 13 0x80, byte 14 1).</summary>
    public const int TypeOneHutCarryingAddress = 0x43ff40;

    /// <summary>The <c>add word [edi+8], imm16</c> of that handler for any other figure byte below 0x80 (the walk).</summary>
    public const int TypeOneHutWalkAddress = 0x43ff4b;

    /// <summary>The handler of the builder's figure (<c>0x43faf4</c>, the target of the stub of job 3).</summary>
    public const int BuilderHandlerAddress = 0x43faf4;

    /// <summary>The <c>add word [edi+8], imm16</c> of the builder's handler for a figure byte from 0x80 (the blows).</summary>
    public const int BuilderWorkAddress = 0x43fb55;

    /// <summary>The <c>add word [edi+8], imm16</c> of the builder's handler for any other figure byte below 0x80 (the walk).</summary>
    public const int BuilderWalkAddress = 0x43fb60;

    /// <summary>The bytes of <c>add word [edi+8], imm16</c> before the immediate.</summary>
    private static readonly byte[] AddFigureWord = [0x66, 0x81, 0x47, 0x08];

    /// <summary>
    /// The address of the carried goods' figure words: 27 words, one for each good (<c>0x43f4b7</c>). The carrier's draw
    /// handler (<c>0x43f8ae</c>) loads this address at <c>0x43f904</c> and adds the word at 2 * good to the figure word
    /// (<c>docs/rules/goods.md</c>). The high byte of a word is the row of the figure bases, so the log's <c>0x1400</c>
    /// puts the figure byte in the row <c>0x14</c> (confirmed by the draw run of <c>settler-draws.txt</c>, the carrier carrying a log).
    /// </summary>
    public const int CarriedFigureAddress = 0x43f4b7;

    /// <summary>
    /// The address of the frame rows: one signed byte for each frame of a settler (<c>0x43f402</c>), 181 of them up to the carried
    /// goods' table. The record builder (<c>0x43f537</c>) adds the byte of the settler's frame to the row of the view whose list
    /// takes the settler's record, so the figure is drawn before or after the settlers of the row above or below
    /// (<c>docs/rules/settlers.md</c>).
    /// </summary>
    public const int FrameRowAddress = 0x43f402;

    /// <summary>
    /// The address of the waiting carriers' phases: 16 words (<c>0x44d87d</c>). The tile pass's figure routine (<c>0x44d77e</c>)
    /// picks one by bits 2 to 5 of the address of the node's record (<c>docs/rules/carriers.md</c>, "Drawing").
    /// </summary>
    public const int IdlePhaseAddress = 0x44d87d;

    /// <summary>
    /// The address of the waiting carriers' figure words: 128 words (<c>0x44d89d</c>), picked by the phase plus the clock,
    /// shifted right 3 (<c>0x44d77e</c>).
    /// </summary>
    public const int IdleFigureAddress = 0x44d89d;

    /// <summary>
    /// The address of the waiting carriers' offsets: two signed bytes, x then y, for each value of the low six bits of the
    /// node's flag byte, its road directions (<c>0x674923</c>, read by <c>0x44d77e</c>).
    /// </summary>
    public const int IdleOffsetAddress = 0x674923;

    /// <summary>The address of the table that gives a lost good the byte of its destination's fill it counts against (<c>0x462d6d</c>).</summary>
    public const int GoodKindAddress = 0x462d6d;

    /// <summary>The address of the table of the carriers a road of each length class holds at most (<c>0x462acd</c>).</summary>
    public const int RoadCapacityAddress = 0x462acd;

    /// <summary>The address of the walk rows: 8 bytes for each row of the free walk's direction choice (<c>0x4388ff</c>).</summary>
    public const int WalkRowAddress = 0x4388ff;

    /// <summary>
    /// The address of the routine that writes the default transport priorities into a player's block at a new game
    /// (<c>0x445570</c>, called by <c>0x42055e</c>): one <c>mov al, imm8</c> and its store for each good 1 to 26.
    /// </summary>
    public const int TransportPriorityAddress = 0x445570;

    /// <summary>
    /// The address of the good class table: two bytes for each good 0 to 31, a class and the offset of a request word (<c>0x464117</c>,
    /// read by the flag pass at <c>0x46440e</c> and <c>0x4644ab</c>); a class with its high bit set sends the good to storage at once.
    /// </summary>
    public const int GoodClassAddress = 0x464117;

    /// <summary>
    /// The addresses of the three orders in which a storage hands its goods out, each a list of steps of three words (the request
    /// word's offset, the class, and the stock word's offset or -1) that a negative word ends (<c>0x42a313</c>, <c>0x42a35d</c>,
    /// <c>0x42a3a7</c>, read by the phase routine at <c>0x42965c</c>).
    /// </summary>
    public static readonly int[] DistributionAddresses = [0x42a313, 0x42a35d, 0x42a3a7];

    /// <summary>
    /// The address of the routine that writes the player's default words at a new game (<c>0x445752</c>, called by <c>0x42055e</c>): it
    /// loads the word <c>0xffdc</c> and stores it at <c>+0x148</c> of the player block, whose high byte is the rank a site's request for
    /// planks starts from (<c>docs/rules/construction-materials.md</c>).
    /// </summary>
    public const int PlankRankAddress = 0x445752;

    /// <summary>
    /// The instructions of the building under construction's request for a builder (<c>0x42d5fc</c>, in <c>0x42d55f</c>): <c>mov eax, job</c> and
    /// <c>mov [edi], eax</c>, <c>mov eax, tool</c> and <c>mov [edi+0x18], eax</c> before the call of <c>0x42be63</c>. The tool is the offset of the
    /// stock word in the storage item (from +4), which is twice the good's number.
    /// </summary>
    public const int BuilderRequestAddress = 0x42d5fc;

    /// <summary>
    /// The same instructions in the handlers of the four types: the woodcutter's (<c>0x42d945</c>), the stonecutter's (<c>0x42d987</c>), the
    /// forester's (<c>0x42d9c9</c>), and the sawmill's (<c>0x42da0b</c>), each 13 bytes in.
    /// </summary>
    public static readonly (BuildingType Type, int Address)[] WorkerRequestAddresses =
    [
        (BuildingType.Woodcutter, 0x42d952),
        (BuildingType.Stonecutter, 0x42d994),
        (BuildingType.Forester, 0x42d9d6),
        (BuildingType.Sawmill, 0x42da18),
    ];

    /// <summary>The address of the builder's order of materials: a 16-bit word for each building type 0 to 0x17, a set bit meaning a stone (<c>0x43f0ba</c>).</summary>
    public const int MaterialOrderAddress = 0x43f0ba;

    /// <summary>The address of the builder's steps: two 16-bit words for each building type, one before the progress word reaches <c>0x8000</c> and one after (<c>0x43f0fa</c>).</summary>
    public const int BlowStepAddress = 0x43f0fa;

    /// <summary>The bytes of the routine for one good: the rank of good g is the immediate at <see cref="TransportPriorityAddress"/> + 8 * (g - 1) + 1.</summary>
    public const int TransportPriorityStep = 8;

    /// <summary>The opcode of <c>mov al, imm8</c>, which starts each good's step of the routine.</summary>
    public const byte MovAlOpcode = 0xb0;

    /// <summary>
    /// Reads the tables from the bytes of <c>Siedler1.exe</c>. Throws <see cref="InvalidDataException"/> when the
    /// bytes are no executable or a table lies outside the data the file holds.
    /// </summary>
    public static RuleTables Import(byte[] exe)
    {
        var classes = Slice(exe, ClassTableAddress, RuleTables.ClassCount);
        var startStock = new byte[RuleTables.StartStockRows][];
        for (var row = 0; row < RuleTables.StartStockRows; row++)
        {
            startStock[row] = Slice(exe, StartStockAddress + row * RuleTables.Goods, RuleTables.Goods);
        }

        var steps = Slice(exe, StepTimeAddress + 2 * RuleTables.StepTimeFirst, 2 * RuleTables.StepTimeCount);
        var windows = Slice(exe, ScanWindowAddress, 2 * RuleTables.ScanWindowCount);

        return new RuleTables(
            [.. classes.Select(code => new ObjectClass(code))],
            startStock,
            [.. Enumerable.Range(0, RuleTables.StepTimeCount).Select(i => BitConverter.ToUInt16(steps, 2 * i))],
            Slice(exe, DoorSpeedAddress, RuleTables.DoorSpeedCount),
            [.. Enumerable.Range(0, RuleTables.ScanWindowCount).Select(i => BitConverter.ToUInt16(windows, 2 * i))],
            Words(Slice(exe, FigureBaseAddress, 4 * RuleTables.FigureBaseRowCount)),
            Words(Slice(exe, FigureOffsetAddress, 1024)),
            Slice(exe, WalkRowAddress, RuleTables.WalkRowCount * RuleTables.WalkRowSize),
            Words(Slice(exe, CarriedFigureAddress, 2 * RuleTables.CarriedFigureCount)),
            Slice(exe, GoodKindAddress, RuleTables.GoodKindCount),
            Slice(exe, RoadCapacityAddress, RuleTables.RoadCapacityCount),
            TransportPriorityRanks(exe),
            WoodcutterAdds(exe),
            [.. Slice(exe, FrameRowAddress, RuleTables.FrameRowCount).Select(row => (sbyte)row)],
            TypeOneHutAdds(exe),
            Words(Slice(exe, IdlePhaseAddress, 2 * RuleTables.IdlePhaseCount)),
            Words(Slice(exe, IdleFigureAddress, 2 * RuleTables.IdleFigureCount)),
            [.. Slice(exe, IdleOffsetAddress, RuleTables.IdleOffsetCount).Select(offset => (sbyte)offset)],
            Slice(exe, BuildingSizeAddress, RuleTables.BuildingTypeCount),
            Slice(exe, BuildingNeedsAddress, 2 * RuleTables.BuildingTypeCount),
            GoodClasses(Slice(exe, GoodClassAddress, 2 * RuleTables.GoodClassCount)),
            [.. DistributionAddresses.Select(address => DistributionOrder(exe, address))],
            PlankRankBase(exe),
            Workers(exe),
            WorkerRequest(exe, BuilderRequestAddress),
            [.. Words(Slice(exe, MaterialOrderAddress, 2 * RuleTables.BuildingTypeCount)).Select(word => (ushort)word)],
            [.. Words(Slice(exe, BlowStepAddress, 4 * RuleTables.BuildingTypeCount)).Select(word => (ushort)word)],
            BuilderAdds(exe),
            StonecutterAdds(exe),
            ForesterAdds(exe)
        );
    }

    // The forester's adds from the immediates of his handler: the stub of job 8 must jump to it.
    private static ForesterFigureAdds ForesterAdds(byte[] exe)
    {
        var stub = JobStubAddress + JobStubSize * Job.ForesterCode;
        Expect(
            exe,
            stub,
            [0xe9, .. BitConverter.GetBytes(ForesterHandlerAddress - (stub + 5))],
            "jumps to the forester's handler"
        );

        return new ForesterFigureAdds(Immediate(exe, ForesterWalkAddress), Immediate(exe, ForesterWorkAddress));
    }

    // The builder's adds from the immediates of his handler, as the woodcutter's are read: the stub of job 3 must jump to the handler.
    private static BuilderFigureAdds BuilderAdds(byte[] exe)
    {
        var stub = JobStubAddress + JobStubSize * Job.BuilderCode;
        Expect(
            exe,
            stub,
            [0xe9, .. BitConverter.GetBytes(BuilderHandlerAddress - (stub + 5))],
            "jumps to the builder's handler"
        );

        return new BuilderFigureAdds(Immediate(exe, BuilderWalkAddress), Immediate(exe, BuilderWorkAddress));
    }

    // The worker each of the four types asks for, from the instructions of its handler.
    private static WorkerRequest?[] Workers(byte[] exe)
    {
        var workers = new WorkerRequest?[RuleTables.BuildingTypeCount];
        foreach (var (type, address) in WorkerRequestAddresses)
        {
            workers[type.Code] = WorkerRequest(exe, address);
        }

        return workers;
    }

    // A request for a worker: mov eax, job; mov [edi], eax; mov eax, tool; mov [edi+0x18], eax. The tool word halved is the good.
    private static WorkerRequest WorkerRequest(byte[] exe, int address)
    {
        var code = Slice(exe, address, 15);
        if (
            code[0] != 0xb8
            || !code[5..7].SequenceEqual<byte>([0x89, 0x07])
            || code[7] != 0xb8
            || !code[12..15].SequenceEqual<byte>([0x89, 0x47, 0x18])
        )
        {
            throw new InvalidDataException(
                $"Siedler1.exe holds no request for a worker at 0x{address:x}: the code does not load a job and a tool as the handlers do."
            );
        }

        return new WorkerRequest(new Job(code[1]), new Good(BitConverter.ToInt32(code, 8) / 2));
    }

    // The request word a flag item offset names: 0x42 is the first word, 0x44 the second (the class table's second byte takes the
    // second for 0x44 and the first for any other offset, as the flag pass does).
    private static RequestWord WordAt(int offset) => offset == 0x44 ? RequestWord.Second : RequestWord.First;

    // The good class table: for each good a class byte and a request word offset. A class byte with its high bit set sends the good
    // to storage at once, which is no class.
    private static GoodClass?[] GoodClasses(byte[] table) =>
        [
            .. Enumerable
                .Range(0, RuleTables.GoodClassCount)
                .Select(good =>
                    (sbyte)table[2 * good] < 0
                        ? (GoodClass?)null
                        : new GoodClass(table[2 * good], WordAt(table[2 * good + 1]))
                ),
        ];

    // One order of the storage's hand-out: the steps up to the word that is negative. The request word's offset must be the first or
    // the second word's (0x42 or 0x44), else the table is not where it is expected. The third word of a step is the offset of the
    // stock word from the first one (the good is the offset halved, plus 1), or -1 for the step of the food goods 1, 3, and 6.
    private static DistributionStep[] DistributionOrder(byte[] exe, int address)
    {
        var steps = new List<DistributionStep>();
        while (true)
        {
            var words = Words(Slice(exe, address + 6 * steps.Count, 6));
            if (words[0] < 0)
            {
                break;
            }

            if (steps.Count >= 2 * RuleTables.Goods || words[0] is not (0x42 or 0x44))
            {
                throw new InvalidDataException(
                    $"Siedler1.exe holds no order of the storage's hand-out at 0x{address:x}: step {steps.Count} is no step."
                );
            }

            steps.Add(
                new DistributionStep(
                    new GoodClass(words[1], WordAt(words[0])),
                    words[2] < 0 ? [new Good(1), new Good(3), new Good(6)] : [new Good(words[2] / 2 + 1)]
                )
            );
        }

        return [.. steps];
    }

    // The rank the planks start from: the high byte of the word the player setup loads and stores at +0x148 of the player block
    // (0x445752: mov ax, imm16; mov ebx, [edi+0x30]; mov [ebx+0x148], ax).
    private static byte PlankRankBase(byte[] exe)
    {
        Expect(exe, PlankRankAddress, [0x66, 0xb8], "loads the word of the player's default");
        Expect(
            exe,
            PlankRankAddress + 4,
            [0x8b, 0x5f, 0x30, 0x66, 0x89, 0x83, 0x48, 0x01, 0x00, 0x00],
            "stores the word at +0x148 of the player block"
        );

        return Slice(exe, PlankRankAddress + 2, 2)[1];
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
        var stub = JobStubAddress + JobStubSize * Job.Woodcutter.Code;
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

    // The stonecutter's adds, read as the woodcutter's are: the stub of job 7 must jump to his handler, and the add of his step
    // back off the heap must be the add of his way back with the stone.
    private static FigureAdds StonecutterAdds(byte[] exe)
    {
        var stub = JobStubAddress + JobStubSize * Job.StonecutterCode;
        Expect(
            exe,
            stub,
            [0xe9, .. BitConverter.GetBytes(StonecutterHandlerAddress - (stub + 5))],
            "jumps to the stonecutter's handler"
        );
        var carrying = Immediate(exe, StonecutterCarryingAddress);
        if (Immediate(exe, StonecutterStepBackAddress) != carrying)
        {
            throw new InvalidDataException(
                "Siedler1.exe holds no figure routine as expected: the stonecutter's step back no longer adds what his way back with the stone adds."
            );
        }

        return new FigureAdds(
            Immediate(exe, StonecutterWalkAddress),
            carrying,
            Immediate(exe, StonecutterCuttingAddress)
        );
    }

    // The adds of the handler of the settler of the type 1 hut, read as the woodcutter's are: the stub of job 11 must jump
    // to the handler.
    private static HutFigureAdds TypeOneHutAdds(byte[] exe)
    {
        var stub = JobStubAddress + JobStubSize * RuleTables.TypeOneHutJob;
        Expect(
            exe,
            stub,
            [0xe9, .. BitConverter.GetBytes(TypeOneHutHandlerAddress - (stub + 5))],
            "jumps to the handler of the settler of the type 1 hut"
        );

        return new HutFigureAdds(
            Immediate(exe, TypeOneHutWalkAddress),
            Immediate(exe, TypeOneHutCarryingAddress),
            Immediate(exe, TypeOneHutHighAddress),
            Immediate(exe, TypeOneHutHighFacingAddress)
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
        var goods = RuleTables.Goods;
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
