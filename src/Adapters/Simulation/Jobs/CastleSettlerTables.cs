namespace OpenSettlers.Simulation;

/// <summary>The castle settler's tables: his name.</summary>
[JobName(
    "castle-settler",
    "Job.CastleSettler; the castle's first settler stands by while the castle is built (state 10, 0x43f17a) and no building asks for job 4; Game.Removal reads job 4 as a settler that goes home as a carrier; docs/rules/settlers.md"
)]
public sealed class CastleSettlerTables : JobTables<CastleSettler> { }
