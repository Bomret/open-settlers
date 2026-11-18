using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The builder's tables: his figure adds from the immediates of his handler (<c>0x43faf4</c>), where the stub of job 3 must jump to
/// it, the request a building site makes for him (<c>0x42d5fc</c>), and his state.
/// </summary>
[StateName(9, "builds", TableEvidence.State)]
sealed class BuilderTables : JobTables<Builder>
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
                new BuilderFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BuilderWalkAddress),
                    Work = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.BuilderWorkAddress),
                }
            )
        );
        data.Add((Job, ProgramTablesImporter.WorkerRequest(exe, ProgramTablesImporter.BuilderRequestAddress)));
    }
}
