using System;
using static OpenSettlers.Testing.ProgramImage;

namespace OpenSettlers.Tests;

/// <summary>
/// The flame lists of the original's burning draw in a program image built in code (docs/rules/removal.md, "The burning
/// draw"). It stays with the tests because <c>FlameLists</c>, which gives the lists' address, is internal to the History
/// Edition source.
/// </summary>
static class FlameListImage
{
    // Every list holds one small flame at (0, 0), but list 2 holds a small flame at (-3, 5) and a large one at (4, -12), and
    // list 0 a large one at (1, -2). Types 7 and 8 point to one list.
    public static void Lists(byte[] exe)
    {
        var header = At(FlameLists.Address);
        var common = 50;
        for (var list = 0; list < FlameLists.ListCount; list++)
        {
            BitConverter.GetBytes((short)common).CopyTo(exe, header + 2 * list);
        }

        exe[header + common + 3] = 0xff;
        BitConverter.GetBytes((short)60).CopyTo(exe, header);
        new byte[] { 8, 1, 0xfe, 0xff }.CopyTo(exe, header + 60);
        BitConverter.GetBytes((short)70).CopyTo(exe, header + 2 * 2);
        new byte[] { 0, 0xfd, 5, 8, 4, 0xf4, 0x80 }.CopyTo(exe, header + 70);
        BitConverter.GetBytes((short)80).CopyTo(exe, header + 2 * 7);
        BitConverter.GetBytes((short)80).CopyTo(exe, header + 2 * 8);
        new byte[] { 8, 2, 3, 0xff }.CopyTo(exe, header + 80);
    }
}
