using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The forester's look (<c>0x43fd88</c>, the waiting and planting frames included): the work's constant for a figure byte from 0x80
/// and the walk's below it.
/// </summary>
/// <param name="tables">The tables of the exe: the forester's adds.</param>
public sealed class ForesterLook(RuleTables tables) : HistoryEditionLook<Forester>(tables)
{
    private readonly ForesterFigureAdds _adds = tables.Get<ForesterFigureAdds>(new Forester());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? _adds.Work : _adds.Walk;
}
