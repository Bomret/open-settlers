using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The builder's look (<c>0x43faf4</c>): the work's constant for a figure byte from 0x80 and the walk's below it.
/// </summary>
/// <param name="tables">The tables of the exe: the builder's adds.</param>
public sealed class BuilderLook(RuleTables tables) : HistoryEditionLook<Builder>(tables)
{
    private readonly BuilderFigureAdds _adds = tables.Get<BuilderFigureAdds>(new Builder());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? _adds.Work : _adds.Walk;
}
