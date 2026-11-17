using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The farmer's look (<c>0x44002b</c>): the sowing's constant for a figure byte from 0x80 while he sows, the harvest's while he
/// harvests, and below it the way back's with the grain or the walk's.
/// </summary>
/// <param name="tables">The tables of the exe: the farmer's adds.</param>
sealed class FarmerLook(RuleTables tables) : HistoryEditionLook<Farmer>
{
    readonly FarmerFigureAdds _adds = tables.Get<FarmerFigureAdds>(new Farmer());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? (draw.Activity is TendsField { Harvests: true } ? _adds.Harvest : _adds.Sow)
        : FigureBytes.WayBackWithLoad(draw) ? _adds.Carrying
        : _adds.Walk;
}
