using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The look of the fisher (<c>0x43fed8</c>): for a figure byte from 0x80 (the cast) by whether he has caught a fish (byte 14), and
/// below it by the way back with a fish, as the woodcutter's does.
/// </summary>
/// <param name="tables">The tables of the exe: the fisher's adds.</param>
sealed class FisherLook(RuleTables tables) : HistoryEditionLook<Fisher>
{
    readonly FisherFigureAdds _adds = tables.Get<FisherFigureAdds>(new Fisher());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? (HasCaught(draw) ? _adds.HighFacing : _adds.High)
        : FigureBytes.WayBackWithLoad(draw) ? _adds.Carrying
        : _adds.Walk;

    // Byte 14 of the settler is 1 (the handler reads it at 0x43fed8): the fisher has caught a fish. During the cast it is the catch
    // flag; the settler's heading, which a step from a node reads as byte 14, is 1 when he faces south-east.
    static bool HasCaught(SettlerDraw draw) =>
        draw.Activity switch
        {
            WalksFree { Trip.Purpose: BackToHut { Carrying: true } } => true,
            Fishes fishes => fishes.Caught,
            Walks => draw.Heading == new Heading { Value = 1 },
            _ => false,
        };
}
