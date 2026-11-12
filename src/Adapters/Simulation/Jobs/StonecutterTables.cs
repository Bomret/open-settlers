using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Simulation;

/// <summary>
/// The stonecutter's tables: his figure adds, read as the woodcutter's are (the stub of job 7 must jump to his handler, and the add
/// of his step back off the heap must be the add of his way back with the stone), and his name and states.
/// </summary>
[JobName(
    "stonecutter",
    "Stonecutter; his work puts good 10 (a stone) on the hut's flag (0x438182) and cuts the stone heaps 0x48 to 0x4f (0x435c5c); docs/rules/stonecutting.md"
)]
[StateName(
    0x15,
    "looks-for-heap",
    "0x435b17 draws nodes of the spiral until a stone heap stands beside one; docs/rules/stonecutting.md"
)]
[StateName(
    0x16,
    "walks-to-heap",
    "0x436a84 walks the vector out to the heap and stops where one stands north-west; docs/rules/stonecutting.md"
)]
[StateName(
    0x17,
    "cuts-stone",
    "0x435c5c steps onto the heap's node, strikes, and takes a stone off it; docs/rules/stonecutting.md"
)]
public sealed class StonecutterTables : JobTables<Stonecutter>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.StonecutterHandlerAddress,
            "jumps to the stonecutter's handler"
        );
        var carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.StonecutterCarryingAddress);
        if (ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.StonecutterStepBackAddress) != carrying)
        {
            throw new InvalidDataException(
                "Siedler1.exe holds no figure routine as expected: the stonecutter's step back no longer adds what his way back with the stone adds."
            );
        }

        data.Add(
            (
                Job,
                new FigureAdds(
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.StonecutterWalkAddress),
                    carrying,
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.StonecutterCuttingAddress)
                )
            )
        );
    }
}
