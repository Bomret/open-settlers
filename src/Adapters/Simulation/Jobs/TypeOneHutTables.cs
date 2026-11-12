using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The tables of the settler of the type 1 hut: his figure adds from the immediates of his handler (<c>0x43fed8</c>), where the stub
/// of job 11 must jump to it. The job has no name.
/// </summary>
public sealed class TypeOneHutTables : JobTables<TypeOneHut>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.TypeOneHutHandlerAddress,
            "jumps to the handler of the settler of the type 1 hut"
        );
        data.Add(
            (
                Job,
                new HutFigureAdds(
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.TypeOneHutWalkAddress),
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.TypeOneHutCarryingAddress),
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.TypeOneHutHighAddress),
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.TypeOneHutHighFacingAddress)
                )
            )
        );
    }
}
