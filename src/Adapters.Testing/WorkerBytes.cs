using System;
using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>The bytes of a settler's and a hut's items as the original holds them, where the engine keeps the same bytes.</summary>
public static class WorkerBytes
{
    /// <summary>
    /// The settler's 16 bytes as the original's item holds them (Table16): the job and player, the frame, the timer, the
    /// node's offset (four bytes for each node, and the original's row holds the two records of its nodes, so a row is
    /// twice the map's width), the stamp, the state, the mode, the target, the heading, and the next state.
    /// </summary>
    public static string Hex(Settler s, int width)
    {
        var raw = SettlerEncoder.Fields(s, TestCatalog.Default);
        var bytes = new List<byte> { (byte)(s.Job.OriginalCode!.Value << 2 | s.Player), (byte)s.Frame };
        bytes.AddRange(BitConverter.GetBytes(s.Timer));
        bytes.AddRange(BitConverter.GetBytes((s.Y * 2 * width + s.X) * 4));
        bytes.AddRange(BitConverter.GetBytes(raw.Stamp));
        bytes.AddRange([(byte)raw.State, (byte)raw.Mode]);
        bytes.AddRange(BitConverter.GetBytes((ushort)raw.Target));
        bytes.AddRange([(byte)raw.Heading, (byte)raw.Next]);

        return Convert.ToHexString([.. bytes]);
    }

    /// <summary>
    /// The settler's 16 bytes as the original's item holds them, for the tests of construction. The slots the item names (a flag or
    /// a building) are one higher than the engine's, the dummies of the original's tables.
    /// </summary>
    public static string BuilderHex(Settler settler, int width)
    {
        var bytes = Convert.FromHexString(Hex(settler, width));
        var slot = settler.Activity switch
        {
            Builds => true,
            Leaving or Walks { Errand: not ToStorage } => true,
            WaitsOutStep { Then: Walks { Errand: not ToStorage } } => true,
            _ => false,
        };
        if (slot)
        {
            var target = BitConverter.ToUInt16(bytes, 12) + 1;
            bytes[12] = (byte)target;
            bytes[13] = (byte)(target >> 8);
        }

        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// The hut's 18 bytes (Table18): the node's offset, the type byte, the status, the flag slot, bytes 8 and 9, the worker's
    /// slot, the progress, and the pointer to a storage item, which a hut has none.
    /// </summary>
    public static string Hex(Building b, int width)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes((b.Y * 2 * width + b.X) * 4));
        bytes.AddRange([
            (byte)(BuildingEncoder.TypeIndex(b, TestCatalog.Default) << 2),
            (byte)BuildingEncoder.Status(b),
        ]);
        bytes.AddRange(BitConverter.GetBytes((ushort)b.Flag));
        bytes.AddRange([0, 0]);
        bytes.AddRange(BitConverter.GetBytes((ushort)BuildingEncoder.Worker(b)));
        bytes.AddRange(BitConverter.GetBytes(b.Progress));
        bytes.AddRange([0, 0, 0, 0]);

        return Convert.ToHexString([.. bytes]);
    }
}
