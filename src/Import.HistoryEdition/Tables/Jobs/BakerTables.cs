using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The baker's tables: his figure adds from the immediates of his handler (<c>0x440112</c>), where the stub of job 16 must jump to it,
/// and his state.
/// </summary>
[StateName(
    0x24,
    "bakes",
    "0x433694 takes flour out of the building's fill and bakes it in two stretches, then carries bread out; inferred name, docs/rules/baking.md"
)]
sealed class BakerTables : JobTables<Baker>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.BakerHandlerAddress,
            "jumps to the baker's handler"
        );
        data.Add(
            (
                Job,
                new FoodHutFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BakerWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BakerCarryingAddress),
                    Work = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BakerBakeAddress),
                }
            )
        );
    }
}
