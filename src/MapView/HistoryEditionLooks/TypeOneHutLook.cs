using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The look of the type 1 hut's settler (<c>0x43fed8</c>): by the heading for a figure byte from 0x80, and by the way back with a
/// load, as the woodcutter's does, below it.
/// </summary>
/// <param name="tables">The tables of the exe: the hut's adds.</param>
public sealed class TypeOneHutLook(RuleTables tables) : HistoryEditionLook<TypeOneHut>
{
    readonly HutFigureAdds _adds = tables.Get<HutFigureAdds>(new TypeOneHut());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? (FacesBack(draw) ? _adds.HighFacing : _adds.High)
        : FigureBytes.WayBackWithLoad(draw) ? _adds.Carrying
        : _adds.Walk;

    // The settler's heading is 1 (byte 14, which the handler reads at 0x43fed8): the way back with a load, the first cut, or a step
    // back from the node to the south-west.
    static bool FacesBack(SettlerDraw draw) =>
        draw.Activity switch
        {
            WalksFree { Trip.Purpose: BackToHut { Carrying: true } } => true,
            Cuts cut => cut.Count == 1,
            Walks => draw.Heading == new Heading { Value = 1 },
            _ => false,
        };
}
