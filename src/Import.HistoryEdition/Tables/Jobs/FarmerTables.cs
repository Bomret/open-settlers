using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The farmer's tables: his figure adds from the immediates of his handler (<c>0x44002b</c>), where the stub of job 14 must jump to it,
/// and his name and states.
/// </summary>
[JobName(
    "farmer",
    "Farmer; the type 0xc handler 0x42e25e asks for job 14 with the scythe (tool stock word 0x28), his work sows and harvests the fields 0x69 to 0x6e and 0x79 to 0x7e (0x433a3a) and puts grain (good 4) on the farm's flag (0x43847d); docs/rules/farming.md"
)]
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
