using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The pig farmer's look (<c>0x43ff56</c>): the work's constant for a figure byte from 0x80, the pig's for the step that takes it out
/// (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="tables">The tables of the exe: the pig farmer's adds.</param>
sealed class PigFarmerLook(RuleTables tables) : HistoryEditionLook<PigFarmer>
{
    readonly PigFarmerFigureAdds _adds = tables.Get<PigFarmerFigureAdds>(new PigFarmer());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Work
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}
