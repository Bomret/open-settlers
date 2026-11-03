using System;
using System.IO;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The tables of the original's program data that the rules read: the object class table and the start stock
/// table. They are read from the install's <c>Siedler1.exe</c> at run time and never restated in source (INV-0001).
/// <see cref="Read"/> finds each by its address in the program, through the section headers of the executable.
/// </summary>
public sealed class ProgramTables
{
    /// <summary>The address of the object class table: 128 bytes, one for each object code (<c>0x41e640</c>).</summary>
    public const int ClassTableAddress = 0x41e640;

    /// <summary>The number of object codes, and the size of the class table.</summary>
    public const int ClassCount = 128;

    /// <summary>The address of the start stock table: 5 rows of 26 bytes (<c>0x442e8a</c>).</summary>
    public const int StartStockAddress = 0x442e8a;

    /// <summary>The number of rows of the start stock table (the levels 0, 10, 20, 30, and 40).</summary>
    public const int StartStockRows = 5;

    /// <summary>Creates tables from their contents; <see cref="Read"/> creates them from the program.</summary>
    public ProgramTables(byte[] classes, byte[][] startStock)
    {
        if (classes.Length != ClassCount || startStock.Length != StartStockRows || startStock.Any(row => row.Length != Simulation.StartStock.Goods))
        {
            throw new ArgumentException("A class table holds 128 bytes and a start stock table 5 rows of 26.");
        }

        Classes = classes;
        StartStock = startStock;
    }

    /// <summary>The class of each object code: 1 and 2 are the trees and stones, 3 a flag, 4 to 6 buildings.</summary>
    public byte[] Classes { get; }

    /// <summary>The start stock rows, each <see cref="Simulation.StartStock.Goods"/> counts.</summary>
    public byte[][] StartStock { get; }

    /// <summary>
    /// Reads the tables from the bytes of <c>Siedler1.exe</c>. Throws <see cref="InvalidDataException"/> when the
    /// bytes are no executable or a table lies outside the data the file holds.
    /// </summary>
    public static ProgramTables Read(byte[] exe)
    {
        var classes = Slice(exe, ClassTableAddress, ClassCount);
        var startStock = new byte[StartStockRows][];
        for (var row = 0; row < StartStockRows; row++)
        {
            startStock[row] = Slice(exe, StartStockAddress + row * Simulation.StartStock.Goods, Simulation.StartStock.Goods);
        }

        return new ProgramTables(classes, startStock);
    }

    // The bytes at a program address, found through the section that holds it (PE32: e_lfanew at 0x3c, the file
    // header 4 bytes after it, the image base at +28 of the optional header, 40 bytes for each section header).
    private static byte[] Slice(byte[] exe, int address, int count)
    {
        try
        {
            var pe = BitConverter.ToInt32(exe, 0x3c);
            if (exe[0] != 'M' || exe[1] != 'Z' || BitConverter.ToInt32(exe, pe) != 0x4550)
            {
                throw new InvalidDataException("Siedler1.exe is no PE executable.");
            }

            var sections = BitConverter.ToUInt16(exe, pe + 6);
            var optional = pe + 24;
            var imageBase = BitConverter.ToInt32(exe, optional + 28);
            var rva = address - imageBase;
            for (var i = 0; i < sections; i++)
            {
                var header = optional + BitConverter.ToUInt16(exe, pe + 20) + 40 * i;
                var start = BitConverter.ToInt32(exe, header + 12);
                var rawSize = BitConverter.ToInt32(exe, header + 16);
                if (rva >= start && rva + count <= start + rawSize)
                {
                    return exe[(BitConverter.ToInt32(exe, header + 20) + rva - start)..][..count];
                }
            }
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("Siedler1.exe is truncated.", e);
        }

        throw new InvalidDataException($"Siedler1.exe holds no data at 0x{address:x}.");
    }
}
