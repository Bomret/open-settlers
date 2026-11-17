using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The miller's tables: his figure adds from the immediates of his handler (<c>0x4400d3</c>), where the stub of job 15 must jump to it,
/// and his name and state.
/// </summary>
[JobName(
    "miller",
    "Miller; the handler 0x42e2a0 of the building type 0xf asks for job 15 with no tool, its start 0x43d33d asks for grain (class 4) and its state 0x23 takes grain out of the building (0x43386b) and carries flour (good 5, 0x433a1e) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/milling.md"
)]
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
