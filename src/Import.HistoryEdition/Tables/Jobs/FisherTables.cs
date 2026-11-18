using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The fisher's tables: his figure adds from the immediates of his handler (<c>0x43fed8</c>), where the stub of job 11 must jump to it,
/// and his states.
/// </summary>
[StateName(
    0x1f,
    "looks-for-water",
    "0x433f71 draws nodes of the spiral until a free node stands at a shore; docs/rules/fishing.md"
)]
[StateName(0x20, "fishes", "0x433e05 casts for a fish at the lake detail beside the node; docs/rules/fishing.md")]
sealed class FisherTables : JobTables<Fisher>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.FisherHandlerAddress,
            "jumps to the fisher's handler"
        );
        data.Add(
            (
                Job,
                new FisherFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FisherWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FisherCarryingAddress),
                    High = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FisherHighAddress),
                    HighFacing = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.FisherHighFacingAddress),
                }
            )
        );
    }
}
