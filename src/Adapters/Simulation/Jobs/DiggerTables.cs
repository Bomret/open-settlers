using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The digger's tables: his figure adds from the immediates of his handler (<c>0x43fa87</c>), where the stub of job 2 must jump to
/// it, the request a large site makes for him (<c>0x42d52a</c>), and his name and state. Every frame of his work is below 0x80, so
/// the walk's add is the one he is drawn by.
/// </summary>
[JobName(
    "digger",
    "Digger; the large site's handler 0x42d328 asks for job 2 with the tool good 16 (request at 0x42d52a) and the job's state 8 changes the heights of the site's node and its six neighbours by one a pass (0x43e45e); docs/rules/digging.md"
)]
[StateName(
    8,
    "digs",
    "0x43e45e walks the node and its six neighbours of the site and changes the heights one a pass; docs/rules/digging.md"
)]
public sealed class DiggerTables : JobTables<Digger>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.DiggerHandlerAddress,
            "jumps to the digger's handler"
        );
        var walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.DiggerWalkAddress);
        data.Add(
            (
                Job,
                new FigureAdds(
                    walk,
                    walk,
                    ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.DiggerHighAddress)
                )
            )
        );
        data.Add((Job, ProgramTablesImporter.WorkerRequest(exe, ProgramTablesImporter.DiggerRequestAddress)));
    }
}
