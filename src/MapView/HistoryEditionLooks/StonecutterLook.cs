using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The stonecutter's look (<c>0x43fcd8</c>): the stroke's constant from 0x80, below it the way back's with the stone, also for the
/// step back off the heap (state 0x17, byte 13 2), or the walk's.
/// </summary>
/// <param name="tables">The tables of the exe: the stonecutter's adds.</param>
public sealed class StonecutterLook(RuleTables tables) : HistoryEditionLook<Stonecutter>(tables)
{
    private readonly FigureAdds _adds = tables.Get<FigureAdds>(new Stonecutter());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Cutting
        : FigureBytes.WayBackWithLoad(draw) || draw.Activity is CutsStone { Phase: 2 } ? _adds.Carrying
        : _adds.Walk;
}
