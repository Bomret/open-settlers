using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The pig farmer's tables: his figure adds from the immediates of his handler (<c>0x43ff56</c>), where the stub of job 12 must jump to it,
/// the rank his request for grain starts from (the player word at <c>+0x158</c>), the table of the odds with which the pigs grow, and his
/// name and state.
/// </summary>
[JobName(
    "pig-farmer",
    "Pig farmer; the handler 0x42e0fa of the building type 0xe asks for job 12 with no tool, its start 0x43d179 asks for grain (class 4) and its state 0x25 takes grain out of the building (0x4333c1) and carries a pig (good 2, 0x433577) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/pig-farming.md"
)]
[StateName(
    0x25,
    "tends-pigs",
    "0x4333c1 takes grain out of the building's fill, works and pauses in stretches while the pigs the building holds grow, and carries a pig out; inferred name, docs/rules/pig-farming.md"
)]
sealed class PigFarmerTables : JobTables<PigFarmer>
{
    /// <inheritdoc />
    public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data)
    {
        ProgramTablesImporter.ExpectStub(
            exe,
            Job,
            ProgramTablesImporter.PigFarmerHandlerAddress,
            "jumps to the pig farmer's handler"
        );
        data.Add(
            (
                Job,
                new PigFarmerFigureAdds
                {
                    Walk = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.PigFarmerWalkAddress),
                    Carrying = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.PigFarmerCarryingAddress),
                    Work = ProgramTablesImporter.Immediate(exe, ProgramTablesImporter.PigFarmerWorkAddress),
                }
            )
        );
        data.Add(
            (
                Job,
                new PigFarmRates
                {
                    RankBase = ProgramTablesImporter.PigRankBase(exe),
                    Growth = ProgramTablesImporter.PigGrowth(exe),
                }
            )
        );
    }
}
