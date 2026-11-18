using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The digger's tables: his figure adds from the immediates of his handler (<c>0x43fa87</c>), where the stub of job 2 must jump to
/// it, the request a large site makes for him (<c>0x42d52a</c>), and his state. Every frame of his work is below 0x80, so
/// the walk's add is the one he is drawn by.
/// </summary>
[StateName(
    8,
    "digs",
    "0x43e45e walks the node and its six neighbours of the site and changes the heights one a pass; docs/rules/digging.md"
)]
sealed class DiggerTables : JobTables<Digger>
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
                new FigureAdds
                {
                    Walk = walk,
                    Carrying = walk,
                    Cutting = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.DiggerHighAddress),
                }
            )
        );
        data.Add((Job, ProgramTablesImporter.WorkerRequest(exe, ProgramTablesImporter.DiggerRequestAddress)));
    }
}
