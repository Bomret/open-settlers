using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The look of an indoor food job under the History Edition source: the work's constant for a figure byte from 0x80, the good's for the step
/// that takes it out (state 5 with state 0xd next), and the walk's for every other. The miller's, pig farmer's, butcher's, and baker's figure
/// handlers have this shape; each job's look names its job and its handler's address.
/// </summary>
/// <typeparam name="TJob">The job the look is for.</typeparam>
abstract class FoodHutLook<TJob>(RuleTables tables) : HistoryEditionLook<TJob>
    where TJob : Job, new()
{
    readonly FoodHutFigureAdds _adds = tables.Get<FoodHutFigureAdds>(new TJob());

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Work
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}
