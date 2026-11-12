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
