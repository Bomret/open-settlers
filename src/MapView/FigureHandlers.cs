using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The figure handler of a job (<c>0x43f744</c> plus 8 for each job): what it adds to a settler's figure byte to make the figure word
/// (<see cref="SettlerFigureTables"/>). A job without a handler has no drawn figure.
/// </summary>
public interface IFigureHandler
{
    /// <summary>What the handler adds to <paramref name="figure"/>, the figure byte of the settler of <paramref name="draw"/>.</summary>
    int Add(SettlerDraw draw, int figure);
}

/// <summary>The figure handlers share the figure byte's top bit, which marks the frames of a settler's work.</summary>
internal static class FigureBytes
{
    // The handlers (0x43fb6b and its siblings) test the figure byte for bit 7.
    private const int HighFigure = 0x80;

    /// <summary>Whether the figure byte is one of the work's frames (from 0x80).</summary>
    public static bool IsWork(int figure) => figure >= HighFigure;

    /// <summary>The way back with a load: the free walk back to the hut's flag with the log or stone (<c>0x43fc0b</c>, <c>0x43ff40</c>).</summary>
    public static bool WayBackWithLoad(SettlerDraw draw) =>
        draw.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: true } };
}

/// <summary>
/// The carrier's handler (<c>0x43f8ae</c>): on its road or handing a good over (the states 3 and 0xe) it adds the word of the good it
/// carries (byte 11), from the table at <c>0x43f4b7</c> (2 * good bytes on, the row of the figure bases in its high byte). A carrier with
/// no good adds nothing, and so does a good the table does not cover. A carrier in state 0x42 has no record (byte 11 is the road
/// direction there, no good).
/// </summary>
/// <param name="carried">For each good the word a carrier carrying it adds.</param>
public sealed class CarrierFigures(short[] carried) : IFigureHandler
{
    /// <inheritdoc />
    public int Add(SettlerDraw draw, int figure) =>
        draw.Activity is WalksRoad or HandsOver && draw.Carried.Code > 0 && draw.Carried.Code < carried.Length
            ? carried[draw.Carried.Code]
            : 0;
}

/// <summary>
/// A handler that adds the work's constant for a figure byte from 0x80 and the walk's below it: the builder's (<c>0x43faf4</c>), the
/// digger's (<c>0x43fa87</c>), and the forester's (<c>0x43fd88</c>, the waiting and planting frames included).
/// </summary>
/// <param name="work">What it adds to a work frame.</param>
/// <param name="walk">What it adds to every other frame.</param>
public sealed class WorkFigures(int work, int walk) : IFigureHandler
{
    /// <inheritdoc />
    public int Add(SettlerDraw draw, int figure) => FigureBytes.IsWork(figure) ? work : walk;
}

/// <summary>
/// The woodcutter's handler (<c>0x43fb6b</c>): the felling's constant from 0x80, below it the way back's with a load (byte 13 0x80,
/// byte 14 1, state 0x10) or the walk's.
/// </summary>
/// <param name="adds">What it adds.</param>
public sealed class WoodcutterFigures(FigureAdds adds) : IFigureHandler
{
    /// <inheritdoc />
    public int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? adds.Cutting
        : FigureBytes.WayBackWithLoad(draw) ? adds.Carrying
        : adds.Walk;
}

/// <summary>
/// The stonecutter's handler (<c>0x43fcd8</c>): the stroke's constant from 0x80, below it the way back's with the stone, also for the
/// step back off the heap (state 0x17, byte 13 2), or the walk's.
/// </summary>
/// <param name="adds">What it adds.</param>
public sealed class StonecutterFigures(FigureAdds adds) : IFigureHandler
{
    /// <inheritdoc />
    public int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? adds.Cutting
        : FigureBytes.WayBackWithLoad(draw) || draw.Activity is CutsStone { Phase: 2 } ? adds.Carrying
        : adds.Walk;
}

/// <summary>
/// The sawmiller's handler (<c>0x43fc21</c>): the saw's constant for a figure byte from 0x80, the plank's for the step that takes it
/// out (state 5 with state 0xd next), and the walk's for every other.
/// </summary>
/// <param name="adds">What it adds.</param>
public sealed class SawmillerFigures(SawmillerFigureAdds adds) : IFigureHandler
{
    /// <inheritdoc />
    public int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? adds.Saw
        : draw.Activity is WaitsOutStep { Then: PutsDownGood } ? adds.Carrying
        : adds.Walk;
}

/// <summary>
/// The handler of the type 1 hut's settler (<c>0x43fed8</c>): by the heading for a figure byte from 0x80, and by the way back with a
/// load, as the woodcutter's does, below it.
/// </summary>
/// <param name="adds">What it adds.</param>
public sealed class TypeOneHutFigures(HutFigureAdds adds) : IFigureHandler
{
    /// <inheritdoc />
    public int Add(SettlerDraw draw, int figure) =>
        FigureBytes.IsWork(figure) ? (FacesBack(draw) ? adds.HighFacing : adds.High)
        : FigureBytes.WayBackWithLoad(draw) ? adds.Carrying
        : adds.Walk;

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
