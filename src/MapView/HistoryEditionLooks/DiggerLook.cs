using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The digger's look (<c>0x43fa87</c>): the work's constant for a figure byte from 0x80 and the walk's below it.
/// </summary>
/// <param name="tables">The tables of the exe: the digger's adds.</param>
public sealed class DiggerLook(RuleTables tables) : HistoryEditionLook<Digger>
{
    private readonly FigureAdds _adds = tables.Get<FigureAdds>(new Digger());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? _adds.Cutting : _adds.Walk;
}
