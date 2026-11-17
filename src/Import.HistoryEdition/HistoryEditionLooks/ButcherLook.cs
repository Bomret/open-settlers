using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The butcher's look (<c>0x43ff90</c>): the work's constant for a figure byte from 0x80, the meat's for the step that takes it out
/// (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="tables">The tables of the exe: the butcher's adds.</param>
sealed class ButcherLook(RuleTables tables) : HistoryEditionLook<Butcher>
{
    readonly ButcherFigureAdds _adds = tables.Get<ButcherFigureAdds>(new Butcher());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Work
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}
