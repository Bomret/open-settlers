using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Tests;

/// <summary>Builds a graphics archive in memory: header, 8-byte rows (size, offset), then the payloads.</summary>
public static class ArchiveBytes
{
    /// <summary>
    /// An archive of <c>rows</c> table rows, row 0 being the header. <c>payloads</c> maps a row number to the
    /// bytes of that entry; every other row is empty. The payloads follow the table in row order.
    /// </summary>
    public static byte[] Build(int rows, IReadOnlyDictionary<int, byte[]>? payloads = null)
    {
        payloads ??= new Dictionary<int, byte[]>();
        var table = rows * 8;
        var body = new MemoryStream();
        var sizes = new uint[Math.Max(rows, 1)];
        var offsets = new uint[Math.Max(rows, 1)];
        for (var row = 1; row < rows; row++)
        {
            if (payloads.TryGetValue(row, out var payload))
            {
                sizes[row] = (uint)payload.Length;
                offsets[row] = (uint)(table + body.Length);
                body.Write(payload);
            }
        }

        var bytes = new byte[table + body.Length];
        Put(bytes, 0, (uint)bytes.Length);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), (ushort)rows);
        for (var row = 1; row < rows; row++)
        {
            Put(bytes, row * 8, sizes[row]);
            Put(bytes, row * 8 + 4, offsets[row]);
        }

        body.ToArray().CopyTo(bytes, table);

        return bytes;
    }

    /// <summary>Writes a little-endian 32-bit value at <c>offset</c>.</summary>
    public static void Put(byte[] bytes, int offset, uint value) =>
        BitConverter.TryWriteBytes(bytes.AsSpan(offset), value);
}
