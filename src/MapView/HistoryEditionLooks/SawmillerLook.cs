using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The sawmiller's look (<c>0x43fc21</c>): the saw's constant for a figure byte from 0x80, the plank's for the step that takes it
/// out (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="tables">The tables of the exe: the sawmiller's adds.</param>
public sealed class SawmillerLook(RuleTables tables) : HistoryEditionLook<Sawmiller>(tables)
{
    private readonly SawmillerFigureAdds _adds = tables.Get<SawmillerFigureAdds>(new Sawmiller());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Saw
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}
