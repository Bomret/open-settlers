using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The builder's tables: his figure adds from the immediates of his handler (<c>0x43faf4</c>), where the stub of job 3 must jump to
/// it, the request a building site makes for him (<c>0x42d5fc</c>), and his name and state.
/// </summary>
[JobName(
    "builder",
    "Job.Builder; the under-construction handler 0x42d55f asks for job 3 with the tool good 17 (request at 0x42d5fc) and the job's state 9 builds the site (0x43ec82); docs/rules/construction.md"
)]
[StateName(9, "builds", NameTable.StateEvidence)]
public sealed class BuilderTables : JobTables<Builder>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.BuilderHandlerAddress,
            "jumps to the builder's handler"
        );
        data.Add(
            (
                Job,
                new BuilderFigureAdds(
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BuilderWalkAddress),
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BuilderWorkAddress)
                )
            )
        );
        data.Add((Job, ProgramTablesImporter.WorkerRequest(exe, ProgramTablesImporter.BuilderRequestAddress)));
    }
}
