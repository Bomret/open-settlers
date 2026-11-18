using System;

namespace OpenSettlers.Testing;

/// <summary>A program image (<c>Siedler1.exe</c>) the tests build in code: no install, no library type.</summary>
public static class ProgramImage
{
    /// <summary>The address the image loads at.</summary>
    public const int ImageBase = 0x400000;

    /// <summary>The address of the section's first byte, relative to <see cref="ImageBase"/>.</summary>
    public const int SectionStart = 0x4e000;

    /// <summary>The file offset of the section's first byte.</summary>
    public const int RawStart = 0x200;

    /// <summary>A PE32 file with one section that holds the addresses from 0x44e000 on, `size` bytes of it, at the file offset 0x200.</summary>
    public static byte[] Executable(Action<byte[]> fill, int size = 0x400)
    {
        var exe = new byte[RawStart + size];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        BitConverter.GetBytes(0x40).CopyTo(exe, 0x3c);
        BitConverter.GetBytes(0x4550).CopyTo(exe, 0x40);
        BitConverter.GetBytes((ushort)1).CopyTo(exe, 0x40 + 6);
        BitConverter.GetBytes((ushort)0xe0).CopyTo(exe, 0x40 + 20);
        BitConverter.GetBytes(ImageBase).CopyTo(exe, 0x40 + 24 + 28);
        var header = 0x40 + 24 + 0xe0;
        BitConverter.GetBytes(SectionStart).CopyTo(exe, header + 12);
        BitConverter.GetBytes(size).CopyTo(exe, header + 16);
        BitConverter.GetBytes(RawStart).CopyTo(exe, header + 20);
        fill(exe);

        return exe;
    }

    /// <summary>The file offset of the address in an image <see cref="Executable"/> builds.</summary>
    public static int At(int address) => address - ImageBase - SectionStart + RawStart;
}
