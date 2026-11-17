using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The baker's tables: his figure adds from the immediates of his handler (<c>0x440112</c>), where the stub of job 16 must jump to it,
/// and his name and state.
/// </summary>
[JobName(
    "baker",
    "Baker; the handler 0x42e37e of the building type 0x10 asks for job 16 with no tool, its start 0x43d3d9 asks for flour (class 5) and its state 0x24 takes flour out of the building (0x433694) and carries bread (good 6, 0x43384f) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/baking.md"
)]
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
