using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// A flag of the player's: the node it stands on and the item of the original's flag table that goes with it, a side for
/// each of the six directions. The flag's position in the <see cref="FlagTable"/> is its slot in the original's flag table
/// (<c>Table70</c>, <c>docs/rules/flags.md</c>); the same number is the link the node holds in the map's resource byte. A
/// slot whose flag was taken down (<c>docs/rules/removal.md</c>) is free until a new flag takes it, and keeps its item.
/// </summary>
/// <remarks>A flag on the node at (<paramref name="x"/>, <paramref name="y"/>) with an empty item.</remarks>
/// <param name="x">The node's column.</param>
/// <param name="y">The node's row.</param>
public sealed class Flag(int x, int y)
{
    readonly FlagSide[] _sides = [.. Enumerable.Range(0, Direction.Count).Select(_ => new FlagSide())];
    readonly Request[] _requests = new Request[2];
    readonly WaitingGood[] _places = [.. Enumerable.Range(0, PlaceCount).Select(_ => new WaitingGood())];

    /// <summary>The number of places for goods on a flag.</summary>
    public const int PlaceCount = 8;

    /// <summary>The node's column; -1 in a free slot.</summary>
    public int X { get; private set; } = x;

    /// <summary>The node's row; -1 in a free slot.</summary>
    public int Y { get; private set; } = y;

    /// <summary>Whether the slot holds no flag.</summary>
    public bool IsFree => X < 0;

    /// <summary>
    /// Whether a request of this flag for a carrier found none; the retry scan clears it, and while it is set the flag asks
    /// for no further carrier.
    /// </summary>
    public bool CarrierRequestFailed { get; internal set; }

    /// <summary>Whether goods on the flag still need their turn: a put sets it, a turn clears it, a turn that could not finish sets it again.</summary>
    public bool NeedsGoodsTurn { get; internal set; }

    /// <summary>
    /// The request of the flag in a request word (bytes <c>0x42</c> to <c>0x45</c> of the flag item, <c>docs/rules/construction-materials.md</c>):
    /// none until a building site's placement sets them.
    /// </summary>
    public Request RequestOf(RequestWord word) => _requests[(int)word];

    /// <summary>The flag asks for a class of goods in a request word, at a rank (a new ask starts at rank 0).</summary>
    public void AskFor(RequestWord word, GoodClass asks, byte rank = 0) =>
        _requests[(int)word] = new Request { Asks = asks, Rank = rank };

    /// <summary>The flag asks for nothing more in a request word; the rank stays.</summary>
    public void ClearAsk(RequestWord word) => _requests[(int)word] = _requests[(int)word] with { Asks = null };

    /// <summary>Gives the request of a request word a rank; the class it asks for stays.</summary>
    public void RankRequest(RequestWord word, byte rank) =>
        _requests[(int)word] = _requests[(int)word] with { Rank = rank };

    /// <summary>
    /// A good is on its way to the flag's request: the rank of the request word shifts right by one where bit 0 is set, else it
    /// becomes 0.
    /// </summary>
    public void ServeRequest(RequestWord word)
    {
        var rank = _requests[(int)word].Rank;
        RankRequest(word, (rank & 1) != 0 ? (byte)(rank >> 1) : (byte)0);
    }

    /// <summary>Records whether the flag's request for a carrier found none.</summary>
    public void RecordCarrierRequest(bool failed) => CarrierRequestFailed = failed;

    /// <summary>The goods on the flag are due their turn.</summary>
    public void MarkGoodsTurnDue() => NeedsGoodsTurn = true;

    /// <summary>The turn of the goods on the flag has begun: none is due until a put or an unfinished turn sets it again.</summary>
    public void ClearGoodsTurn() => NeedsGoodsTurn = false;

    /// <summary>The eight places of the flag, for the goods that lie on it.</summary>
    public IReadOnlyList<WaitingGood> Places => _places;

    /// <summary>The side the road in <paramref name="direction"/> leaves the flag by.</summary>
    public FlagSide Side(Direction direction) => _sides[direction];

    /// <summary>The first place that holds no good, or -1 where every place holds one.</summary>
    public int FirstEmptyPlace()
    {
        for (var place = 0; place < _places.Length; place++)
        {
            if (_places[place].IsEmpty)
            {
                return place;
            }
        }

        return -1;
    }

    /// <summary>Takes the flag down: the slot has no node and keeps its item.</summary>
    internal void Release()
    {
        X = -1;
        Y = -1;
    }
}
