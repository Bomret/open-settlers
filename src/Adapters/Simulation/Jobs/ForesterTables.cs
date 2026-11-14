using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The forester's tables: his figure adds from the immediates of his handler (<c>0x43fd88</c>), where the stub of job 8 must jump to
/// it, and his name and states.
/// </summary>
[JobName(
    "forester",
    "Forester; the type 9 handler 0x42d9c9 asks for job 8 with no tool, and his work plants the saplings 0x67 and 0x68 (0x4389bf); docs/rules/forestry.md"
)]
[StateName(
    0x13,
    "looks-for-spot",
    "0x438c9f draws nodes of the spiral until a free node of lowland terrain comes up; docs/rules/forestry.md"
)]
[StateName(0x14, "plants", "0x4389bf waits on the node and puts the sapling down; docs/rules/forestry.md")]
public sealed class ForesterTables : JobTables<Forester>
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
