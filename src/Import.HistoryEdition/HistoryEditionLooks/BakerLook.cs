using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The baker's look (<c>0x440112</c>): the baking's constant for a figure byte from 0x80, the loaf's for the step that takes the bread
/// out (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="tables">The tables of the exe: the baker's adds.</param>
sealed class BakerLook(RuleTables tables) : HistoryEditionLook<Baker>
{
    readonly BakerFigureAdds _adds = tables.Get<BakerFigureAdds>(new Baker());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Bake
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}
