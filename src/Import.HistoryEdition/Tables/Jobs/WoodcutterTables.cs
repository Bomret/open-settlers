using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The woodcutter's tables: his figure adds from the immediates of his handler (<c>0x43fb6b</c>), where the stub of job 5 must
/// jump to the handler, and his states.
/// </summary>
[StateName(0x11, "cuts-tree", TableEvidence.State)]
[StateName(0x12, "looks-for-tree", TableEvidence.State)]
sealed class WoodcutterTables : JobTables<Woodcutter>
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
                new FigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.WoodcutterWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.WoodcutterCarryingAddress),
                    Cutting = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.WoodcutterCuttingAddress),
                }
            )
        );
    }
}
