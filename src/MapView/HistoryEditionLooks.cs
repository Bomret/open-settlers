using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The carrier's look (<c>0x43f8ae</c>): on its road or handing a good over (the states 3 and 0xe) it adds the word of the good it
/// carries (byte 11), from the table at <c>0x43f4b7</c> (2 * good bytes on, the row of the figure bases in its high byte). A carrier with
/// no good adds nothing, and so does a good the table does not cover. A carrier in state 0x42 has no record (byte 11 is the road
/// direction there, no good).
/// </summary>
/// <param name="tables">The tables of the exe: for each good the word a carrier carrying it adds.</param>
public sealed class CarrierLook(RuleTables tables) : HistoryEditionLook<Carrier>(tables)
{
    private readonly short[] _carried = tables.CarriedFigures;

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        draw.Activity is WalksRoad or HandsOver && draw.Carried.Code > 0 && draw.Carried.Code < _carried.Length
            ? _carried[draw.Carried.Code]
            : 0;
}

/// <summary>
/// The builder's look (<c>0x43faf4</c>): the work's constant for a figure byte from 0x80 and the walk's below it.
/// </summary>
/// <param name="tables">The tables of the exe: the builder's adds.</param>
public sealed class BuilderLook(RuleTables tables) : HistoryEditionLook<Builder>(tables)
{
    private readonly BuilderFigureAdds _adds = tables.Get<BuilderFigureAdds>(Job.Builder);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? _adds.Work : _adds.Walk;
}

/// <summary>
/// The digger's look (<c>0x43fa87</c>): the work's constant for a figure byte from 0x80 and the walk's below it.
/// </summary>
/// <param name="tables">The tables of the exe: the digger's adds.</param>
public sealed class DiggerLook(RuleTables tables) : HistoryEditionLook<Digger>(tables)
{
    private readonly FigureAdds _adds = tables.Get<FigureAdds>(Job.Digger);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? _adds.Cutting : _adds.Walk;
}

/// <summary>
/// The forester's look (<c>0x43fd88</c>, the waiting and planting frames included): the work's constant for a figure byte from 0x80
/// and the walk's below it.
/// </summary>
/// <param name="tables">The tables of the exe: the forester's adds.</param>
public sealed class ForesterLook(RuleTables tables) : HistoryEditionLook<Forester>(tables)
{
    private readonly ForesterFigureAdds _adds = tables.Get<ForesterFigureAdds>(Job.Forester);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? _adds.Work : _adds.Walk;
}

/// <summary>
/// The woodcutter's look (<c>0x43fb6b</c>): the felling's constant from 0x80, below it the way back's with a load (byte 13 0x80,
/// byte 14 1, state 0x10) or the walk's.
/// </summary>
/// <param name="tables">The tables of the exe: the woodcutter's adds.</param>
public sealed class WoodcutterLook(RuleTables tables) : HistoryEditionLook<Woodcutter>(tables)
{
    private readonly FigureAdds _adds = tables.Get<FigureAdds>(Job.Woodcutter);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Cutting
        : FigureBytes.WayBackWithLoad(draw) ? _adds.Carrying
        : _adds.Walk;
}

/// <summary>
/// The stonecutter's look (<c>0x43fcd8</c>): the stroke's constant from 0x80, below it the way back's with the stone, also for the
/// step back off the heap (state 0x17, byte 13 2), or the walk's.
/// </summary>
/// <param name="tables">The tables of the exe: the stonecutter's adds.</param>
public sealed class StonecutterLook(RuleTables tables) : HistoryEditionLook<Stonecutter>(tables)
{
    private readonly FigureAdds _adds = tables.Get<FigureAdds>(Job.Stonecutter);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Cutting
        : FigureBytes.WayBackWithLoad(draw) || draw.Activity is CutsStone { Phase: 2 } ? _adds.Carrying
        : _adds.Walk;
}

/// <summary>
/// The sawmiller's look (<c>0x43fc21</c>): the saw's constant for a figure byte from 0x80, the plank's for the step that takes it
/// out (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="tables">The tables of the exe: the sawmiller's adds.</param>
public sealed class SawmillerLook(RuleTables tables) : HistoryEditionLook<Sawmiller>(tables)
{
    private readonly SawmillerFigureAdds _adds = tables.Get<SawmillerFigureAdds>(Job.Sawmiller);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? _adds.Saw
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? _adds.Carrying
        : _adds.Walk;
}

/// <summary>
/// The look of the type 1 hut's settler (<c>0x43fed8</c>): by the heading for a figure byte from 0x80, and by the way back with a
/// load, as the woodcutter's does, below it.
/// </summary>
/// <param name="tables">The tables of the exe: the hut's adds.</param>
public sealed class TypeOneHutLook(RuleTables tables) : HistoryEditionLook<TypeOneHut>(tables)
{
    private readonly HutFigureAdds _adds = tables.Get<HutFigureAdds>(Job.TypeOneHut);

    /// <inheritdoc />
    public override int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? (FacesBack(draw) ? _adds.HighFacing : _adds.High)
        : FigureBytes.WayBackWithLoad(draw) ? _adds.Carrying
        : _adds.Walk;

    // The settler's heading is 1 (byte 14, which the handler reads at 0x43fed8): the way back with a load, the first cut, or a step
    // back from the node to the south-west.
    private static bool FacesBack(SettlerDraw draw) =>
        draw.Activity switch
        {
            WalksFree { Trip.Purpose: BackToHut { Carrying: true } } => true,
            Cuts cut => cut.Count == 1,
            Walks => draw.Heading == new Heading(1),
            _ => false,
        };
}
