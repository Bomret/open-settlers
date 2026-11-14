using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The sawmiller's tables: his figure adds from the immediates of his handler (<c>0x43fc21</c>), where the stub of job 6 must jump
/// to it, and his name and states.
/// </summary>
[JobName(
    "sawmiller",
    "Sawmiller; the handler 0x42da0b of the building type 0x11 asks for job 6 with the tool good 22 and the job's state 0x18 takes a log out of the building (0x4359fe) and carries the plank (good 8) out to the flag (0x43c6aa); docs/rules/sawmilling.md"
)]
[StateName(
    0xb,
    "steps-out-with-good",
    "0x43e228 steps from the door onto the flag where the flag has a free place, with the good in his hands; docs/rules/sawmilling.md"
)]
[StateName(
    0xd,
    "puts-down-good",
    "0x43c6aa lays the good in his hands on the first empty place of the flag, then enters the building; docs/rules/sawmilling.md"
)]
[StateName(
    0x18,
    "saws",
    "0x4359fe takes a log out of the building's fill and saws it for 0x93f clock units; docs/rules/sawmilling.md"
)]
public sealed class SawmillerTables : JobTables<Sawmiller>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.SawmillerHandlerAddress,
            "jumps to the sawmiller's handler"
        );
        data.Add(
            (
                Job,
                new SawmillerFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.SawmillerWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.SawmillerCarryingAddress),
                    Saw = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.SawmillerSawAddress),
                }
            )
        );
    }
}
