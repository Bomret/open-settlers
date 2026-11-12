using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The woodcutter's tables: his figure adds from the immediates of his handler (<c>0x43fb6b</c>), where the stub of job 5 must
/// jump to the handler, and his name and states.
/// </summary>
[JobName("woodcutter", "Job.Woodcutter; docs/rules/woodcutting.md")]
[StateName(0x11, "cuts-tree", NameTable.StateEvidence)]
[StateName(0x12, "looks-for-tree", NameTable.StateEvidence)]
public sealed class WoodcutterTables : JobTables<Woodcutter>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.WoodcutterHandlerAddress,
            "jumps to the woodcutter's handler"
        );
        data.Add(
            (
                Job,
                new FigureAdds(
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.WoodcutterWalkAddress),
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.WoodcutterCarryingAddress),
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.WoodcutterCuttingAddress)
                )
            )
        );
    }
}
