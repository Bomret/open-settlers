using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The woodcutter's look (<c>0x43fb6b</c>): the felling's constant from 0x80, below it the way back's with a load (byte 13 0x80,
/// byte 14 1, state 0x10) or the walk's.
/// </summary>
/// <param name="tables">The tables of the exe: the woodcutter's adds.</param>
public sealed class WoodcutterLook(RuleTables tables) : HistoryEditionLook<Woodcutter>
{
    private readonly FigureAdds _adds = tables.Get<FigureAdds>(new Woodcutter());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Cutting
        : FigureBytes.WayBackWithLoad(draw) ? _adds.Carrying
        : _adds.Walk;
}
