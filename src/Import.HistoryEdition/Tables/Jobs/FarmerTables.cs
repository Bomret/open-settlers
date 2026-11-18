using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The farmer's tables: his figure adds from the immediates of his handler (<c>0x44002b</c>), where the stub of job 14 must jump to it,
/// and his states.
/// </summary>
[StateName(
    0x21,
    "looks-for-field",
    "0x433b35 draws nodes of the spiral until a ripe field or a free meadow node comes up; docs/rules/farming.md"
)]
[StateName(
    0x22,
    "tends-field",
    "0x433a3a waits on the node, then sows it or harvests the field; docs/rules/farming.md"
)]
sealed class FarmerTables : JobTables<Farmer>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.FarmerHandlerAddress,
            "jumps to the farmer's handler"
        );
        data.Add(
            (
                Job,
                new FarmerFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FarmerWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FarmerCarryingAddress),
                    Sow = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FarmerSowAddress),
                    Harvest = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FarmerHarvestAddress),
                }
            )
        );
    }
}
