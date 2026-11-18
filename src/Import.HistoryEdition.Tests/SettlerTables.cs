namespace OpenSettlers.Tests;

/// <summary>
/// Figure tables over made-up job data. They live in this test project because <c>SettlerFigureTables</c> is internal to the
/// History Edition source.
/// </summary>
static class SettlerTables
{
    // Figure tables over made-up job data: the given adds of each job, all 0 (the carrier's row) for the ones not given, and the figure
    // bases, offsets, and waiting carriers' tables given apart from the rule tables.
    public static SettlerFigureTables TablesOf(
        short[] bases,
        short[] offsets,
        byte[] sequences,
        short[]? carried = null,
        FigureAdds woodcutter = default,
        FisherFigureAdds typeOneHut = default,
        (short[] Phases, short[] Figures, sbyte[] Offsets)? idle = null,
        BuilderFigureAdds builder = default,
        FigureAdds stonecutter = default,
        ForesterFigureAdds forester = default,
        FigureAdds digger = default,
        SawmillerFigureAdds sawmiller = default,
        FoodHutFigureAdds miller = default,
        FarmerFigureAdds farmer = default,
        FoodHutFigureAdds baker = default,
        FoodHutFigureAdds butcher = default,
        FoodHutFigureAdds pigFarmer = default
    ) =>
        new(
            new RuleTables(
                new ObjectClass[RuleTables.ClassCount],
                MadeProgram.MadeStartStock(),
                figureBases: bases,
                figureOffsets: offsets,
                carriedFigures: carried ?? [],
                idlePhases: idle?.Phases,
                idleFigures: idle?.Figures,
                idleOffsets: idle?.Offsets,
                jobData:
                [
                    (new Woodcutter(), woodcutter),
                    (new Stonecutter(), stonecutter),
                    (new Digger(), digger),
                    (new Builder(), builder),
                    (new Forester(), forester),
                    (new Sawmiller(), sawmiller),
                    (new Miller(), miller),
                    (new Fisher(), typeOneHut),
                    (new Farmer(), farmer),
                    (new Baker(), baker),
                    (new Butcher(), butcher),
                    (new PigFarmer(), pigFarmer),
                ]
            ),
            sequences
        );
}
