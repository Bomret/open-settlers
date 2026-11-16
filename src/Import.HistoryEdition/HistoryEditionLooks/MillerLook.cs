using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The miller's look (<c>0x4400d3</c>): the grinding's constant for a figure byte from 0x80, the sack's for the step that takes the flour
/// out (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="tables">The tables of the exe: the miller's adds.</param>
sealed class MillerLook(RuleTables tables) : HistoryEditionLook<Miller>
{
    readonly MillerFigureAdds _adds = tables.Get<MillerFigureAdds>(new Miller());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Grind
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}
