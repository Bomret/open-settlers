using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The carrier's look (<c>0x43f8ae</c>): on its road or handing a good over (the states 3 and 0xe) it adds the word of the good it
/// carries (byte 11), from the table at <c>0x43f4b7</c> (2 * good bytes on, the row of the figure bases in its high byte). A carrier with
/// no good adds nothing, and so does a good with no original code (<see cref="GoodTables.CodeOf"/>) or one the table does not cover. A carrier in state 0x42 has no record (byte 11 is the road
/// direction there, no good).
/// </summary>
/// <param name="tables">The tables of the exe: for each good the word a carrier carrying it adds.</param>
sealed class CarrierLook(RuleTables tables) : HistoryEditionLook<Carrier>
{
    readonly FigureTables _figures = tables.Figures;

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        draw.Activity is WalksRoad or HandsOver && GoodTables.CodeOf(draw.Carried) is { } code
            ? _figures.CarriedWord(code)
            : 0;
}
