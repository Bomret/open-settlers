using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The miller's tables: his figure adds from the immediates of his handler (<c>0x4400d3</c>), where the stub of job 15 must jump to it,
/// and his state.
/// </summary>
[StateName(
    0x23,
    "grinds",
    "0x43386b takes grain out of the building's fill and grinds it in four stretches, then carries flour out; inferred name, docs/rules/milling.md"
)]
sealed class MillerTables : JobTables<Miller>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.MillerHandlerAddress,
            "jumps to the miller's handler"
        );
        data.Add(
            (
                Job,
                new FoodHutFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.MillerWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.MillerCarryingAddress),
                    Work = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.MillerGrindAddress),
                }
            )
        );
    }
}
