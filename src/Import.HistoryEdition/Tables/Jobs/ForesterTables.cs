using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The forester's tables: his figure adds from the immediates of his handler (<c>0x43fd88</c>), where the stub of job 8 must jump to
/// it, and his states.
/// </summary>
[StateName(
    0x13,
    "looks-for-spot",
    "0x438c9f draws nodes of the spiral until a free node of lowland terrain comes up; docs/rules/forestry.md"
)]
[StateName(0x14, "plants", "0x4389bf waits on the node and puts the sapling down; docs/rules/forestry.md")]
sealed class ForesterTables : JobTables<Forester>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.ForesterHandlerAddress,
            "jumps to the forester's handler"
        );
        data.Add(
            (
                Job,
                new ForesterFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.ForesterWalkAddress),
                    Work = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.ForesterWorkAddress),
                }
            )
        );
    }
}
