using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The pig farmer's tables: his figure adds from the immediates of his handler (<c>0x43ff56</c>), where the stub of job 12 must jump to it,
/// the rank his request for grain starts from (the player word at <c>+0x158</c>), the table of the odds with which the pigs grow, and his
/// state.
/// </summary>
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
                new FoodHutFigureAdds
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
