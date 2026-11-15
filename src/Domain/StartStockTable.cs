using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenSettlers.Domain;

/// <summary>
/// The start stock table of the program: the stock of the goods at each of its rows (the levels 0, 10, 20, 30, and 40). It is
/// read-only; the rows it is built from are copied. A collection expression of rows builds one.
/// </summary>
/// <param name="rows">The rows to copy from.</param>
[CollectionBuilder(typeof(StartStockTable), nameof(Of))]
public sealed class StartStockTable(byte[][] rows) : IEnumerable<byte[]>
{
    readonly ImmutableArray<ImmutableArray<byte>> _rows = [.. rows.Select(row => ImmutableArray.Create(row))];

    /// <summary>The number of rows.</summary>
    public int Count => _rows.Length;

    /// <summary>The stock of the goods at the row <paramref name="row"/>.</summary>
    public ImmutableArray<byte> this[int row] => _rows[row];

    /// <summary>Takes the rows of a table a caller builds as arrays.</summary>
    public static implicit operator StartStockTable(byte[][] rows) => new(rows);

    /// <summary>Builds the table of a collection expression of rows.</summary>
    public static StartStockTable Of(ReadOnlySpan<byte[]> rows) => new(rows.ToArray());

    IEnumerator<byte[]> IEnumerable<byte[]>.GetEnumerator() => _rows.Select(row => row.ToArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<byte[]>)this).GetEnumerator();
}
