using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The butcher's tables: his figure adds from the immediates of his handler (<c>0x43ff90</c>), where the stub of job 13 must jump to it,
/// and his state.
/// </summary>
[StateName(
    0x26,
    "butchers",
    "0x4332a8 takes a pig out of the building's fill and works on it for 0x5ff clock units, then carries meat out; inferred name, docs/rules/butchering.md"
)]
sealed class ButcherTables : JobTables<Butcher>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.ButcherHandlerAddress,
            "jumps to the butcher's handler"
        );
        data.Add(
            (
                Job,
                new FoodHutFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.ButcherWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.ButcherCarryingAddress),
                    Work = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.ButcherWorkAddress),
                }
            )
        );
    }
}
